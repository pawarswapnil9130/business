using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("products")]
    public class Product
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Column("product_type")]
        [MaxLength(50)]
        public string ProductType { get; set; } = string.Empty; // 'MANUFACTURED' or 'TRADED'

        [Column("design_brand")]
        [MaxLength(255)]
        public string? DesignBrand { get; set; }

        [Required]
        [Column("size")]
        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        [Required]
        [Column("color")]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        [Column("cost_price")]
        public decimal CostPrice { get; set; }

        [Column("selling_price")]
        public decimal SellingPrice { get; set; }

        [Column("distributor_price")]
        public decimal DistributorPrice { get; set; } = 0.00m;

        [Column("gst_percent")]
        public decimal GstPercent { get; set; } = 18.00m;

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("set_size")]
        public int SetSize { get; set; } = 4; // Set size (e.g. 3 pcs or 4 pcs per ratio bundle)

        [Column("set_ratio")]
        [MaxLength(255)]
        public string? SetRatio { get; set; } = "38, 40, 42, 44"; // e.g. "M, L, XL" or "38, 40, 42, 44"

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
