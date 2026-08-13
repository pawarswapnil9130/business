using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("expenses")]
    public class Expense
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        public string Category { get; set; } = "General";

        [Required]
        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("expense_date")]
        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

        [Column("payment_mode")]
        public string PaymentMode { get; set; } = "CASH";

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
