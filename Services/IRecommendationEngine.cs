using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.Services
{
    public interface IRecommendationEngine
    {
        /// <summary>
        /// Generates prioritized, rule-based AWS recommendations from the extracted features of a project.
        /// </summary>
        Task<List<Recommendation>> GenerateRecommendationsAsync(List<ExtractedFeature> features, Guid sessionId);
    }
}
