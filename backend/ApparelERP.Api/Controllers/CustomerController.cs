using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly IConfiguration _configuration;

        public CustomerController(ApparelDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
                    AvailableSets = availableSets
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

            return CreatedAtAction(nameof(GetCustomerOrderById), new { id = salesOrder.Id }, MapToCustomerOrderDto(salesOrder));
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
        public async Task<ActionResult> UploadPaymentProof(IFormFile proofFile)
        {
            if (proofFile == null || proofFile.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            // Ensure directory exists
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proofs");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"proof_{DateTime.UtcNow.Ticks}{Path.GetExtension(proofFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await proofFile.CopyToAsync(stream);
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
