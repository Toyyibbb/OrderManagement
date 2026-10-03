using Microsoft.AspNetCore.Mvc;
using Tes.Models;
using Tes.Service;

namespace Tes.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ProfileService _service;

        public ProfileController(ProfileService service)
        {
            _service = service;
        }

        public IActionResult Index()
        {
            var data = _service.GetProfile();
            return View(data);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Profile profile)
        {
            _service.InsertProfile(profile);
            return RedirectToAction("Index");
        }
    }
}
