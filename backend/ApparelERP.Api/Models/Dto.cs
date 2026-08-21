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
        public int SetSize { get; set; } = 4;
        public string? SetRatio { get; set; } = "38, 40, 42, 44";
        public decimal SellingPrice { get; set; }
        public decimal DistributorPrice { get; set; }
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

    public class CreateExpenseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public decimal Amount { get; set; }
        public DateTime? ExpenseDate { get; set; }
        public string PaymentMode { get; set; } = "CASH";
        public string? Notes { get; set; }
    }

    public class ExpenseSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string PaymentMode { get; set; } = "CASH";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardSummaryDto
    {
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; } // Clear Net Profit (Gross Profit - Operating Expenses)
        public decimal GrossProfit { get; set; } // Profit before Operating Expenses
        public decimal TotalExpenses { get; set; } // Total Operating Expenses
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

    // ==========================================
    // CUSTOMER WHOLESALE PORTAL DTOs
    // ==========================================

    public class CustomerApplyDto
    {
        public string ShopName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Whatsapp { get; set; }
        public string? Email { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? GstNumber { get; set; }
        public string? Password { get; set; }
        public string? Notes { get; set; }
    }

    public class CustomerLoginDto
    {
        public string PhoneOrUsername { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerLoginStep1ResponseDto
    {
        public bool RequireOtp { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        public string PhoneMasked { get; set; } = string.Empty;
        public string TempSessionToken { get; set; } = string.Empty;
    }

    public class CustomerOtpVerifyDto
    {
        public string TempSessionToken { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }

    public class CustomerAuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string CustomerType { get; set; } = "REGULAR"; // 'REGULAR' or 'DISTRIBUTOR'
        public string City { get; set; } = string.Empty;
    }

    public class CustomerProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? DesignBrand { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public decimal Price { get; set; } // The exact tier price per piece (Regular or Distributor)
        public int SetSize { get; set; } = 4; // e.g. 3 or 4 pcs per ratio pack
        public string? SetRatio { get; set; } = "38, 40, 42, 44";
        public decimal SetPrice => Price * (SetSize > 0 ? SetSize : 1);
        public decimal GstPercent { get; set; }
        public int AvailableStock { get; set; } // Current pieces in stock
        public int AvailableSets => SetSize > 0 ? (AvailableStock / SetSize) : AvailableStock; // Max full sets available
        public bool Available => AvailableSets > 0; // True only if at least 1 full set is in stock
        public string? ImageUrl { get; set; }
    }

    public class CustomerPublicProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? DesignBrand { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int SetSize { get; set; } = 4;
        public string? SetRatio { get; set; } = "38, 40, 42, 44";
        public string? ImageUrl { get; set; }
        public int AvailableSets { get; set; }
        public bool Available => AvailableSets > 0;
    }

    public class CustomerOrderCreateDto
    {
        public string? Notes { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentProofUrl { get; set; }
        public List<CustomerOrderItemCreateDto> Items { get; set; } = new();
    }

    public class CustomerOrderItemCreateDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } // Total pieces (must be multiple of SetSize)
        public int SetsCount { get; set; } // Number of sets ordered (e.g. 2 sets)
    }

    public class CustomerOrderDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = "PENDING";
        public string OrderSource { get; set; } = "WHOLESALE_PORTAL";
        public decimal TotalAmount { get; set; }
        public decimal TotalGst { get; set; }
        public decimal FinalAmount { get; set; }
        public DateTime SalesDate { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentProofUrl { get; set; }
        public List<CustomerOrderItemDto> Items { get; set; } = new();
    }

    public class CustomerOrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string? DesignBrand { get; set; }
        public int SetSize { get; set; } = 4;
        public string? SetRatio { get; set; }
        public int SetsCount { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GstPercent { get; set; }
        public decimal SubTotal { get; set; }
    }

    // ==========================================
    // ADMIN CUSTOMER MANAGEMENT DTOs
    // ==========================================

    public class AdminCustomerSummaryDto
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Whatsapp { get; set; }
        public string? Email { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? GstNumber { get; set; }
        public string CustomerType { get; set; } = "REGULAR";
        public string Status { get; set; } = "PENDING";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int TotalOrdersPlaced { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class UpdateCustomerStatusDto
    {
        public string Status { get; set; } = "ACTIVE"; // 'PENDING', 'ACTIVE', 'SUSPENDED', 'REJECTED'
        public string? Notes { get; set; }
    }

    public class UpdateCustomerTypeDto
    {
        public string CustomerType { get; set; } = "REGULAR"; // 'REGULAR' or 'DISTRIBUTOR'
    }

    public class UpdateOrderStatusDto
    {
        public string OrderStatus { get; set; } = "CONFIRMED"; // 'PENDING', 'CONFIRMED', 'PROCESSING', 'DISPATCHED', 'DELIVERED', 'CANCELLED'
    }
}
