using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/admin/customers")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public class CustomerAdminController : ControllerBase
    {
        private readonly ApparelDbContext _context;

        public CustomerAdminController(ApparelDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AdminCustomerSummaryDto>>> GetAllCustomers()
        {
            var customers = await _context.Customers
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var orderStats = await _context.SalesOrders
                .Where(so => so.CustomerId != null)
                .GroupBy(so => so.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(so => so.FinalAmount)
                })
                .ToListAsync();

            var dtos = customers.Select(c =>
            {
                var stat = orderStats.FirstOrDefault(s => s.CustomerId == c.Id);
                return new AdminCustomerSummaryDto
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    ShopName = c.ShopName,
                    OwnerName = c.OwnerName,
                    Phone = c.Phone,
                    Whatsapp = c.Whatsapp,
                    Email = c.Email,
                    Address = c.Address,
                    City = c.City,
                    GstNumber = c.GstNumber,
                    CustomerType = c.CustomerType,
                    Status = c.Status,
                    Notes = c.Notes,
                    CreatedAt = c.CreatedAt,
                    ApprovedAt = c.ApprovedAt,
                    TotalOrdersPlaced = stat?.OrderCount ?? 0,
                    TotalSpent = stat?.TotalSpent ?? 0.00m
                };
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AdminCustomerSummaryDto>> GetCustomerById(int id)
        {
            var c = await _context.Customers.FindAsync(id);
            if (c == null) return NotFound();

            var orderStat = await _context.SalesOrders
                .Where(so => so.CustomerId == id)
                .GroupBy(so => so.CustomerId!.Value)
                .Select(g => new
                {
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(so => so.FinalAmount)
                })
                .FirstOrDefaultAsync();

            return Ok(new AdminCustomerSummaryDto
            {
                Id = c.Id,
                UserId = c.UserId,
                ShopName = c.ShopName,
                OwnerName = c.OwnerName,
                Phone = c.Phone,
                Whatsapp = c.Whatsapp,
                Email = c.Email,
                Address = c.Address,
                City = c.City,
                GstNumber = c.GstNumber,
                CustomerType = c.CustomerType,
                Status = c.Status,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                ApprovedAt = c.ApprovedAt,
                TotalOrdersPlaced = orderStat?.OrderCount ?? 0,
                TotalSpent = orderStat?.TotalSpent ?? 0.00m
            });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateCustomerStatus(int id, UpdateCustomerStatusDto dto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = "Customer account not found." });

            var oldStatus = customer.Status;
            customer.Status = dto.Status.ToUpper();
            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                customer.Notes = dto.Notes;
            }

            if (customer.Status == "ACTIVE" && oldStatus != "ACTIVE")
            {
                customer.ApprovedAt = DateTime.UtcNow;
                if (customer.UserId != null)
                {
                    var user = await _context.Users.FindAsync(customer.UserId);
                    if (user != null)
                    {
                        user.IsApproved = true;
                    }
                }
            }
            else if (customer.Status == "SUSPENDED" || customer.Status == "REJECTED")
            {
                if (customer.UserId != null)
                {
                    var user = await _context.Users.FindAsync(customer.UserId);
                    if (user != null)
                    {
                        user.IsApproved = false;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Customer status updated to {customer.Status} successfully.", customerId = customer.Id });
        }

        [HttpPut("{id}/type")]
        public async Task<IActionResult> UpdateCustomerType(int id, UpdateCustomerTypeDto dto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = "Customer account not found." });

            var newType = dto.CustomerType.ToUpper();
            if (newType != "REGULAR" && newType != "DISTRIBUTOR")
            {
                return BadRequest(new { message = "Customer type must be either 'REGULAR' or 'DISTRIBUTOR'." });
            }

            customer.CustomerType = newType;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Pricing group set to {customer.CustomerType} successfully.", customerId = customer.Id });
        }

        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<SalesOrder>>> GetPortalWholesaleOrders()
        {
            var orders = await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .Include(so => so.Customer)
                .Where(so => so.OrderSource == "WHOLESALE_PORTAL" || so.CustomerId != null)
                .OrderByDescending(so => so.SalesDate)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, UpdateOrderStatusDto dto)
        {
            var order = await _context.SalesOrders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            order.OrderStatus = dto.OrderStatus.ToUpper();
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Order status updated to {order.OrderStatus} successfully.", orderId = order.Id });
        }
    }
}
