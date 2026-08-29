using System;
using System.Collections.Generic;

namespace CloudAdvisor.Models.DTOs
{
    public class UploadResponseDto
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ExtractedFeatureDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public object Details { get; set; } = new object();
    }

    public class RecommendationDto
    {
        public string AwsService { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string TriggeringFeature { get; set; } = string.Empty;
    }

    public class CostSummaryItemDto
    {
        public string Service { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public decimal MonthlyCostUSD { get; set; }
        public string Justification { get; set; } = string.Empty;
    }

    public class CostSummaryDto
    {
        public List<CostSummaryItemDto> Items { get; set; } = new List<CostSummaryItemDto>();
        public decimal TotalMonthlyCostUSD { get; set; }
        public string Disclaimer { get; set; } = string.Empty;
    }

    public class AnalysisResultDto
    {
        public Guid SessionId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public List<ExtractedFeatureDto> Features { get; set; } = new List<ExtractedFeatureDto>();
        public List<RecommendationDto> Recommendations { get; set; } = new List<RecommendationDto>();
        public CostSummaryDto CostSummary { get; set; } = new CostSummaryDto();
    }
}
