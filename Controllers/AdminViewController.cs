using Microsoft.AspNetCore.Mvc;

namespace CloudAdvisor.Controllers
{
    public class AdminViewController : Controller
    {
        [Route("admin")]
        public IActionResult Index()
        {
            return View("~/Views/Admin/Index.cshtml");
        }

        [Route("admin/pricing")]
        public IActionResult Pricing()
        {
            return View("~/Views/Admin/Pricing.cshtml");
        }

        [Route("admin/rules")]
        public IActionResult Rules()
        {
            return View("~/Views/Admin/Rules.cshtml");
        }

        [Route("admin/sessions")]
        public IActionResult Sessions()
        {
            return View("~/Views/Admin/Sessions.cshtml");
        }

        [Route("admin/users")]
        public IActionResult Users()
        {
            return View("~/Views/Admin/Users.cshtml");
        }
    }
}
