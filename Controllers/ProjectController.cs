using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CloudAdvisor.Data;
using CloudAdvisor.Models.Domain;
using CloudAdvisor.Models.DTOs;
using CloudAdvisor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudAdvisor.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/project")]
    public class ProjectController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(
            ApplicationDbContext context,
            IFileUploadService fileUploadService,
            IServiceProvider serviceProvider,
            ILogger<ProjectController> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB
        public async Task<IActionResult> Upload([FromForm] IFormFile projectFile, [FromForm] string projectName)
        {
            // 1. Get current logged in user ID
            string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { error = "User is not authenticated." });
            }

            // 2. Validate upload basic existence
            if (projectFile == null || projectFile.Length == 0)
            {
                return BadRequest(new { error = "The selected file is empty." });
            }

            var sessionId = Guid.NewGuid();
            string projName = !string.IsNullOrWhiteSpace(projectName)
                ? projectName.Trim()
                : Path.GetFileNameWithoutExtension(projectFile.FileName);

            // 3. Create initial pending session entry in Database
            var session = new AnalysisSession
            {
                SessionId = sessionId,
                ProjectName = projName,
                UploadedAt = DateTime.UtcNow,
                Status = SessionStatus.Processing,
                UserId = userId
            };

            _context.AnalysisSessions.Add(session);
            await _context.SaveChangesAsync();

            // 4. Save and extract synchronously (file validation needs access to IFormFile before dispose)
            var (success, errorMsg, extractionPath, csFiles) = await _fileUploadService.SaveAndExtractArchiveAsync(projectFile, sessionId);
            
            if (!success)
            {
                session.Status = SessionStatus.Failed;
                session.ErrorMessage = errorMsg;
                await _context.SaveChangesAsync();
                return BadRequest(new { error = errorMsg });
            }

            // 5. Run static analysis in background
            _ = Task.Run(async () =>
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var fileService = scope.ServiceProvider.GetRequiredService<IFileUploadService>();
                    var roslynService = scope.ServiceProvider.GetRequiredService<IRoslynAnalysisService>();
                    var recommender = scope.ServiceProvider.GetRequiredService<IRecommendationEngine>();
                    var costService = scope.ServiceProvider.GetRequiredService<ICostEstimationService>();

                    var backgroundSession = await db.AnalysisSessions.FindAsync(sessionId);
                    if (backgroundSession == null) return;

                    try
                    {
                        if (csFiles == null || csFiles.Count == 0)
                        {
                            throw new Exception("No C# (.cs) source files found in the project archive.");
                        }

                        // Static Code Analysis via Roslyn
                        var features = await roslynService.AnalyzeProjectFilesAsync(csFiles, sessionId);
                        
                        // Save features
                        foreach (var f in features)
                        {
                            db.ExtractedFeatures.Add(f);
                        }
                        await db.SaveChangesAsync();

                        // Recommendation generation
                        var recommendations = await recommender.GenerateRecommendationsAsync(features, sessionId);
                        foreach (var r in recommendations)
                        {
                            db.Recommendations.Add(r);
                        }
                        await db.SaveChangesAsync();

                        // Cost Estimation
                        var estimates = await costService.EstimateCostsAsync(recommendations, sessionId);
                        foreach (var est in estimates)
                        {
                            db.CostEstimates.Add(est);
                        }
                        await db.SaveChangesAsync();

                        // Update session summary fields
                        backgroundSession.HasDatabase = features.Any(f => f.FeatureType == FeatureType.DbContext);
                        backgroundSession.HasAuthentication = features.Any(f => f.FeatureType == FeatureType.AuthMiddleware);
                        backgroundSession.HasFileHandling = features.Any(f => f.FeatureType == FeatureType.FileHandling);
                        backgroundSession.HasApiControllers = features.Any(f => f.FeatureType == FeatureType.Controller || f.FeatureType == FeatureType.ApiEndpoint);
                        backgroundSession.HasBackgroundServices = features.Any(f => f.FeatureType == FeatureType.BackgroundService);
                        backgroundSession.HasCaching = features.Any(f => f.FeatureName.Contains("Cache") || f.FeatureName.Contains("cache")); // or from methods if checked

                        decimal totalCost = costService.CalculateTotalMonthlyCost(estimates);
                        backgroundSession.TotalCost = totalCost;

                        // Create frontend-compatible recommendations JSON structure
                        var frontendRecs = estimates.Select(e => {
                            var rec = recommendations.FirstOrDefault(r => r.AwsService == e.ServiceName);
                            return new
                            {
                                ServiceName = e.ServiceName,
                                MonthlyCost = e.MonthlyCostUSD,
                                Justification = rec?.Reason ?? e.Justification
                            };
                        }).ToList();

                        backgroundSession.RecommendationsJson = JsonSerializer.Serialize(frontendRecs);
                        backgroundSession.Status = SessionStatus.Completed;
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Analysis pipeline failed for session {SessionId}", sessionId);
                        backgroundSession.Status = SessionStatus.Failed;
                        backgroundSession.ErrorMessage = ex.Message;
                        await db.SaveChangesAsync();
                    }
                    finally
                    {
                        // Safely cleanup temporary directories
                        if (!string.IsNullOrEmpty(extractionPath))
                        {
                            fileService.CleanUpSessionDirectory(extractionPath);
                        }
                    }
                }
            });

            return Ok(new { sessionId = sessionId, status = "Processing", message = "Analysis has been scheduled in the background." });
        }

        [HttpGet("my-sessions")]
        public async Task<IActionResult> GetMySessions()
        {
            string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var sessions = await _context.AnalysisSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UploadedAt)
                .Select(s => new
                {
                    id = s.SessionId,
                    projectName = s.ProjectName,
                    status = s.Status.ToString(),
                    totalCost = s.TotalCost,
                    analysedAt = s.UploadedAt,
                    recommendationsJson = s.RecommendationsJson
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("status/{sessionId}")]
        public async Task<IActionResult> GetStatus(Guid sessionId)
        {
            var session = await _context.AnalysisSessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found." });
            }

            // Verify Ownership
            string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool isAdmin = User.IsInRole("Admin") || User.FindFirst("isAdmin")?.Value == "true";

            if (userIdStr != session.UserId.ToString() && !isAdmin)
            {
                return StatusCode(403, new { error = "You do not have access to this session" });
            }

            return Ok(new
            {
                id = session.SessionId,
                projectName = session.ProjectName,
                analysedAt = session.UploadedAt,
                status = session.Status.ToString(),
                progressStep = session.Status == SessionStatus.Completed ? 5 : (session.Status == SessionStatus.Failed ? 5 : 2),
                errorMessage = session.ErrorMessage,
                hasDatabase = session.HasDatabase,
                hasAuthentication = session.HasAuthentication,
                hasFileHandling = session.HasFileHandling,
                hasApiControllers = session.HasApiControllers,
                hasBackgroundServices = session.HasBackgroundServices,
                hasCaching = session.HasCaching,
                totalCost = session.TotalCost,
                recommendationsJson = session.RecommendationsJson
            });
        }

        [HttpGet("results/{sessionId}")]
        public async Task<IActionResult> GetResults(Guid sessionId)
        {
            var session = await _context.AnalysisSessions
                .Include(s => s.ExtractedFeatures)
                .Include(s => s.Recommendations)
                .Include(s => s.CostEstimates)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
            {
                return NotFound(new { error = "Session not found." });
            }

            // Verify Ownership
            string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool isAdmin = User.IsInRole("Admin") || User.FindFirst("isAdmin")?.Value == "true";

            if (userIdStr != session.UserId.ToString() && !isAdmin)
            {
                return StatusCode(403, new { error = "You do not have access to this session" });
            }

            // Map features to DTO
            var featuresDto = session.ExtractedFeatures.Select(f => {
                object detailsObj;
                try
                {
                    detailsObj = JsonSerializer.Deserialize<Dictionary<string, object>>(f.Details) ?? new Dictionary<string, object>();
                }
                catch
                {
                    detailsObj = new { raw = f.Details };
                }
                return new ExtractedFeatureDto
                {
                    Type = f.FeatureType.ToString(),
                    Name = f.FeatureName,
                    FilePath = f.FilePath,
                    LineNumber = f.LineNumber,
                    Details = detailsObj
                };
            }).ToList();

            // Map recommendations to DTO
            var recommendationsDto = session.Recommendations.Select(r => new RecommendationDto
            {
                AwsService = r.AwsService,
                Category = r.ServiceCategory.ToString(),
                Priority = r.Priority.ToString(),
                Reason = r.Reason,
                TriggeringFeature = r.TriggeringFeature
            }).ToList();

            // Map costs to DTO
            var costSummaryItems = session.CostEstimates.Select(e => new CostSummaryItemDto
            {
                Service = e.ServiceName,
                Tier = e.Tier,
                MonthlyCostUSD = e.MonthlyCostUSD,
                Justification = e.Justification
            }).ToList();

            var resultDto = new AnalysisResultDto
            {
                SessionId = session.SessionId,
                ProjectName = session.ProjectName,
                Status = session.Status.ToString(),
                ErrorMessage = session.ErrorMessage,
                Features = featuresDto,
                Recommendations = recommendationsDto,
                CostSummary = new CostSummaryDto
                {
                    Items = costSummaryItems,
                    TotalMonthlyCostUSD = session.TotalCost,
                    Disclaimer = "Estimates are based on predefined pricing tiers and are approximate. Actual AWS costs depend on usage patterns."
                }
            };

            return Ok(resultDto);
        }
    }
}
