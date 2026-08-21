using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Data
{
    public class ApparelDbContext : DbContext
    {
        public ApparelDbContext(DbContextOptions<ApparelDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Fabric> Fabrics { get; set; }
        public DbSet<ProductionBatch> ProductionBatches { get; set; }
        public DbSet<TradingPurchase> TradingPurchases { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<StockLedger> StockLedgerEntries { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        
        // Keyless views
        public DbSet<StockSummaryDto> StockSummaries { get; set; }
        public DbSet<ProfitReportDto> ProfitReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure View mappings
            modelBuilder.Entity<StockSummaryDto>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_stock_summary");
            });

            modelBuilder.Entity<ProfitReportDto>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_profit_report");
            });

            // Set cascade delete behaviour or specific properties if needed
            modelBuilder.Entity<SalesOrderItem>()
                .HasOne(soi => soi.SalesOrder)
                .WithMany(so => so.Items)
                .HasForeignKey(soi => soi.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
