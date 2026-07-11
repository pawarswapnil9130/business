using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("stock_ledger")]
    public class StockLedger
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        [Column("quantity_change")]
        public int QuantityChange { get; set; } // Positive for stock-in, negative for sales

        [Required]
        [Column("transaction_type")]
        [MaxLength(50)]
        public string TransactionType { get; set; } = string.Empty; // 'MANUFACTURED_IN', 'TRADING_IN', 'SALE_OUT', 'ADJUSTMENT'

        [Column("reference_id")]
        public int? ReferenceId { get; set; } // Production Batch ID, Trading Purchase ID, Sales Order ID

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
