using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class ShiftController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShiftController(HotelManagementContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Kierownik")]
        public async Task<IActionResult> ManageSchedule(DateTime? month)
        {
            var firstDay = month.HasValue
                ? new DateTime(month.Value.Year, month.Value.Month, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            var lastDay = firstDay.AddMonths(1);

            var shifts = await _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.Date >= firstDay && ws.Date < lastDay)
                .ToListAsync();

            var employees = await _userManager.GetUsersInRoleAsync("Pracownik");
            ViewBag.Employees = employees;
            ViewBag.Month = firstDay;

            var isPublished = await _context.PublishedSchedules
                .AnyAsync(p => p.Year == firstDay.Year && p.Month == firstDay.Month && p.IsPublished);
            ViewBag.IsPublished = isPublished;

            return View("ManageSchedule", shifts);
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignShift(DateTime date, string shiftType, string userId)
        {
            if (string.IsNullOrEmpty(shiftType) || string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("", "Wszystkie pola są wymagane.");
            }

            var exists = await _context.WorkShifts
                .AnyAsync(ws => ws.Date.Date == date.Date && ws.ShiftType == shiftType && ws.UserId == userId);

            if (exists)
            {
                ModelState.AddModelError("", "Ten pracownik jest już przypisany do tej zmiany.");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01") });
            }

            var shift = new WorkShift
            {
                Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
                ShiftType = shiftType,
                UserId = userId
            };

            _context.WorkShifts.Add(shift);
            await _context.SaveChangesAsync();

            return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01") });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var shift = await _context.WorkShifts.FindAsync(id);
            if (shift != null)
            {
                _context.WorkShifts.Remove(shift);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageSchedule", new { month = shift?.Date.ToString("yyyy-MM-01") });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishSchedule(int year, int month)
        {
            var already = await _context.PublishedSchedules
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month);

            if (already == null)
            {
                _context.PublishedSchedules.Add(new PublishedSchedule
                {
                    Year = year,
                    Month = month,
                    IsPublished = true,
                    PublishedAt = DateTime.UtcNow
                });
            }
            else
            {
                already.IsPublished = true;
                already.PublishedAt = DateTime.UtcNow;
                _context.PublishedSchedules.Update(already);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("ManageSchedule", new { month = new DateTime(year, month, 1).ToString("yyyy-MM-01") });
        }

        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> MySchedule(DateTime? month)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var target = month ?? DateTime.Today;
            var firstDay = new DateTime(target.Year, target.Month, 1);
            var lastDay = firstDay.AddMonths(1);

            var isPublished = await _context.PublishedSchedules
                .AnyAsync(p => p.Year == firstDay.Year && p.Month == firstDay.Month && p.IsPublished);

            if (!isPublished)
            {
                ViewBag.Month = firstDay;
                ViewBag.IsPublished = false;
                return View("MySchedule", new List<WorkShift>());
            }

            var shifts = await _context.WorkShifts
                .Where(ws => ws.UserId == user.Id && ws.Date >= firstDay && ws.Date < lastDay)
                .ToListAsync();

            ViewBag.Month = firstDay;
            ViewBag.IsPublished = true;
            return View("MySchedule", shifts);
        }
    }
}
