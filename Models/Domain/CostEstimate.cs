using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudAdvisor.Models.Domain
{
    public class CostEstimate
    {
        [Key]
        public int EstimateId { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [ForeignKey("SessionId")]
        public AnalysisSession? Session { get; set; }

        public int? RecommendationId { get; set; }

        [ForeignKey("RecommendationId")]
        public Recommendation? Recommendation { get; set; }

        [Required]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Tier { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyCostUSD { get; set; }

        [Required]
        public string Justification { get; set; } = string.Empty;
    }
}
