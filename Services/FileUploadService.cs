using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CloudAdvisor.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudAdvisor.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<FileUploadService> _logger;

        public FileUploadService(IConfiguration config, ILogger<FileUploadService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<(bool Success, string ErrorMessage, string? ExtractionPath, List<string>? CsFiles)> SaveAndExtractArchiveAsync(IFormFile file, Guid sessionId)
        {
            try
            {
                // 1. Validate the ZIP file
                var (isValid, errorMessage) = FileValidator.ValidateZipFile(file);
                if (!isValid)
                {
                    return (false, errorMessage, null, null);
                }

                // 2. Determine temp folders
                string rootTempPath = _config["CloudAdvisor:TempUploadPath"] 
                    ?? Path.Combine(Path.GetTempPath(), "CloudAdvisor");
                
                string sessionTempPath = Path.Combine(rootTempPath, "sessions", sessionId.ToString());
                Directory.CreateDirectory(sessionTempPath);

                string tempZipPath = Path.Combine(sessionTempPath, $"{sessionId}.zip");
                string extractionPath = Path.Combine(sessionTempPath, "src");

                // 3. Save uploaded file to disk
                using (var stream = new FileStream(tempZipPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 4. Extract
                var csFiles = ZipExtractor.ExtractSourceFiles(tempZipPath, extractionPath);

                // Delete the temporary zip immediately after extraction
                try
                {
                    File.Delete(tempZipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary zip file: {Path}", tempZipPath);
                }

                return (true, string.Empty, sessionTempPath, csFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during ZIP file processing for session {SessionId}.", sessionId);
                return (false, $"Extraction failed: {ex.Message}", null, null);
            }
        }

        public void CleanUpSessionDirectory(string extractionPath)
        {
            if (string.IsNullOrWhiteSpace(extractionPath) || !Directory.Exists(extractionPath))
            {
                return;
            }

            try
            {
                Directory.Delete(extractionPath, recursive: true);
                _logger.LogInformation("Successfully cleaned up session temp directory: {Path}", extractionPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up session temp directory: {Path}", extractionPath);
            }
        }
    }
}
