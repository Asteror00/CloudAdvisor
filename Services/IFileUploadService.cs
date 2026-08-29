using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CloudAdvisor.Services
{
    public interface IFileUploadService
    {
        /// <summary>
        /// Saves the ZIP file, validates it, and extracts it to a temporary directory.
        /// </summary>
        Task<(bool Success, string ErrorMessage, string? ExtractionPath, List<string>? CsFiles)> SaveAndExtractArchiveAsync(IFormFile file, Guid sessionId);

        /// <summary>
        /// Deletes the temporary directory and all its contents.
        /// </summary>
        void CleanUpSessionDirectory(string extractionPath);
    }
}
