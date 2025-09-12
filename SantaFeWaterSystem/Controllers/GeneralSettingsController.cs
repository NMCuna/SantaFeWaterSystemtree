using Microsoft.AspNetCore.Mvc;

namespace SantaFeWaterSystem.Controllers
{
    public class GeneralSettingsController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult Security() => View();
        public IActionResult Policies() => View();
        public IActionResult System() => View();

    }
}
