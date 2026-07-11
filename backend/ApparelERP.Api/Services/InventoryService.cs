using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Services
{
    public interface IInventoryService
    {
        Task<List<StockSummaryDto>> GetStockSummaryAsync();
        Task<int> GetProductStockAsync(int productId);
        Task<bool> VerifyStockAvailabilityAsync(int productId, string itemType, int saleQuantity);
        Task RecordStockDeductionAsync(int productId, string itemType, int saleQuantity, int salesOrderId);
        Task ClearAllStockAsync();
    }

    public class InventoryService : IInventoryService
    {
        private readonly ApparelDbContext _context;

        public InventoryService(ApparelDbContext context)
        {
            _context = context;
        }

        public async Task<List<StockSummaryDto>> GetStockSummaryAsync()
        {
            return await _context.StockSummaries.ToListAsync();
        }

        public async Task ClearAllStockAsync()
        {
            _context.StockLedgerEntries.RemoveRange(_context.StockLedgerEntries);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetProductStockAsync(int productId)
        {
            var stockSummary = await _context.StockSummaries
                .FirstOrDefaultAsync(s => s.ProductId == productId);
            return stockSummary?.CurrentStock ?? 0;
        }

        public async Task<bool> VerifyStockAvailabilityAsync(int productId, string itemType, int saleQuantity)
        {
            int requiredPieces = CalculateRequiredPieces(itemType, saleQuantity);
            int availablePieces = await GetProductStockAsync(productId);
            return availablePieces >= requiredPieces;
        }

        public async Task RecordStockDeductionAsync(int productId, string itemType, int saleQuantity, int salesOrderId)
        {
            int requiredPieces = CalculateRequiredPieces(itemType, saleQuantity);

            var ledgerEntry = new Models.StockLedger
            {
                ProductId = productId,
                QuantityChange = -requiredPieces, // Negative to represent deduction
                TransactionType = "SALE_OUT",
                ReferenceId = salesOrderId,
                CreatedAt = DateTime.UtcNow
            };

            _context.StockLedgerEntries.Add(ledgerEntry);
            await _context.SaveChangesAsync();
        }

        private int CalculateRequiredPieces(string itemType, int quantity)
        {
            if (itemType.Equals("SET", StringComparison.OrdinalIgnoreCase))
            {
                // In clothing/apparel retail/wholesale, a set represents multiple shirts
                // (e.g., standard pack of 4: S, M, L, XL or standard combo packs).
                // We'll define a SET as 4 pieces.
                return quantity * 4;
            }
            return quantity; // PCS = 1 piece
        }
    }
}
