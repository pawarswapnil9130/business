using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;
using ApparelERP.Api.Services;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IInventoryService _inventoryService;

        public CustomerController(ApparelDbContext context, IConfiguration configuration, IInventoryService inventoryService)
        {
            _context = context;
            _configuration = configuration;
            _inventoryService = inventoryService;
        }

        // ==========================================
        // 1. PUBLIC SHOWCASE & ONBOARDING
        // ==========================================

        [HttpGet("public-showcase")]
        public async Task<ActionResult<IEnumerable<CustomerPublicProductDto>>> GetPublicShowcase()
        {
            // Public catalog strictly omits prices and exact stock counts
            var stockList = await _context.StockSummaries.ToListAsync();
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var showcase = products.Select(p =>
            {
                var stock = stockList.FirstOrDefault(s => s.ProductId == p.Id)?.CurrentStock ?? 0;
                var setSize = p.SetSize > 0 ? p.SetSize : 4;
                var availableSets = setSize > 0 ? (stock / setSize) : stock;
                return new CustomerPublicProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    DesignBrand = p.DesignBrand,
                    Size = p.Size,
                    Color = p.Color,
                    SetSize = setSize,
                    SetRatio = !string.IsNullOrWhiteSpace(p.SetRatio) ? p.SetRatio : "38, 40, 42, 44",
                    ImageUrl = p.ImageUrl,
                    Available = availableSets > 0
                };
            }).ToList();

            return Ok(showcase);
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyForWholesaleAccount(CustomerApplyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ShopName) || 
                string.IsNullOrWhiteSpace(dto.OwnerName) || 
                string.IsNullOrWhiteSpace(dto.Phone))
            {
                return BadRequest(new { message = "Shop name, owner name, and phone number are required." });
            }

            var cleanPhone = dto.Phone.Trim();
            var existingCustomer = await _context.Customers.AnyAsync(c => c.Phone == cleanPhone);
            if (existingCustomer)
            {
                return BadRequest(new { message = "An account application with this phone number already exists." });
            }

            var username = cleanPhone;
            var existingUser = await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            if (existingUser)
            {
                return BadRequest(new { message = "Username/phone is already registered." });
            }

            var password = !string.IsNullOrWhiteSpace(dto.Password) ? dto.Password : "Casa@" + cleanPhone.Substring(Math.Max(0, cleanPhone.Length - 4));

            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "CUSTOMER",
                IsApproved = false, // Must be approved by admin
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var customer = new Customer
            {
                UserId = user.Id,
                ShopName = dto.ShopName.Trim(),
                OwnerName = dto.OwnerName.Trim(),
                Phone = cleanPhone,
                Whatsapp = !string.IsNullOrWhiteSpace(dto.Whatsapp) ? dto.Whatsapp.Trim() : cleanPhone,
                Email = dto.Email?.Trim(),
                Address = dto.Address.Trim(),
                City = dto.City.Trim(),
                GstNumber = dto.GstNumber?.Trim(),
                CustomerType = "REGULAR", // Default group
                Status = "PENDING",       // Pending approval
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Wholesale account application submitted successfully! Casa Enterprises will review and activate your account shortly.",
                shopName = customer.ShopName
            });
        }

        // ==========================================
        // 2. 2-STEP AUTHENTICATION (PASSWORD + OTP)
        // ==========================================

        [HttpPost("auth/login")]
        public async Task<ActionResult<CustomerLoginStep1ResponseDto>> LoginStep1(CustomerLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PhoneOrUsername) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Phone/Username and Password are required." });
            }

            var query = dto.PhoneOrUsername.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == query);
            
            if (user == null)
            {
                // Also search by phone in Customers table
                var custByPhone = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == dto.PhoneOrUsername.Trim());
                if (custByPhone?.UserId != null)
                {
                    user = await _context.Users.FindAsync(custByPhone.UserId);
                }
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid mobile number or password." });
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id || c.Phone == user.Username);
            if (customer == null)
            {
                return Unauthorized(new { message = "No wholesale customer account linked to this user." });
            }

            if (customer.Status != "ACTIVE" || !user.IsApproved)
            {
                return Unauthorized(new { message = "Your wholesale account is pending approval or suspended. Please contact Casa Enterprises." });
            }

            // Generate 6-Digit OTP
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();
            customer.OtpCode = otpCode;
            customer.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _context.SaveChangesAsync();

            // In local/production, OTP is logged / sent via SMS.
            Console.WriteLine($"[CASA OTP SERVICE] >>> OTP for {customer.ShopName} ({customer.Phone}): {otpCode} <<<");

            // Generate temporary session token for OTP verification step
            var tempSessionToken = GenerateTempToken(customer);

            var phone = customer.Phone;
            var maskedPhone = phone.Length >= 6 
                ? phone.Substring(0, 2) + "******" + phone.Substring(phone.Length - 2) 
                : phone;

            return Ok(new CustomerLoginStep1ResponseDto
            {
                RequireOtp = true,
                Message = $"Verification OTP sent to {maskedPhone} (Demo OTP: {otpCode})",
                PhoneMasked = maskedPhone,
                TempSessionToken = tempSessionToken
            });
        }

        [HttpPost("auth/verify-otp")]
        public async Task<ActionResult<CustomerAuthResponseDto>> VerifyOtp(CustomerOtpVerifyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TempSessionToken) || string.IsNullOrWhiteSpace(dto.OtpCode))
            {
                return BadRequest(new { message = "Session token and OTP code are required." });
            }

            var customerId = ValidateTempToken(dto.TempSessionToken);
            if (customerId == null)
            {
                return Unauthorized(new { message = "Invalid or expired login session. Please login again." });
            }

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE")
            {
                return Unauthorized(new { message = "Customer account not found or inactive." });
            }

            if (customer.OtpExpiry == null || customer.OtpExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "OTP has expired. Please request a new OTP." });
            }

            if (customer.OtpCode != dto.OtpCode.Trim())
            {
                return BadRequest(new { message = "Invalid OTP code entered. Please check and retry." });
            }

            // Clear OTP after successful verification
            customer.OtpCode = null;
            customer.OtpExpiry = null;
            await _context.SaveChangesAsync();

            // Generate Full Customer JWT
            var token = GenerateCustomerJwtToken(customer);

            return Ok(new CustomerAuthResponseDto
            {
                Token = token,
                CustomerId = customer.Id,
                ShopName = customer.ShopName,
                OwnerName = customer.OwnerName,
                CustomerType = customer.CustomerType,
                City = customer.City
            });
        }

        [HttpGet("auth/me")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerAuthResponseDto>> GetCurrentProfile()
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null) return NotFound();

            return Ok(new CustomerAuthResponseDto
            {
                Token = string.Empty,
                CustomerId = customer.Id,
                ShopName = customer.ShopName,
                OwnerName = customer.OwnerName,
                CustomerType = customer.CustomerType,
                City = customer.City
            });
        }

        // ==========================================
        // 3. WHOLESALE PRODUCTS CATALOG
        // ==========================================

        [HttpGet("products")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<IEnumerable<CustomerProductDto>>> GetWholesaleProducts()
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE") return Unauthorized();

            var isDistributor = customer.CustomerType.Equals("DISTRIBUTOR", StringComparison.OrdinalIgnoreCase);

            var stockList = await _context.StockSummaries.ToListAsync();
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var result = products.Select(p =>
            {
                var stock = stockList.FirstOrDefault(s => s.ProductId == p.Id)?.CurrentStock ?? 0;
                
                // Determine price strictly on server based on customer tier
                var tierPrice = isDistributor && p.DistributorPrice > 0 
                    ? p.DistributorPrice 
                    : p.SellingPrice;

                return new CustomerProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    DesignBrand = p.DesignBrand,
                    Size = p.Size,
                    Color = p.Color,
                    Price = tierPrice,
                    SetSize = p.SetSize > 0 ? p.SetSize : 4,
                    SetRatio = !string.IsNullOrWhiteSpace(p.SetRatio) ? p.SetRatio : "38, 40, 42, 44",
                    GstPercent = p.GstPercent,
                    AvailableStock = stock,
                    ImageUrl = p.ImageUrl
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("products/{id}")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerProductDto>> GetWholesaleProductById(int id)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE") return Unauthorized();

            var p = await _context.Products.FindAsync(id);
            if (p == null || !p.IsActive) return NotFound();

            var isDistributor = customer.CustomerType.Equals("DISTRIBUTOR", StringComparison.OrdinalIgnoreCase);
            var stockSummary = await _context.StockSummaries.FirstOrDefaultAsync(s => s.ProductId == p.Id);
            var stock = stockSummary?.CurrentStock ?? 0;

            var tierPrice = isDistributor && p.DistributorPrice > 0 
                ? p.DistributorPrice 
                : p.SellingPrice;

            return Ok(new CustomerProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category,
                DesignBrand = p.DesignBrand,
                Size = p.Size,
                Color = p.Color,
                Price = tierPrice,
                SetSize = p.SetSize > 0 ? p.SetSize : 4,
                SetRatio = !string.IsNullOrWhiteSpace(p.SetRatio) ? p.SetRatio : "38, 40, 42, 44",
                GstPercent = p.GstPercent,
                AvailableStock = stock,
                ImageUrl = p.ImageUrl
            });
        }

        // ==========================================
        // 4. WHOLESALE ORDERING & CART CHECKOUT
        // ==========================================

        [HttpPost("orders")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerOrderDto>> PlaceWholesaleOrder(CustomerOrderCreateDto dto)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE") return Unauthorized();

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Order must contain at least one item." });
            }

            var isDistributor = customer.CustomerType.Equals("DISTRIBUTOR", StringComparison.OrdinalIgnoreCase);

            decimal totalTaxable = 0;
            decimal totalGst = 0;
            var orderItems = new List<SalesOrderItem>();

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.Quantity <= 0) continue;

                var product = await _context.Products.FindAsync(itemDto.ProductId);
                if (product == null || !product.IsActive)
                {
                    return BadRequest(new { message = $"Product with ID {itemDto.ProductId} is not available." });
                }

                // Strict inventory validation: verify against live factory stock
                var stockSummary = await _context.StockSummaries.FirstOrDefaultAsync(s => s.ProductId == product.Id);
                var currentStock = stockSummary?.CurrentStock ?? 0;
                var setSize = product.SetSize > 0 ? product.SetSize : 4;
                var maxAvailableSets = setSize > 0 ? (currentStock / setSize) : currentStock;

                if (itemDto.Quantity > currentStock)
                {
                    var requestedSets = setSize > 0 ? (itemDto.Quantity / setSize) : itemDto.Quantity;
                    return BadRequest(new { 
                        message = $"Insufficient stock for '{product.Name}'. Only {maxAvailableSets} Sets ({currentStock} pcs) are available in the warehouse, but {requestedSets} Sets ({itemDto.Quantity} pcs) were requested." 
                    });
                }

                // Strictly evaluate price from database based on customer's tier
                var unitPrice = isDistributor && product.DistributorPrice > 0 
                    ? product.DistributorPrice 
                    : product.SellingPrice;

                var subtotalExclTax = itemDto.Quantity * unitPrice;
                var gstAmount = subtotalExclTax * (product.GstPercent / 100.0m);
                var lineTotal = subtotalExclTax + gstAmount;

                totalTaxable += subtotalExclTax;
                totalGst += gstAmount;

                orderItems.Add(new SalesOrderItem
                {
                    ProductId = product.Id,
                    ItemType = "PCS",
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    GstPercent = product.GstPercent,
                    Discount = 0.00m,
                    SubTotal = lineTotal
                });
            }

            if (orderItems.Count == 0)
            {
                return BadRequest(new { message = "No valid items in the order." });
            }

            var finalAmount = totalTaxable + totalGst;
            var invoiceNo = $"WS-{DateTime.UtcNow:yyyyMMddHHmmss}-{customer.Id}";

            var salesOrder = new SalesOrder
            {
                InvoiceNo = invoiceNo,
                CustomerId = customer.Id,
                CustomerName = $"{customer.ShopName} ({customer.OwnerName})",
                CustomerPhone = customer.Phone,
                OrderStatus = "PENDING",
                OrderSource = "WHOLESALE_PORTAL",
                TotalAmount = totalTaxable,
                TotalGst = totalGst,
                FinalAmount = finalAmount,
                SalesDate = DateTime.UtcNow,
                Items = orderItems
            };

            _context.SalesOrders.Add(salesOrder);
            await _context.SaveChangesAsync();

            // Record stock deductions
            foreach (var item in salesOrder.Items)
            {
                await _inventoryService.RecordStockDeductionAsync(item.ProductId, item.ItemType, item.Quantity, salesOrder.Id);
            }

            return CreatedAtAction(nameof(GetCustomerOrderById), new { id = salesOrder.Id }, MapToCustomerOrderDto(salesOrder));
        }

        // ==========================================
        // 5. RAZORPAY GATEWAY CHECKOUT INTEGRATION
        // ==========================================

        [HttpPost("orders/create-razorpay-order")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<RazorpayCreateOrderResponseDto>> CreateRazorpayOrder(RazorpayCreateOrderRequestDto dto)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE") return Unauthorized();

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Order must contain at least one item." });
            }

            var isDistributor = customer.CustomerType.Equals("DISTRIBUTOR", StringComparison.OrdinalIgnoreCase);

            decimal totalTaxable = 0;
            decimal totalGst = 0;
            var orderItems = new List<SalesOrderItem>();

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.Quantity <= 0) continue;

                var product = await _context.Products.FindAsync(itemDto.ProductId);
                if (product == null || !product.IsActive)
                {
                    return BadRequest(new { message = $"Product with ID {itemDto.ProductId} is not available." });
                }

                var stockSummary = await _context.StockSummaries.FirstOrDefaultAsync(s => s.ProductId == product.Id);
                var currentStock = stockSummary?.CurrentStock ?? 0;
                var setSize = product.SetSize > 0 ? product.SetSize : 4;
                var maxAvailableSets = setSize > 0 ? (currentStock / setSize) : currentStock;

                if (itemDto.Quantity > currentStock)
                {
                    var requestedSets = setSize > 0 ? (itemDto.Quantity / setSize) : itemDto.Quantity;
                    return BadRequest(new { 
                        message = $"Insufficient stock for '{product.Name}'. Only {maxAvailableSets} Sets ({currentStock} pcs) are available, but {requestedSets} Sets were requested." 
                    });
                }

                var unitPrice = isDistributor && product.DistributorPrice > 0 
                    ? product.DistributorPrice 
                    : product.SellingPrice;

                var subtotalExclTax = itemDto.Quantity * unitPrice;
                var gstAmount = subtotalExclTax * (product.GstPercent / 100.0m);
                var lineTotal = subtotalExclTax + gstAmount;

                totalTaxable += subtotalExclTax;
                totalGst += gstAmount;

                orderItems.Add(new SalesOrderItem
                {
                    ProductId = product.Id,
                    ItemType = "PCS",
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    GstPercent = product.GstPercent,
                    Discount = 0.00m,
                    SubTotal = lineTotal
                });
            }

            var finalAmount = totalTaxable + totalGst;
            var invoiceNo = $"WS-{DateTime.UtcNow:yyyyMMddHHmmss}-{customer.Id}";
            var amountInPaise = (long)Math.Round(finalAmount * 100, MidpointRounding.AwayFromZero);

            var keyId = _configuration["Razorpay:KeyId"] ?? Environment.GetEnvironmentVariable("RAZORPAY_KEY_ID") ?? "rzp_live_TumCIxUf4bQr05";
            var keySecret = _configuration["Razorpay:KeySecret"] ?? Environment.GetEnvironmentVariable("RAZORPAY_KEY_SECRET") ?? "eSwGGU0loHzLSxgTuBL5FaZN";

            using var httpClient = new HttpClient();
            var authBytes = Encoding.UTF8.GetBytes($"{keyId}:{keySecret}");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var razorpayPayload = new
            {
                amount = amountInPaise,
                currency = "INR",
                receipt = invoiceNo,
                notes = new Dictionary<string, string>
                {
                    { "source_app", "CASA Wholesale Portal" },
                    { "business", "Casa Enterprises" },
                    { "channel", "Wholesale Portal" },
                    { "customer_name", $"{customer.ShopName} ({customer.OwnerName})" },
                    { "customer_phone", customer.Phone ?? "" },
                    { "invoice_no", invoiceNo },
                    { "order_notes", dto.Notes ?? "" }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(razorpayPayload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://api.razorpay.com/v1/orders", jsonContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { message = $"Razorpay order creation failed: {responseBody}" });
            }

            using var doc = JsonDocument.Parse(responseBody);
            var razorpayOrderId = doc.RootElement.GetProperty("id").GetString() ?? "";

            // Pre-create SalesOrder as PENDING (will be confirmed via Frontend callback OR Razorpay Webhook)
            var salesOrder = new SalesOrder
            {
                InvoiceNo = invoiceNo,
                CustomerId = customer.Id,
                CustomerName = $"{customer.ShopName} ({customer.OwnerName})",
                CustomerPhone = customer.Phone,
                OrderStatus = "PENDING",
                OrderSource = "WHOLESALE_PORTAL",
                TotalAmount = totalTaxable,
                TotalGst = totalGst,
                FinalAmount = finalAmount,
                SalesDate = DateTime.UtcNow,
                PaymentProofUrl = $"RZP_ORDER:{razorpayOrderId}",
                Items = orderItems
            };

            _context.SalesOrders.Add(salesOrder);
            await _context.SaveChangesAsync();

            return Ok(new RazorpayCreateOrderResponseDto
            {
                RazorpayOrderId = razorpayOrderId,
                Amount = amountInPaise,
                AmountRupees = finalAmount,
                Currency = "INR",
                KeyId = keyId,
                InvoiceNo = invoiceNo,
                BusinessName = "CASA ENTERPRISES",
                CustomerName = customer.OwnerName,
                CustomerPhone = customer.Phone ?? "",
                CustomerEmail = customer.Email ?? ""
            });
        }

        [HttpPost("orders/verify-razorpay-payment")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerOrderDto>> VerifyRazorpayPayment(RazorpayVerifyPaymentDto dto)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (customer == null || customer.Status != "ACTIVE") return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.RazorpayOrderId) || 
                string.IsNullOrWhiteSpace(dto.RazorpayPaymentId) || 
                string.IsNullOrWhiteSpace(dto.RazorpaySignature))
            {
                return BadRequest(new { message = "Incomplete Razorpay payment response payload." });
            }

            var keySecret = _configuration["Razorpay:KeySecret"] ?? Environment.GetEnvironmentVariable("RAZORPAY_KEY_SECRET") ?? "eSwGGU0loHzLSxgTuBL5FaZN";

            var payload = $"{dto.RazorpayOrderId}|{dto.RazorpayPaymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (!string.Equals(computedSignature, dto.RazorpaySignature, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Payment signature verification failed. Untrusted payment transaction." });
            }

            // Check if order was already confirmed (e.g. by Webhook)
            var existingOrder = await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(so => so.PaymentProofUrl == $"RZP_ORDER:{dto.RazorpayOrderId}" || 
                                           so.PaymentProofUrl == $"RAZORPAY_PAID:{dto.RazorpayOrderId}");

            if (existingOrder != null)
            {
                if (existingOrder.OrderStatus != "CONFIRMED")
                {
                    existingOrder.OrderStatus = "CONFIRMED";
                    existingOrder.TransactionId = dto.RazorpayPaymentId;
                    existingOrder.PaymentProofUrl = $"RAZORPAY_PAID:{dto.RazorpayOrderId}";
                    await _context.SaveChangesAsync();

                    foreach (var item in existingOrder.Items)
                    {
                        await _inventoryService.RecordStockDeductionAsync(item.ProductId, item.ItemType, item.Quantity, existingOrder.Id);
                    }
                }
                return Ok(MapToCustomerOrderDto(existingOrder));
            }

            // Fallback: If for any reason pre-created order wasn't found, create and confirm now
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "Order must contain at least one item." });
            }

            var isDistributor = customer.CustomerType.Equals("DISTRIBUTOR", StringComparison.OrdinalIgnoreCase);
            decimal fallbackTaxable = 0;
            decimal fallbackGst = 0;
            var fallbackItems = new List<SalesOrderItem>();

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.Quantity <= 0) continue;
                var product = await _context.Products.FindAsync(itemDto.ProductId);
                if (product == null || !product.IsActive) continue;

                var unitPrice = isDistributor && product.DistributorPrice > 0 ? product.DistributorPrice : product.SellingPrice;
                var subtotalExclTax = itemDto.Quantity * unitPrice;
                var gstAmount = subtotalExclTax * (product.GstPercent / 100.0m);
                var lineTotal = subtotalExclTax + gstAmount;

                fallbackTaxable += subtotalExclTax;
                fallbackGst += gstAmount;

                fallbackItems.Add(new SalesOrderItem
                {
                    ProductId = product.Id,
                    ItemType = "PCS",
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    GstPercent = product.GstPercent,
                    Discount = 0.00m,
                    SubTotal = lineTotal
                });
            }

            var fallbackFinal = fallbackTaxable + fallbackGst;
            var invoiceNo = $"WS-{DateTime.UtcNow:yyyyMMddHHmmss}-{customer.Id}";

            var newOrder = new SalesOrder
            {
                InvoiceNo = invoiceNo,
                CustomerId = customer.Id,
                CustomerName = $"{customer.ShopName} ({customer.OwnerName})",
                CustomerPhone = customer.Phone,
                OrderStatus = "CONFIRMED",
                OrderSource = "WHOLESALE_PORTAL",
                TotalAmount = fallbackTaxable,
                TotalGst = fallbackGst,
                FinalAmount = fallbackFinal,
                SalesDate = DateTime.UtcNow,
                TransactionId = dto.RazorpayPaymentId,
                PaymentProofUrl = $"RAZORPAY_PAID:{dto.RazorpayOrderId}",
                Items = fallbackItems
            };

            _context.SalesOrders.Add(newOrder);
            await _context.SaveChangesAsync();

            foreach (var item in newOrder.Items)
            {
                await _inventoryService.RecordStockDeductionAsync(item.ProductId, item.ItemType, item.Quantity, newOrder.Id);
            }

            return Ok(MapToCustomerOrderDto(newOrder));
        }

        // ==========================================
        // 6. RAZORPAY WEBHOOK (BACKGROUND FULFILLMENT)
        // ==========================================

        [HttpPost("orders/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> RazorpayWebhook()
        {
            try
            {
                string rawBody;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    rawBody = await reader.ReadToEndAsync();
                }

                var webhookSecret = _configuration["Razorpay:WebhookSecret"] 
                    ?? Environment.GetEnvironmentVariable("RAZORPAY_WEBHOOK_SECRET") 
                    ?? "casa_webhook_sec_99a8b7c6d5e4";

                // 1. Verify Webhook Signature if signature header is provided
                if (Request.Headers.TryGetValue("X-Razorpay-Signature", out var signatureHeader))
                {
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
                    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
                    var computedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

                    if (!string.Equals(computedSignature, signatureHeader.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[Razorpay Webhook ERROR] Invalid Webhook Signature.");
                        return BadRequest(new { error = "Invalid webhook signature" });
                    }
                }

                // 2. Parse Webhook Event JSON
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : "";

                Console.WriteLine($"[Razorpay Webhook] Received Event: {eventType}");

                if (!root.TryGetProperty("payload", out var payload))
                {
                    return Ok(new { message = "No payload present" });
                }

                JsonElement paymentEntity = default;
                if (payload.TryGetProperty("payment", out var p) && p.TryGetProperty("entity", out var pe))
                {
                    paymentEntity = pe;
                }

                JsonElement orderEntity = default;
                if (payload.TryGetProperty("order", out var o) && o.TryGetProperty("entity", out var oe))
                {
                    orderEntity = oe;
                }

                string? sourceApp = null;
                string? business = null;
                string? invoiceNo = null;
                string? razorpayOrderId = null;
                string? razorpayPaymentId = null;

                if (paymentEntity.ValueKind != JsonValueKind.Undefined)
                {
                    if (paymentEntity.TryGetProperty("id", out var pid)) razorpayPaymentId = pid.GetString();
                    if (paymentEntity.TryGetProperty("order_id", out var oid)) razorpayOrderId = oid.GetString();
                    if (paymentEntity.TryGetProperty("notes", out var notes))
                    {
                        if (notes.TryGetProperty("source_app", out var sa)) sourceApp = sa.GetString();
                        if (notes.TryGetProperty("business", out var b)) business = b.GetString();
                        if (notes.TryGetProperty("invoice_no", out var inv)) invoiceNo = inv.GetString();
                    }
                }

                if (orderEntity.ValueKind != JsonValueKind.Undefined)
                {
                    if (razorpayOrderId == null && orderEntity.TryGetProperty("id", out var oid)) razorpayOrderId = oid.GetString();
                    if (orderEntity.TryGetProperty("notes", out var notes))
                    {
                        if (sourceApp == null && notes.TryGetProperty("source_app", out var sa)) sourceApp = sa.GetString();
                        if (business == null && notes.TryGetProperty("business", out var b)) business = b.GetString();
                        if (invoiceNo == null && notes.TryGetProperty("invoice_no", out var inv)) invoiceNo = inv.GetString();
                    }
                }

                // =====================================================================
                // 3. MULTI-APP ISOLATION FILTER:
                // Safely ignore payments belonging to InterviewDesk, Billing_ERP, etc.
                // =====================================================================
                var isCasaPayment = string.Equals(business, "Casa Enterprises", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(sourceApp, "CASA Wholesale Portal", StringComparison.OrdinalIgnoreCase);

                if (!isCasaPayment)
                {
                    Console.WriteLine($"[Razorpay Webhook] Ignored payment '{razorpayPaymentId}': belongs to other application ({sourceApp ?? "External App / InterviewDesk"}).");
                    return Ok(new { status = "ignored", reason = "belongs to another application" });
                }

                // =====================================================================
                // 4. PROCESS CASA ENTERPRISES WHOLESALE PAYMENT
                // =====================================================================
                var salesOrder = await _context.SalesOrders
                    .Include(so => so.Items)
                    .FirstOrDefaultAsync(so => 
                        (!string.IsNullOrEmpty(razorpayOrderId) && so.PaymentProofUrl == $"RZP_ORDER:{razorpayOrderId}") ||
                        (!string.IsNullOrEmpty(invoiceNo) && so.InvoiceNo == invoiceNo) ||
                        (!string.IsNullOrEmpty(razorpayPaymentId) && so.TransactionId == razorpayPaymentId));

                if (salesOrder == null)
                {
                    Console.WriteLine($"[Razorpay Webhook] Order not found for invoice '{invoiceNo}' or order '{razorpayOrderId}'.");
                    return Ok(new { status = "ignored", reason = "order record not found" });
                }

                if (salesOrder.OrderStatus == "CONFIRMED")
                {
                    Console.WriteLine($"[Razorpay Webhook] Order #{salesOrder.InvoiceNo} already confirmed. No action needed.");
                    return Ok(new { status = "already_confirmed", invoiceNo = salesOrder.InvoiceNo });
                }

                // Confirm order and deduct stock in the background!
                salesOrder.OrderStatus = "CONFIRMED";
                salesOrder.TransactionId = razorpayPaymentId ?? salesOrder.TransactionId;
                salesOrder.PaymentProofUrl = $"RAZORPAY_PAID:{razorpayOrderId}";
                await _context.SaveChangesAsync();

                foreach (var item in salesOrder.Items)
                {
                    await _inventoryService.RecordStockDeductionAsync(item.ProductId, item.ItemType, item.Quantity, salesOrder.Id);
                }

                Console.WriteLine($"[Razorpay Webhook SUCCESS] Confirmed Wholesale Order #{salesOrder.InvoiceNo} via background webhook!");
                return Ok(new { status = "success", invoiceNo = salesOrder.InvoiceNo });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razorpay Webhook Exception] {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("orders")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<IEnumerable<CustomerOrderDto>>> GetCustomerOrders()
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var orders = await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .Where(so => so.CustomerId == customerId.Value)
                .OrderByDescending(so => so.SalesDate)
                .ToListAsync();

            var dtos = orders.Select(MapToCustomerOrderDto).ToList();
            return Ok(dtos);
        }

        [HttpGet("orders/{id}")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerOrderDto>> GetCustomerOrderById(int id)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var order = await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(so => so.Id == id && so.CustomerId == customerId.Value);

            if (order == null)
            {
                return NotFound(new { message = "Order not found or you do not have permission to view it." });
            }

            return Ok(MapToCustomerOrderDto(order));
        }

        [HttpPost("orders/{id}/reorder")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult<CustomerOrderCreateDto>> ReorderPreviousOrder(int id)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return Unauthorized();

            var previousOrder = await _context.SalesOrders
                .Include(so => so.Items)
                .FirstOrDefaultAsync(so => so.Id == id && so.CustomerId == customerId.Value);

            if (previousOrder == null)
            {
                return NotFound(new { message = "Previous order not found." });
            }

            var reorderCart = new CustomerOrderCreateDto
            {
                Notes = $"Reorder from #{previousOrder.InvoiceNo}",
                Items = previousOrder.Items.Select(i => new CustomerOrderItemCreateDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            return Ok(reorderCart);
        }


        // ==========================================
        // 6. PAYMENT PROOF UPLOAD
        // ==========================================

        [HttpPost("upload-proof")]
        [Authorize(Roles = "CUSTOMER")]
        public async Task<ActionResult> UploadPaymentProof([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            // Ensure directory exists
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proofs");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"proof_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/proofs/{fileName}";
            return Ok(new { url });
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================

        private int? GetCurrentCustomerId()
        {
            var claim = User.FindFirst("CustomerId");
            if (claim != null && int.TryParse(claim.Value, out var cid))
            {
                return cid;
            }
            return null;
        }

        private CustomerOrderDto MapToCustomerOrderDto(SalesOrder so)
        {
            return new CustomerOrderDto
            {
                Id = so.Id,
                InvoiceNo = so.InvoiceNo,
                OrderStatus = so.OrderStatus,
                OrderSource = so.OrderSource,
                TotalAmount = so.TotalAmount,
                TotalGst = so.TotalGst,
                FinalAmount = so.FinalAmount,
                SalesDate = so.SalesDate,
                Items = so.Items.Select(i => {
                    var setSize = i.Product?.SetSize > 0 ? i.Product.SetSize : 4;
                    return new CustomerOrderItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.Name ?? $"Item #{i.ProductId}",
                        Category = i.Product?.Category ?? "Apparel",
                        Size = i.Product?.Size ?? "",
                        Color = i.Product?.Color ?? "",
                        DesignBrand = i.Product?.DesignBrand,
                        SetSize = setSize,
                        SetRatio = i.Product?.SetRatio ?? "38, 40, 42, 44",
                        SetsCount = Math.Max(1, i.Quantity / setSize),
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        GstPercent = i.GstPercent,
                        SubTotal = i.SubTotal
                    };
                }).ToList()
            };
        }

        private string GenerateTempToken(Customer customer)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForApparelERPSystem_2026_SecureKey_MustBeLongEnough";
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("CustomerId", customer.Id.ToString()),
                    new Claim("Type", "OTP_TEMP_SESSION")
                }),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private int? ValidateTempToken(string token)
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForApparelERPSystem_2026_SecureKey_MustBeLongEnough";
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtKey);

                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParams, out _);
                var typeClaim = principal.FindFirst("Type");
                if (typeClaim?.Value != "OTP_TEMP_SESSION") return null;

                var customerIdClaim = principal.FindFirst("CustomerId");
                if (customerIdClaim != null && int.TryParse(customerIdClaim.Value, out var cid))
                {
                    return cid;
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private string GenerateCustomerJwtToken(Customer customer)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForApparelERPSystem_2026_SecureKey_MustBeLongEnough";
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, customer.UserId?.ToString() ?? customer.Id.ToString()),
                new Claim(ClaimTypes.Name, customer.Phone),
                new Claim(ClaimTypes.Role, "CUSTOMER"),
                new Claim("CustomerId", customer.Id.ToString()),
                new Claim("CustomerType", customer.CustomerType),
                new Claim("ShopName", customer.ShopName)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(14), // 2-week active wholesale session
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
