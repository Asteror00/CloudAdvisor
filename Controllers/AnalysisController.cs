using System;
using System.Threading.Tasks;
using CloudAdvisor.Data;
using CloudAdvisor.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CloudAdvisor.Controllers
{
    public class AnalysisController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnalysisController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /upload
        [HttpGet]
        [Route("upload")]
        public IActionResult Upload()
        {
            return View(new UploadViewModel());
        }

        // GET: /analyzing/{id}
        [HttpGet]
        [Route("analyzing/{id}")]
        public IActionResult Analyzing(Guid id)
        {
            ViewBag.HistoryId = id;
            return View();
        }

        // GET: /results/{id}
        [HttpGet]
        [Route("results/{id}")]
        public async Task<IActionResult> Results(Guid id)
        {
            var session = await _context.AnalysisSessions.FindAsync(id);
            if (session == null)
            {
                return NotFound($"Analysis session with ID {id} was not found.");
            }

            var viewModel = new ResultsViewModel
            {
                ProjectName = session.ProjectName,
                AnalysedAt = session.UploadedAt,
                HasDatabase = session.HasDatabase,
                HasAuthentication = session.HasAuthentication,
                HasFileHandling = session.HasFileHandling,
                HasApiControllers = session.HasApiControllers,
                HasBackgroundServices = session.HasBackgroundServices,
                HasCaching = session.HasCaching,
                TotalMonthlyCost = session.TotalCost,
                TotalAnnualCost = session.TotalCost * 12,
                ExtractedFilesCount = 0,
                AnalysisTimeMs = 0
            };

            ViewBag.HistoryId = id;
            return View(viewModel);
        }
    }
}
