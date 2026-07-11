using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("trading_purchases")]
    public class TradingPurchase
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_no")]
        [MaxLength(100)]
        public string InvoiceNo { get; set; } = string.Empty;

        [Column("supplier_id")]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("purchase_price")]
        public decimal PurchasePrice { get; set; }

        [Column("gst_percent")]
        public decimal GstPercent { get; set; } = 18.00m;

        [Required]
        [Column("total_cost")]
        public decimal TotalCost { get; set; } // Quantity * PurchasePrice + GST

        [Column("purchase_date")]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    }
}
