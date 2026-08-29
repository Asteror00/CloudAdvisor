using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CloudAdvisor.Data;
using CloudAdvisor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudAdvisor.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/report")]
    public class ReportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportGenerationService _reportService;

        public ReportController(ApplicationDbContext context, IReportGenerationService reportService)
        {
            _context = context;
            _reportService = reportService;
        }

        [HttpGet("{sessionId}")]
        [HttpGet("/api/project/report/{sessionId}")]
        public async Task<IActionResult> DownloadReport(Guid sessionId)
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

            try
            {
                byte[] pdfBytes = await _reportService.GeneratePdfReportAsync(session);
                
                string safeProjectName = string.Join("_", session.ProjectName.Split(System.IO.Path.GetInvalidFileNameChars()));
                string fileName = $"CloudAdvisor_Report_{safeProjectName}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to generate PDF report: {ex.Message}" });
            }
        }
    }
}
