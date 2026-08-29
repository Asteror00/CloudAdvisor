using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudAdvisor.Services
{
    public class CostEstimationService : ICostEstimationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<CostEstimationService> _logger;

        public CostEstimationService(IConfiguration config, ILogger<CostEstimationService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private class PricingItem
        {
            public string Service { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public decimal MonthlyUSD { get; set; }
            public string Unit { get; set; } = string.Empty;
        }

        private class PricingConfig
        {
            public List<PricingItem> Pricing { get; set; } = new List<PricingItem>();
        }

        private class RuleDefinition
        {
            public string RuleId { get; set; } = string.Empty;
            public string TriggerFeature { get; set; } = string.Empty;
            public string AwsService { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
        }

        private class RuleConfig
        {
            public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();
        }

        public async Task<List<CostEstimate>> EstimateCostsAsync(List<Recommendation> recommendations, Guid sessionId)
        {
            _logger.LogInformation("Estimating costs for session {SessionId}...", sessionId);
            var estimates = new List<CostEstimate>();

            string pricingFilePath = _config["CloudAdvisor:PricingFilePath"] ?? "Rules/PricingConfig.json";
            string rulesFilePath = _config["CloudAdvisor:RulesFilePath"] ?? "Rules/RecommendationRules.json";

            if (!File.Exists(pricingFilePath))
            {
                _logger.LogWarning("Pricing configuration file not found at {Path}.", pricingFilePath);
                return estimates;
            }

            try
            {
                // Load pricing configurations
                string pricingJson = await File.ReadAllTextAsync(pricingFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pricingConfig = JsonSerializer.Deserialize<PricingConfig>(pricingJson, options);

                // Load rules configurations to cross-reference tiers
                List<RuleDefinition> rules = new List<RuleDefinition>();
                if (File.Exists(rulesFilePath))
                {
                    string rulesJson = await File.ReadAllTextAsync(rulesFilePath);
                    var ruleConfigObj = JsonSerializer.Deserialize<RuleConfig>(rulesJson, options);
                    if (ruleConfigObj != null && ruleConfigObj.Rules != null)
                    {
                        rules = ruleConfigObj.Rules;
                    }
                }

                if (pricingConfig == null || pricingConfig.Pricing == null)
                {
                    return estimates;
                }

                foreach (var rec in recommendations)
                {
                    // Find the rule to determine which tier is recommended
                    var rule = rules.FirstOrDefault(r => 
                        r.AwsService.Equals(rec.AwsService, StringComparison.OrdinalIgnoreCase) && 
                        r.TriggerFeature.Equals(rec.TriggeringFeature, StringComparison.OrdinalIgnoreCase));

                    string targetTier = rule?.Tier ?? "Standard";

                    // Match service + tier in the pricing config
                    var priceItem = pricingConfig.Pricing.FirstOrDefault(p => 
                        p.Service.Equals(rec.AwsService, StringComparison.OrdinalIgnoreCase) && 
                        p.Tier.Equals(targetTier, StringComparison.OrdinalIgnoreCase));

                    // Fallback to service name match if tier does not match
                    if (priceItem == null)
                    {
                        priceItem = pricingConfig.Pricing.FirstOrDefault(p => 
                            p.Service.Equals(rec.AwsService, StringComparison.OrdinalIgnoreCase));
                    }

                    decimal cost = priceItem?.MonthlyUSD ?? 0.00m;
                    string tierName = priceItem?.Tier ?? targetTier;
                    string unit = priceItem?.Unit ?? "unit";

                    estimates.Add(new CostEstimate
                    {
                        SessionId = sessionId,
                        RecommendationId = rec.RecommendationId,
                        ServiceName = rec.AwsService,
                        Tier = tierName,
                        MonthlyCostUSD = cost,
                        Justification = $"Cost of {rec.AwsService} ({tierName} tier) based on estimated usage. Billing unit: {unit}."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate cost estimations for session {SessionId}", sessionId);
                throw;
            }

            return estimates;
        }

        public decimal CalculateTotalMonthlyCost(List<CostEstimate> estimates)
        {
            if (estimates == null || estimates.Count == 0) return 0.00m;
            return estimates.Sum(e => e.MonthlyCostUSD);
        }
    }
}
