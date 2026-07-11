using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("fabrics")]
    public class Fabric
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("color")]
        [MaxLength(100)]
        public string Color { get; set; } = string.Empty;

        [Column("supplier_id")]
        public int? SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        [Required]
        [Column("cost_per_meter")]
        public decimal CostPerMeter { get; set; }

        [Required]
        [Column("total_meters")]
        public decimal TotalMeters { get; set; }

        [Column("used_meters")]
        public decimal UsedMeters { get; set; } = 0.00m;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
