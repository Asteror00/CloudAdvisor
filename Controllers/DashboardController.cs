using Microsoft.AspNetCore.Mvc;

namespace CloudAdvisor.Controllers
{
    public class DashboardController : Controller
    {
        [Route("dashboard")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
