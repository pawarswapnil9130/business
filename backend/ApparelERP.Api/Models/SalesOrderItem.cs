using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("sales_order_items")]
    public class SalesOrderItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("sales_order_id")]
        public int SalesOrderId { get; set; }

        [ForeignKey(nameof(SalesOrderId))]
        public SalesOrder? SalesOrder { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        [Column("item_type")]
        [MaxLength(50)]
        public string ItemType { get; set; } = "PCS"; // 'SET' or 'PCS'

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("gst_percent")]
        public decimal GstPercent { get; set; }

        [Column("discount")]
        public decimal Discount { get; set; } = 0.00m;

        [Required]
        [Column("sub_total")]
        public decimal SubTotal { get; set; }
    }
}
