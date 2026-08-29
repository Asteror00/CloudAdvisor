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
    public class RecommendationEngine : IRecommendationEngine
    {
        private readonly IConfiguration _config;
        private readonly ILogger<RecommendationEngine> _logger;

        public RecommendationEngine(IConfiguration config, ILogger<RecommendationEngine> logger)
        {
            _config = config;
            _logger = logger;
        }

        private class RuleDefinition
        {
            public string RuleId { get; set; } = string.Empty;
            public string TriggerFeature { get; set; } = string.Empty;
            public string Condition { get; set; } = string.Empty;
            public string AwsService { get; set; } = string.Empty;
            public string ServiceCategory { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        private class RuleConfig
        {
            public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();
        }

        public async Task<List<Recommendation>> GenerateRecommendationsAsync(List<ExtractedFeature> features, Guid sessionId)
        {
            _logger.LogInformation("Generating recommendations for session {SessionId}...", sessionId);

            var recommendations = new List<Recommendation>();
            string rulesFilePath = _config["CloudAdvisor:RulesFilePath"] ?? "Rules/RecommendationRules.json";

            if (!File.Exists(rulesFilePath))
            {
                _logger.LogWarning("Rules configuration file not found at {Path}. Returning empty recommendations.", rulesFilePath);
                return recommendations;
            }

            try
            {
                // Load and parse rules
                string jsonText = await File.ReadAllTextAsync(rulesFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ruleConfig = JsonSerializer.Deserialize<RuleConfig>(jsonText, options);

                if (ruleConfig == null || ruleConfig.Rules == null)
                {
                    return recommendations;
                }

                foreach (var rule in ruleConfig.Rules)
                {
                    if (EvaluateCondition(rule, features, out int actualCount, out string firstFeatureName, out int dbSetCount))
                    {
                        // Parse priority
                        if (!Enum.TryParse<RecommendationPriority>(rule.Priority, true, out var priority))
                        {
                            priority = RecommendationPriority.Recommended;
                        }

                        // Parse category
                        if (!Enum.TryParse<ServiceCategory>(rule.ServiceCategory, true, out var category))
                        {
                            category = ServiceCategory.Compute;
                        }

                        // Format reason string
                        string formattedReason = rule.Reason
                            .Replace("{count}", actualCount.ToString())
                            .Replace("{name}", firstFeatureName)
                            .Replace("{dbSetCount}", dbSetCount.ToString());

                        recommendations.Add(new Recommendation
                        {
                            SessionId = sessionId,
                            AwsService = rule.AwsService,
                            ServiceCategory = category,
                            Reason = formattedReason,
                            Priority = priority,
                            TriggeringFeature = rule.TriggerFeature
                        });
                    }
                }

                // Deduplicate recommendations by AWS Service (keep the highest priority one)
                recommendations = recommendations
                    .GroupBy(r => r.AwsService)
                    .Select(g => g.OrderBy(r => r.Priority).First()) // Required (0) comes before Recommended (1) and Optional (2) in the enum
                    .ToList();

                // Sort ordered: Required first, then Recommended, then Optional
                recommendations = recommendations
                    .OrderBy(r => r.Priority)
                    .ToList();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run recommendation engine logic for session {SessionId}", sessionId);
                throw;
            }

            _logger.LogInformation("Generated {Count} recommendations.", recommendations.Count);
            return recommendations;
        }

        private bool EvaluateCondition(RuleDefinition rule, List<ExtractedFeature> features, out int actualCount, out string name, out int dbSetCount)
        {
            actualCount = 0;
            name = string.Empty;
            dbSetCount = 0;

            string trigger = rule.TriggerFeature;
            if (!Enum.TryParse<FeatureType>(trigger, true, out var targetType))
            {
                return false;
            }

            var matchingFeatures = features.Where(f => f.FeatureType == targetType).ToList();
            actualCount = matchingFeatures.Count;

            if (actualCount > 0)
            {
                name = matchingFeatures[0].FeatureName;

                if (targetType == FeatureType.DbContext)
                {
                    foreach (var f in matchingFeatures)
                    {
                        try
                        {
                            using (var doc = JsonDocument.Parse(f.Details))
                            {
                                if (doc.RootElement.TryGetProperty("DbSetCount", out var countProp))
                                {
                                    dbSetCount += countProp.GetInt32();
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            string cond = rule.Condition.Trim();
            if (cond.Equals("present", StringComparison.OrdinalIgnoreCase))
            {
                return actualCount > 0;
            }

            if (cond.StartsWith("count", StringComparison.OrdinalIgnoreCase))
            {
                string opAndValue = cond.Substring("count".Length).Trim();
                return EvaluateNumericCondition(opAndValue, actualCount);
            }

            if (cond.StartsWith("dbSetCount", StringComparison.OrdinalIgnoreCase))
            {
                string opAndValue = cond.Substring("dbSetCount".Length).Trim();
                return EvaluateNumericCondition(opAndValue, dbSetCount);
            }

            return false;
        }

        private bool EvaluateNumericCondition(string opAndValue, int val)
        {
            if (opAndValue.StartsWith(">="))
            {
                if (int.TryParse(opAndValue.Substring(2).Trim(), out int target))
                {
                    return val >= target;
                }
            }
            else if (opAndValue.StartsWith("<="))
            {
                if (int.TryParse(opAndValue.Substring(2).Trim(), out int target))
                {
                    return val <= target;
                }
            }
            else if (opAndValue.StartsWith(">"))
            {
                if (int.TryParse(opAndValue.Substring(1).Trim(), out int target))
                {
                    return val > target;
                }
            }
            else if (opAndValue.StartsWith("<"))
            {
                if (int.TryParse(opAndValue.Substring(1).Trim(), out int target))
                {
                    return val < target;
                }
            }
            else if (opAndValue.StartsWith("=="))
            {
                if (int.TryParse(opAndValue.Substring(2).Trim(), out int target))
                {
                    return val == target;
                }
            }
            else if (opAndValue.StartsWith("="))
            {
                if (int.TryParse(opAndValue.Substring(1).Trim(), out int target))
                {
                    return val == target;
                }
            }

            return false;
        }
    }
}
