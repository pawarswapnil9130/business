using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Services
{
    public interface ISalesService
    {
        Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto);
        Task<List<SalesOrder>> GetAllSalesOrdersAsync();
        Task<SalesOrder?> GetSalesOrderByIdAsync(int id);
        Task<List<ProfitReportDto>> GetProfitReportAsync();
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();
        Task DeleteSalesOrderAsync(int id);
    }

    public class SalesService : ISalesService
    {
        private readonly ApparelDbContext _context;
        private readonly IInventoryService _inventoryService;

        public SalesService(ApparelDbContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }

        public async Task<List<SalesOrder>> GetAllSalesOrdersAsync()
        {
            return await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .OrderByDescending(so => so.SalesDate)
                .ToListAsync();
        }

        public async Task<SalesOrder?> GetSalesOrderByIdAsync(int id)
        {
            return await _context.SalesOrders
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(so => so.Id == id);
        }

        public async Task DeleteSalesOrderAsync(int id)
        {
            var salesOrder = await _context.SalesOrders.FindAsync(id);
            if (salesOrder == null)
            {
                throw new ArgumentException("Sales order not found.");
            }

            // Remove associated stock ledger entries (so stock is reverted)
            var ledgerEntries = await _context.StockLedgerEntries
                .Where(sl => sl.TransactionType == "SALE_OUT" && sl.ReferenceId == id)
                .ToListAsync();
            _context.StockLedgerEntries.RemoveRange(ledgerEntries);

            // Remove the sales order itself (cascade delete handles SalesOrderItems)
            _context.SalesOrders.Remove(salesOrder);

            await _context.SaveChangesAsync();
        }

        public async Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto)
        {
            if (dto.Items == null || !dto.Items.Any())
            {
                throw new ArgumentException("Sales order must contain at least one item.");
            }

            // 1. Verify Stock for all items first (Transaction Rollback/Fail early)
            foreach (var item in dto.Items)
            {
                bool isAvailable = await _inventoryService.VerifyStockAvailabilityAsync(item.ProductId, item.ItemType, item.Quantity);
                if (!isAvailable)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    string name = product?.Name ?? $"Product #{item.ProductId}";
                    throw new InvalidOperationException($"Insufficient stock for item: {name} (Size: {product?.Size}, Color: {product?.Color}).");
                }
            }

            // 2. Generate unique Invoice Number
            string invoiceNo = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // 3. Construct Order Items & Calculate Pricing
            decimal orderTotalAmount = 0.00m;
            decimal orderTotalGst = 0.00m;
            var orderItems = new List<SalesOrderItem>();

            foreach (var itemDto in dto.Items)
            {
                var product = await _context.Products.FindAsync(itemDto.ProductId)
                    ?? throw new ArgumentException($"Product #{itemDto.ProductId} not found");

                // In a SET sale, quantity represents number of sets, so total items = quantity * 4. 
                // Price per set is typically unitPrice * 4, or unitPrice represents the set price.
                // To keep it simple: unitPrice represents the selling price of 1 Piece or 1 Set.
                decimal lineQty = itemDto.Quantity;
                decimal itemSubtotalExcludingTax = (lineQty * itemDto.UnitPrice) - itemDto.Discount;
                
                decimal taxRate = product.GstPercent / 100.0m;
                decimal itemGstAmount = itemSubtotalExcludingTax * taxRate;
                decimal itemSubtotalWithTax = itemSubtotalExcludingTax + itemGstAmount;

                orderTotalAmount += itemSubtotalExcludingTax;
                orderTotalGst += itemGstAmount;

                orderItems.Add(new SalesOrderItem
                {
                    ProductId = itemDto.ProductId,
                    ItemType = itemDto.ItemType,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice,
                    GstPercent = product.GstPercent,
                    Discount = itemDto.Discount,
                    SubTotal = itemSubtotalWithTax
                });
            }

            // 4. Create and Save SalesOrder
            var salesOrder = new SalesOrder
            {
                InvoiceNo = invoiceNo,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                TotalAmount = orderTotalAmount,
                TotalGst = orderTotalGst,
                FinalAmount = orderTotalAmount + orderTotalGst,
                SalesDate = DateTime.UtcNow,
                Items = orderItems
            };

            _context.SalesOrders.Add(salesOrder);
            await _context.SaveChangesAsync(); // Saves order to generate IDs

            // 5. Record stock deductions
            foreach (var item in salesOrder.Items)
            {
                await _inventoryService.RecordStockDeductionAsync(item.ProductId, item.ItemType, item.Quantity, salesOrder.Id);
            }

            return salesOrder;
        }

        public async Task<List<ProfitReportDto>> GetProfitReportAsync()
        {
            return await _context.ProfitReports.ToListAsync();
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            var reports = await GetProfitReportAsync();

            decimal totalSales = reports.Sum(r => r.ItemRevenue);
            decimal totalProfit = reports.Sum(r => r.NetProfit);

            decimal mfgSales = reports.Where(r => r.ProductType == "MANUFACTURED").Sum(r => r.ItemRevenue);
            decimal mfgProfit = reports.Where(r => r.ProductType == "MANUFACTURED").Sum(r => r.NetProfit);

            decimal tradingSales = reports.Where(r => r.ProductType == "TRADED").Sum(r => r.ItemRevenue);
            decimal tradingProfit = reports.Where(r => r.ProductType == "TRADED").Sum(r => r.NetProfit);

            int lowStockCount = await _context.StockSummaries.CountAsync(s => s.CurrentStock < 10);
            int activeBatches = await _context.ProductionBatches.CountAsync(b => b.Status != "COMPLETED");

            return new DashboardSummaryDto
            {
                TotalSales = totalSales,
                TotalProfit = totalProfit,
                MfgSales = mfgSales,
                MfgProfit = mfgProfit,
                TradingSales = tradingSales,
                TradingProfit = tradingProfit,
                LowStockProductsCount = lowStockCount,
                ActiveProductionBatchesCount = activeBatches
            };
        }
    }
}
