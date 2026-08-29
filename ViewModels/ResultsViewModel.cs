using System;
using System.Collections.Generic;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.ViewModels
{
    /// <summary>
    /// View model for presenting the analysis dashboard results.
    /// </summary>
    public class ResultsViewModel
    {
        public string ProjectName { get; set; } = string.Empty;
        public DateTime AnalysedAt { get; set; }
        
        public bool HasDatabase { get; set; }
        public bool HasAuthentication { get; set; }
        public bool HasFileHandling { get; set; }
        public bool HasApiControllers { get; set; }
        public bool HasBackgroundServices { get; set; }
        public bool HasCaching { get; set; }

        public decimal TotalMonthlyCost { get; set; }
        public decimal TotalAnnualCost { get; set; }
        
        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

        public int ExtractedFilesCount { get; set; }
        public long AnalysisTimeMs { get; set; }
    }
}
