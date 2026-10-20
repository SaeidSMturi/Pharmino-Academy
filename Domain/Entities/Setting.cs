using System.Diagnostics.CodeAnalysis;

namespace Domain.Entities
{
    public class Setting
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        [AllowNull]
        public string? Description { get; set; }
        [AllowNull]
        public string? DataString { get; set; }
        [AllowNull]
        public bool? DataBool { get; set; }
        [AllowNull]
        public int? DataInt { get; set; }
        [AllowNull]
        public decimal? DataDecimal { get; set; }
        [AllowNull]
        public string? DataJson { get; set; }
        public bool Changeable { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
