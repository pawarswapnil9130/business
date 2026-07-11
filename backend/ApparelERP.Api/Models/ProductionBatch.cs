using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("production_batches")]
    public class ProductionBatch
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("batch_code")]
        [MaxLength(100)]
        public string BatchCode { get; set; } = string.Empty;

        [Column("fabric_id")]
        public int FabricId { get; set; }

        [ForeignKey(nameof(FabricId))]
        public Fabric? Fabric { get; set; }

        [Required]
        [Column("fabric_meters_used")]
        public decimal FabricMetersUsed { get; set; }

        [Column("wastage_meters")]
        public decimal WastageMeters { get; set; } = 0.00m;

        [Column("tailoring_cost")]
        public decimal TailoringCost { get; set; } = 0.00m;

        [Column("additional_cost")]
        public decimal AdditionalCost { get; set; } = 0.00m;

        [Column("quantity_produced")]
        public int QuantityProduced { get; set; } = 0;

        [Column("cost_per_piece")]
        public decimal CostPerPiece { get; set; } = 0.00m;

        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "PLANNING"; // 'PLANNING', 'IN_PRODUCTION', 'COMPLETED'

        [Column("product_id")]
        public int? ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Column("design_name")]
        [MaxLength(255)]
        public string? DesignName { get; set; }

        [Column("quantity_to_sew")]
        public int QuantityToSew { get; set; }

        [Column("date_created")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Column("date_completed")]
        public DateTime? DateCompleted { get; set; }
    }
}
