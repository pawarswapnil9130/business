using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models.Dto
{
    public class CreateSalesOrderDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public List<CreateSalesOrderItemDto> Items { get; set; } = new();
    }

    public class CreateSalesOrderItemDto
    {
        public int ProductId { get; set; }
        public string ItemType { get; set; } = "PCS"; // 'SET' or 'PCS'
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } = 0.00m;
    }

    public class StartProductionBatchDto
    {
        public string BatchCode { get; set; } = string.Empty;
        public int FabricId { get; set; }
        public decimal FabricMetersUsed { get; set; }
        public decimal WastageMeters { get; set; }
        public int? ProductId { get; set; }
        public string? DesignName { get; set; }
        public int QuantityToSew { get; set; }
    }

    public class CompleteProductionBatchDto
    {
        public int BatchId { get; set; }
        public decimal TailoringCost { get; set; }
        public decimal AdditionalCost { get; set; }
        public int QuantityProduced { get; set; }
    }

    public class CreateTradingPurchaseDto
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal GstPercent { get; set; } = 18.00m;
    }

    public class StockSummaryDto
    {
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [Column("category")]
        public string Category { get; set; } = string.Empty;

        [Column("product_type")]
        public string ProductType { get; set; } = string.Empty;

        [Column("design_brand")]
        public string? DesignBrand { get; set; }

        [Column("size")]
        public string Size { get; set; } = string.Empty;

        [Column("color")]
        public string Color { get; set; } = string.Empty;

        [Column("cost_price")]
        public decimal CostPrice { get; set; }

        [Column("selling_price")]
        public decimal SellingPrice { get; set; }

        [Column("current_stock")]
        public int CurrentStock { get; set; }
    }

    public class ProfitReportDto
    {
        [Column("sales_order_id")]
        public int SalesOrderId { get; set; }

        [Column("invoice_no")]
        public string InvoiceNo { get; set; } = string.Empty;

        [Column("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [Column("sales_date")]
        public DateTime SalesDate { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [Column("product_type")]
        public string ProductType { get; set; } = string.Empty;

        [Column("category")]
        public string Category { get; set; } = string.Empty;

        [Column("size")]
        public string Size { get; set; } = string.Empty;

        [Column("color")]
        public string Color { get; set; } = string.Empty;

        [Column("item_type")]
        public string ItemType { get; set; } = string.Empty;

        [Column("quantity_sold")]
        public int QuantitySold { get; set; }

        [Column("selling_price_per_unit")]
        public decimal SellingPricePerUnit { get; set; }

        [Column("item_revenue")]
        public decimal ItemRevenue { get; set; }

        [Column("cost_price_per_unit")]
        public decimal CostPricePerUnit { get; set; }

        [Column("total_cost_basis")]
        public decimal TotalCostBasis { get; set; }

        [Column("net_profit")]
        public decimal NetProfit { get; set; }
    }

    public class DashboardSummaryDto
    {
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal MfgSales { get; set; }
        public decimal MfgProfit { get; set; }
        public decimal TradingSales { get; set; }
        public decimal TradingProfit { get; set; }
        public int LowStockProductsCount { get; set; }
        public int ActiveProductionBatchesCount { get; set; }
    }

    public class UserRegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UserLoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UserAuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
    }

    public class UserSummaryDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserOnboardDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
