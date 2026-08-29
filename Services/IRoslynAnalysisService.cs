using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;

namespace CloudAdvisor.Services
{
    public interface IRoslynAnalysisService
    {
        /// <summary>
        /// Walk C# files using Roslyn to extract architectural features like Controllers, DbContexts, auth schemes, backgrounds, file handling, and DI.
        /// </summary>
        Task<List<ExtractedFeature>> AnalyzeProjectFilesAsync(List<string> filePaths, Guid sessionId, CancellationToken cancellationToken = default);
    }
}
