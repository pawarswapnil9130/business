using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelERP.Api.Models
{
    [Table("system_settings")]
    public class SystemSetting
    {
        [Key]
        [Column("setting_key")]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Column("setting_value")]
        public string Value { get; set; } = string.Empty;
    }
}
