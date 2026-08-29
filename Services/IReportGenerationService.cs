using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.Services
{
    public interface IReportGenerationService
    {
        /// <summary>
        /// Generates a styled, multi-page PDF report containing the project overview, features, recommendations, and cost breakdown.
        /// </summary>
        Task<byte[]> GeneratePdfReportAsync(AnalysisSession session);
    }
}
