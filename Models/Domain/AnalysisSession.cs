using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudAdvisor.Models.Domain
{
    public class AnalysisSession
    {
        [Key]
        public Guid SessionId { get; set; }

        [Required]
        [StringLength(255)]
        public string ProjectName { get; set; } = string.Empty;

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public SessionStatus Status { get; set; } = SessionStatus.Pending;

        public string? ErrorMessage { get; set; }

        // Foreign key to User
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Helper summary fields for frontend compatibility and fast querying
        public bool HasDatabase { get; set; }
        public bool HasAuthentication { get; set; }
        public bool HasFileHandling { get; set; }
        public bool HasApiControllers { get; set; }
        public bool HasBackgroundServices { get; set; }
        public bool HasCaching { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// JSON-serialized list of recommendations matching the format expected by the frontend.
        /// </summary>
        public string RecommendationsJson { get; set; } = "[]";

        // Navigation collections
        public ICollection<ExtractedFeature> ExtractedFeatures { get; set; } = new List<ExtractedFeature>();
        public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
        public ICollection<CostEstimate> CostEstimates { get; set; } = new List<CostEstimate>();
    }
}
