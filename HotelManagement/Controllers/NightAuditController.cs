using System.Threading.Tasks;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Kierownik")]
    public class NightAuditController : Controller
    {
        private readonly NightAuditService _auditService;
        private readonly IBusinessDateProvider _businessDate;

        public NightAuditController(NightAuditService auditService, IBusinessDateProvider businessDate)
        {
            _auditService = auditService;
            _businessDate = businessDate;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var date = await _businessDate.GetCurrentBusinessDateAsync();
            ViewData["BusinessDate"] = date.ToString("yyyy-MM-dd");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run()
        {
            var userId = User?.Identity?.Name;
            await _auditService.RunAsync(userId);
            return RedirectToAction(nameof(Index));
        }
    }
}
