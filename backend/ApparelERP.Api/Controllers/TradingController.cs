using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;
using ApparelERP.Api.Services;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,EMPLOYEE,CA")]
    public class TradingController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly ICostingService _costingService;

        public TradingController(ApparelDbContext context, ICostingService costingService)
        {
            _context = context;
            _costingService = costingService;
        }

        // ==========================================
        // SUPPLIERS
        // ==========================================

        [HttpGet("suppliers")]
        public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
        {
            return await _context.Suppliers.ToListAsync();
        }

        [HttpPost("suppliers")]
        public async Task<ActionResult<Supplier>> CreateSupplier(Supplier supplier)
        {
            supplier.CreatedAt = DateTime.UtcNow;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return Ok(supplier);
        }

        // ==========================================
        // TRADING PURCHASES
        // ==========================================

        [HttpGet("purchases")]
        public async Task<ActionResult<IEnumerable<TradingPurchase>>> GetPurchases()
        {
            return await _context.TradingPurchases
                .Include(p => p.Supplier)
                .Include(p => p.Product)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();
        }

        [HttpPost("purchases")]
        public async Task<ActionResult<TradingPurchase>> RecordPurchase(CreateTradingPurchaseDto dto)
        {
            try
            {
                var purchase = await _costingService.RecordTradingPurchaseAsync(dto);
                return Ok(purchase);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("suppliers/{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            // Set SupplierId to null on associated Fabrics
            var fabrics = await _context.Fabrics.Where(f => f.SupplierId == id).ToListAsync();
            foreach (var fabric in fabrics)
            {
                fabric.SupplierId = null;
            }

            // Find all trading purchases for the supplier
            var purchases = await _context.TradingPurchases.Where(p => p.SupplierId == id).ToListAsync();
            foreach (var purchase in purchases)
            {
                // Delete stock ledger entries created by the purchase
                var ledgerEntries = await _context.StockLedgerEntries
                    .Where(sl => sl.TransactionType == "TRADING_IN" && sl.ReferenceId == purchase.Id)
                    .ToListAsync();
                _context.StockLedgerEntries.RemoveRange(ledgerEntries);
            }

            // Delete the purchases
            _context.TradingPurchases.RemoveRange(purchases);

            // Delete the supplier
            _context.Suppliers.Remove(supplier);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("purchases/{id}")]
        public async Task<IActionResult> DeletePurchase(int id)
        {
            var purchase = await _context.TradingPurchases.FindAsync(id);
            if (purchase == null)
            {
                return NotFound();
            }

            // Remove associated stock ledger entries (so stock is reverted)
            var ledgerEntries = await _context.StockLedgerEntries
                .Where(sl => sl.TransactionType == "TRADING_IN" && sl.ReferenceId == id)
                .ToListAsync();
            _context.StockLedgerEntries.RemoveRange(ledgerEntries);

            // Remove the purchase
            _context.TradingPurchases.Remove(purchase);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
