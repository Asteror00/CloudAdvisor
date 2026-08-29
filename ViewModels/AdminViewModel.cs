using System.Collections.Generic;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.ViewModels
{
    /// <summary>
    /// View model for the Admin Dashboard panel.
    /// </summary>
    public class AdminViewModel
    {
        /// <summary>
        /// Gets or sets the list of AWS recommendations.
        /// </summary>
        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

        /// <summary>
        /// Gets or sets the list of historical analysis records.
        /// </summary>
        public List<AnalysisSession> AnalysisHistories { get; set; } = new List<AnalysisSession>();

        /// <summary>
        /// Gets or sets the total number of analyses run.
        /// </summary>
        public int TotalAnalysesRun { get; set; }

        /// <summary>
        /// Gets or sets the name of the most commonly detected architectural feature.
        /// </summary>
        public string MostCommonFeature { get; set; } = "None";

        /// <summary>
        /// Gets or sets the average calculated monthly cost across all past projects.
        /// </summary>
        public decimal AverageMonthlyCost { get; set; }

        /// <summary>
        /// Gets or sets a dictionary containing count statistics for each feature.
        /// </summary>
        public Dictionary<string, int> FeatureCounts { get; set; } = new Dictionary<string, int>();
    }
}
