using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Services
{
    public interface ICostingService
    {
        Task<ProductionBatch> StartProductionBatchAsync(StartProductionBatchDto dto);
        Task<ProductionBatch> CompleteProductionBatchAsync(CompleteProductionBatchDto dto);
        Task<TradingPurchase> RecordTradingPurchaseAsync(CreateTradingPurchaseDto dto);
    }

    public class CostingService : ICostingService
    {
        private readonly ApparelDbContext _context;

        public CostingService(ApparelDbContext context)
        {
            _context = context;
        }

        public async Task<ProductionBatch> StartProductionBatchAsync(StartProductionBatchDto dto)
        {
            var fabric = await _context.Fabrics.FindAsync(dto.FabricId) 
                ?? throw new ArgumentException("Fabric not found");

            int? productId = dto.ProductId;

            if (!productId.HasValue && !string.IsNullOrWhiteSpace(dto.DesignName))
            {
                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == dto.DesignName.Trim().ToLower() && p.ProductType == "MANUFACTURED");

                if (existingProduct != null)
                {
                    productId = existingProduct.Id;
                }
                else
                {
                    var newProduct = new Product
                    {
                        Name = dto.DesignName.Trim(),
                        Category = "Uncategorized",
                        ProductType = "MANUFACTURED",
                        DesignBrand = "Generic",
                        Size = "Free",
                        Color = "Standard",
                        CostPrice = 0.00m,
                        SellingPrice = 0.00m,
                        GstPercent = 12.00m
                    };
                    _context.Products.Add(newProduct);
                    await _context.SaveChangesAsync();
                    productId = newProduct.Id;
                }
            }

            if (productId.HasValue)
            {
                var product = await _context.Products.FindAsync(productId.Value)
                    ?? throw new ArgumentException("Product not found");

                if (product.ProductType != "MANUFACTURED")
                {
                    throw new InvalidOperationException("Cannot start production batch for a non-manufactured product.");
                }
            }

            decimal totalMetersRequired = dto.FabricMetersUsed + dto.WastageMeters;
            if ((fabric.TotalMeters - fabric.UsedMeters) < totalMetersRequired)
            {
                throw new InvalidOperationException($"Insufficient fabric. Available: {fabric.TotalMeters - fabric.UsedMeters}m, Required: {totalMetersRequired}m");
            }

            // Deduct fabric meters (used + wasted)
            fabric.UsedMeters += totalMetersRequired;

            // Auto-generate a unique batch code to prevent duplicate key violations
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var batchCode = string.IsNullOrWhiteSpace(dto.BatchCode)
                ? $"BATCH-{DateTime.UtcNow:yyyyMMdd}-{uniqueSuffix}"
                : $"{dto.BatchCode}-{uniqueSuffix}";

            var batch = new ProductionBatch
            {
                BatchCode = batchCode,
                FabricId = dto.FabricId,
                FabricMetersUsed = dto.FabricMetersUsed,
                WastageMeters = dto.WastageMeters,
                ProductId = productId,
                DesignName = dto.DesignName,
                QuantityToSew = dto.QuantityToSew,
                Status = "IN_PRODUCTION",
                DateCreated = DateTime.UtcNow
            };

            _context.ProductionBatches.Add(batch);
            await _context.SaveChangesAsync();

            return batch;
        }

        public async Task<ProductionBatch> CompleteProductionBatchAsync(CompleteProductionBatchDto dto)
        {
            var batch = await _context.ProductionBatches
                .Include(b => b.Fabric)
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == dto.BatchId)
                ?? throw new ArgumentException("Production batch not found");

            if (batch.Status == "COMPLETED")
            {
                throw new InvalidOperationException("Production batch is already completed.");
            }

            if (dto.QuantityProduced <= 0)
            {
                throw new ArgumentException("Quantity produced must be greater than zero.");
            }

            // Calculate costs
            decimal fabricCost = batch.FabricMetersUsed * (batch.Fabric?.CostPerMeter ?? 0);
            decimal totalCost = fabricCost + dto.TailoringCost + dto.AdditionalCost;
            decimal costPerPiece = totalCost / dto.QuantityProduced;

            // Update batch details
            batch.TailoringCost = dto.TailoringCost;
            batch.AdditionalCost = dto.AdditionalCost;
            batch.QuantityProduced = dto.QuantityProduced;
            batch.CostPerPiece = costPerPiece;
            batch.Status = "COMPLETED";
            batch.DateCompleted = DateTime.UtcNow;

            // Resolve Product dynamically at completion time if missing!
            if (!batch.ProductId.HasValue)
            {
                string productName = !string.IsNullOrWhiteSpace(batch.DesignName)
                    ? batch.DesignName.Trim()
                    : $"Stitched {batch.Fabric?.Name ?? "Fabric"} ({batch.Fabric?.Color ?? "Standard"})";

                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == productName.ToLower() && p.ProductType == "MANUFACTURED");

                if (existingProduct != null)
                {
                    batch.ProductId = existingProduct.Id;
                    batch.Product = existingProduct;
                }
                else
                {
                    var newProduct = new Product
                    {
                        Name = productName,
                        Category = "Uncategorized",
                        ProductType = "MANUFACTURED",
                        DesignBrand = "Generic",
                        Size = "Free",
                        Color = batch.Fabric?.Color ?? "Standard",
                        CostPrice = costPerPiece,
                        SellingPrice = 0.00m,
                        GstPercent = 12.00m
                    };
                    _context.Products.Add(newProduct);
                    await _context.SaveChangesAsync();
                    batch.ProductId = newProduct.Id;
                    batch.Product = newProduct;
                }
            }

            // Update Product Cost Price
            if (batch.Product != null)
            {
                batch.Product.CostPrice = costPerPiece;
            }

            // Add stock entry to stock_ledger
            var stockLedger = new StockLedger
            {
                ProductId = batch.ProductId.Value,
                QuantityChange = dto.QuantityProduced,
                TransactionType = "MANUFACTURED_IN",
                ReferenceId = batch.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockLedgerEntries.Add(stockLedger);

            await _context.SaveChangesAsync();
            return batch;
        }

        public async Task<TradingPurchase> RecordTradingPurchaseAsync(CreateTradingPurchaseDto dto)
        {
            var product = await _context.Products.FindAsync(dto.ProductId)
                ?? throw new ArgumentException("Product not found");

            if (product.ProductType != "TRADED")
            {
                throw new InvalidOperationException("Cannot record trading purchase for a manufactured product.");
            }

            var supplier = await _context.Suppliers.FindAsync(dto.SupplierId)
                ?? throw new ArgumentException("Supplier not found");

            // Calculate Total Cost
            decimal subTotal = dto.Quantity * dto.PurchasePrice;
            decimal gstAmount = subTotal * (dto.GstPercent / 100.0m);
            decimal totalCost = subTotal + gstAmount;

            var purchase = new TradingPurchase
            {
                InvoiceNo = dto.InvoiceNo,
                SupplierId = dto.SupplierId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                PurchasePrice = dto.PurchasePrice,
                GstPercent = dto.GstPercent,
                TotalCost = totalCost,
                PurchaseDate = DateTime.UtcNow
            };

            // Update Product Cost Price (Latest purchase price is stored)
            product.CostPrice = dto.PurchasePrice;

            _context.TradingPurchases.Add(purchase);
            await _context.SaveChangesAsync(); // Saves purchase to generate Id

            // Add stock ledger transaction
            var stockLedger = new StockLedger
            {
                ProductId = dto.ProductId,
                QuantityChange = dto.Quantity,
                TransactionType = "TRADING_IN",
                ReferenceId = purchase.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockLedgerEntries.Add(stockLedger);

            await _context.SaveChangesAsync();
            return purchase;
        }
    }
}
