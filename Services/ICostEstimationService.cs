using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.Services
{
    public interface ICostEstimationService
    {
        /// <summary>
        /// Generates cost estimates for each recommendation based on the pricing configurations in PricingConfig.json.
        /// </summary>
        Task<List<CostEstimate>> EstimateCostsAsync(List<Recommendation> recommendations, Guid sessionId);

        /// <summary>
        /// Calculates the total monthly cost.
        /// </summary>
        decimal CalculateTotalMonthlyCost(List<CostEstimate> estimates);
    }
}
