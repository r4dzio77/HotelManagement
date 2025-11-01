using System;
using System.Threading.Tasks;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using HotelManagement.Models;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Admin,Kierownik,Pracownik")]
    public class NightAuditController : Controller
    {
        private readonly NightAuditService _audit;
        private readonly NightAuditProgressStore _progress;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBusinessDateProvider _businessDate;

        public NightAuditController(
            NightAuditService audit,
            NightAuditProgressStore progress,
            UserManager<ApplicationUser> userManager,
            IBusinessDateProvider businessDate)
        {
            _audit = audit;
            _progress = progress;
            _userManager = userManager;
            _businessDate = businessDate;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start()
        {
            var user = await _userManager.GetUserAsync(User);
            var id = _audit.StartAudit(user?.Id);
            return Json(new { auditId = id });
        }

        [HttpGet]
        public IActionResult Progress(Guid id)
        {
            var ap = _progress.Get(id);
            if (ap == null) return NotFound();
            return Json(new
            {
                ap.Id,
                ap.Percent,
                ap.CurrentStep,
                ap.Steps,
                ap.Messages,
                ap.IsCompleted,
                ap.IsSuccess,
                ap.ReportPath
            });
        }

        // 🆕 RĘCZNA synchronizacja daty operacyjnej z dzisiejszą (dla adminów/kierowników/pracowników)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncToToday()
        {
            var user = await _userManager.GetUserAsync(User);
            await _businessDate.SetCurrentBusinessDateAsync(DateTime.Today, user?.Id);
            TempData["Notification"] = "Data operacyjna została ustawiona na dzisiejszą.";
            return RedirectToAction(nameof(Index));
        }
    }
}
