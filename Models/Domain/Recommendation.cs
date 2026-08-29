using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudAdvisor.Models.Domain
{
    public class Recommendation
    {
        [Key]
        public int RecommendationId { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [ForeignKey("SessionId")]
        public AnalysisSession? Session { get; set; }

        [Required]
        [StringLength(100)]
        public string AwsService { get; set; } = string.Empty;

        [Required]
        public ServiceCategory ServiceCategory { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public RecommendationPriority Priority { get; set; }

        [Required]
        [StringLength(100)]
        public string TriggeringFeature { get; set; } = string.Empty;
    }
}
