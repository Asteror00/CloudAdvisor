using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudAdvisor.Models.Domain
{
    public class ExtractedFeature
    {
        [Key]
        public int FeatureId { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [ForeignKey("SessionId")]
        public AnalysisSession? Session { get; set; }

        [Required]
        public FeatureType FeatureType { get; set; }

        [Required]
        [StringLength(255)]
        public string FeatureName { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public int LineNumber { get; set; }

        // JSON Metadata about findings (e.g. details of routes, methods, JWT config, DbSets)
        [Required]
        public string Details { get; set; } = "{}";
    }
}
