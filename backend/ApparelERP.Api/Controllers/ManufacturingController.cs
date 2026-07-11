using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    public class ManufacturingController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly ICostingService _costingService;

        public ManufacturingController(ApparelDbContext context, ICostingService costingService)
        {
            _context = context;
            _costingService = costingService;
        }

        // ==========================================
        // FABRICS MANAGEMENT
        // ==========================================

        [HttpGet("fabrics")]
        public async Task<ActionResult<IEnumerable<Fabric>>> GetFabrics()
        {
            return await _context.Fabrics
                .Include(f => f.Supplier)
                .Where(f => !f.IsDeleted && (f.TotalMeters - f.UsedMeters) >= 1.00m)
                .ToListAsync();
        }

        [HttpPost("fabrics")]
        public async Task<ActionResult<Fabric>> CreateFabric(Fabric fabric)
        {
            fabric.CreatedAt = DateTime.UtcNow;
            _context.Fabrics.Add(fabric);
            await _context.SaveChangesAsync();

            return Ok(fabric);
        }

        [HttpDelete("fabrics/{id}")]
        public async Task<IActionResult> DeleteFabric(int id)
        {
            var fabric = await _context.Fabrics.FindAsync(id);
            if (fabric == null)
            {
                return NotFound();
            }

            // If this fabric has been used in production batches, soft-delete it to preserve history
            var hasBeenUsed = await _context.ProductionBatches.AnyAsync(b => b.FabricId == id);
            if (hasBeenUsed)
            {
                fabric.IsDeleted = true;
            }
            else
            {
                // Never used in any batch, safe to hard-delete completely
                _context.Fabrics.Remove(fabric);
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // PRODUCTION BATCHES
        // ==========================================

        [HttpGet("batches")]
        public async Task<ActionResult<IEnumerable<ProductionBatch>>> GetBatches()
        {
            return await _context.ProductionBatches
                .Include(b => b.Fabric)
                .Include(b => b.Product)
                .OrderByDescending(b => b.DateCreated)
                .ToListAsync();
        }

        [HttpGet("batches/{id}")]
        public async Task<ActionResult<ProductionBatch>> GetBatch(int id)
        {
            var batch = await _context.ProductionBatches
                .Include(b => b.Fabric)
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
            {
                return NotFound();
            }

            return batch;
        }

        [HttpPost("batches/start")]
        public async Task<ActionResult<ProductionBatch>> StartBatch(StartProductionBatchDto dto)
        {
            try
            {
                var batch = await _costingService.StartProductionBatchAsync(dto);
                return CreatedAtAction(nameof(GetBatch), new { id = batch.Id }, batch);
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

        [HttpPost("batches/complete")]
        public async Task<ActionResult<ProductionBatch>> CompleteBatch(CompleteProductionBatchDto dto)
        {
            try
            {
                var batch = await _costingService.CompleteProductionBatchAsync(dto);
                return Ok(batch);
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

        [HttpDelete("batches/{id}")]
        public async Task<IActionResult> DeleteBatch(int id)
        {
            var batch = await _context.ProductionBatches.FindAsync(id);
            if (batch == null)
            {
                return NotFound();
            }

            // Restore fabric meters used by this batch
            var fabric = await _context.Fabrics.FindAsync(batch.FabricId);
            if (fabric != null)
            {
                fabric.UsedMeters -= (batch.FabricMetersUsed + batch.WastageMeters);
                if (fabric.UsedMeters < 0) fabric.UsedMeters = 0; // Safeguard
            }

            // Remove associated stock ledger entries (so stock is reverted)
            var ledgerEntries = await _context.StockLedgerEntries
                .Where(sl => sl.TransactionType == "MANUFACTURED_IN" && sl.ReferenceId == id)
                .ToListAsync();
            _context.StockLedgerEntries.RemoveRange(ledgerEntries);

            // Remove the batch itself
            _context.ProductionBatches.Remove(batch);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
