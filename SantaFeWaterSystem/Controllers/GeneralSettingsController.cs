using Microsoft.AspNetCore.Mvc;

namespace SantaFeWaterSystem.Controllers
{
    public class GeneralSettingsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
