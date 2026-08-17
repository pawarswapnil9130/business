using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("sales_orders")]
    public class SalesOrder
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_no")]
        [MaxLength(100)]
        public string InvoiceNo { get; set; } = string.Empty;

        [Required]
        [Column("customer_name")]
        [MaxLength(255)]
        public string CustomerName { get; set; } = string.Empty;

        [Column("customer_phone")]
        [MaxLength(50)]
        public string? CustomerPhone { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Required]
        [Column("order_status")]
        [MaxLength(50)]
        public string OrderStatus { get; set; } = "COMPLETED"; // 'PENDING', 'CONFIRMED', 'PROCESSING', 'DISPATCHED', 'DELIVERED', 'CANCELLED', 'COMPLETED'

        [Required]
        [Column("order_source")]
        [MaxLength(50)]
        public string OrderSource { get; set; } = "POS_DESK"; // 'POS_DESK' or 'WHOLESALE_PORTAL'

        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Required]
        [Column("total_gst")]
        public decimal TotalGst { get; set; }

        [Required]
        [Column("final_amount")]
        public decimal FinalAmount { get; set; }

        [Column("sales_date")]
        public DateTime SalesDate { get; set; } = DateTime.UtcNow;

        public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
    }
}
