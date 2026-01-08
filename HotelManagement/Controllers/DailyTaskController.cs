using System;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class DailyTaskController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;
        private readonly UserManager<ApplicationUser> _userManager;

        public DailyTaskController(
            HotelManagementContext context,
            IBusinessDateProvider businessDate,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _businessDate = businessDate;
            _userManager = userManager;
        }

        // =========================
        // ➕ DODANIE ZADANIA (KIEROWNIK)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik")]
        public async Task<IActionResult> Add(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return RedirectToAction("Index", "Dashboard");

            var businessDate = (await _businessDate.GetCurrentBusinessDateAsync()).Date;

            var task = new DailyTask
            {
                Title = title.Trim(),
                BusinessDate = businessDate,
                IsCompleted = false
            };

            _context.DailyTasks.Add(task);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Dashboard");
        }

        // =========================
        // ✅ OZNACZ JAKO WYKONANE (PRACOWNIK / KIEROWNIK)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> ToggleComplete(int id)
        {
            var task = await _context.DailyTasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);

            task.IsCompleted = !task.IsCompleted;

            if (task.IsCompleted)
            {
                task.CompletedByUserId = user.Id;
                task.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                task.CompletedByUserId = null;
                task.CompletedAt = null;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
