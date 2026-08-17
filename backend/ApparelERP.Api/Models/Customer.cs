using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column("shop_name")]
        [MaxLength(255)]
        public string ShopName { get; set; } = string.Empty;

        [Required]
        [Column("owner_name")]
        [MaxLength(255)]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [Column("phone")]
        [MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [Column("whatsapp")]
        [MaxLength(50)]
        public string? Whatsapp { get; set; }

        [Column("email")]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Column("city")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Column("gst_number")]
        [MaxLength(50)]
        public string? GstNumber { get; set; }

        [Required]
        [Column("customer_type")]
        [MaxLength(50)]
        public string CustomerType { get; set; } = "REGULAR"; // 'REGULAR' or 'DISTRIBUTOR'

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "PENDING"; // 'PENDING', 'ACTIVE', 'SUSPENDED', 'REJECTED'

        [Column("otp_code")]
        [MaxLength(10)]
        public string? OtpCode { get; set; }

        [Column("otp_expiry")]
        public DateTime? OtpExpiry { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }
    }
}
