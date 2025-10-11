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
    public class ShiftController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly GoogleCalendarHelper _googleCalendarHelper; // ✅ helper

        public ShiftController(
            HotelManagementContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            GoogleCalendarHelper googleCalendarHelper) // ✅ DI
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _googleCalendarHelper = googleCalendarHelper;
        }

        // ==================== ZARZĄDZANIE PRACOWNIKAMI ====================

        [Authorize(Roles = "Kierownik")]
        public async Task<IActionResult> Employees()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Pracownik");
            return View(employees);
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(string firstName, string lastName, string email, string password)
        {
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Wszystkie pola są wymagane.";
                return RedirectToAction("Employees");
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Error"] = "Użytkownik z tym adresem e-mail już istnieje.";
                return RedirectToAction("Employees");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Pracownik"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Pracownik"));
                }

                await _userManager.AddToRoleAsync(user, "Pracownik");
                TempData["Message"] = "Dodano nowego pracownika.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Employees");
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                TempData["Message"] = "Pracownik został usunięty.";
            }
            return RedirectToAction("Employees");
        }

        // ==================== GRAFIK ====================

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
        public async Task<IActionResult> EditShift(int id)
        {
            var shift = await _context.WorkShifts.FindAsync(id);
            if (shift == null) return NotFound();

            var employees = await _userManager.GetUsersInRoleAsync("Pracownik");
            ViewBag.Employees = employees;
            ViewBag.ShiftToEdit = shift;
            return RedirectToAction("ManageSchedule", new { month = shift.Date.ToString("yyyy-MM-01") });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShift(int id, string shiftType, string userId)
        {
            var shift = await _context.WorkShifts.FindAsync(id);
            if (shift == null) return NotFound();

            shift.ShiftType = shiftType;
            shift.UserId = userId;

            _context.WorkShifts.Update(shift);
            await _context.SaveChangesAsync();

            return RedirectToAction("ManageSchedule", new { month = shift.Date.ToString("yyyy-MM-01") });
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
            var record = await _context.PublishedSchedules
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month);

            if (record == null)
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
                record.IsPublished = true;
                record.PublishedAt = DateTime.UtcNow;
                _context.PublishedSchedules.Update(record);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("ManageSchedule", new { month = new DateTime(year, month, 1).ToString("yyyy-MM-01") });
        }

        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> MySchedule(DateTime? month, bool all = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var target = month ?? DateTime.Today;
            var firstDay = new DateTime(target.Year, target.Month, 1);
            var lastDay = firstDay.AddMonths(1);

            var isPublished = await _context.PublishedSchedules
                .AnyAsync(p => p.Year == firstDay.Year && p.Month == firstDay.Month && p.IsPublished);

            ViewBag.Month = firstDay;
            ViewBag.IsPublished = isPublished;
            ViewBag.ShowAll = all;

            if (!isPublished)
            {
                return View("MySchedule", new List<WorkShift>());
            }

            IQueryable<WorkShift> query = _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.Date >= firstDay && ws.Date < lastDay);

            if (!all)
            {
                query = query.Where(ws => ws.UserId == user.Id);
            }

            var shifts = await query.OrderBy(ws => ws.Date).ThenBy(ws => ws.ShiftType).ToListAsync();
            return View("MySchedule", shifts);
        }

        // ==================== EKSPORT DO GOOGLE CALENDAR ====================

        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> ExportMyShiftsToGoogle(DateTime? month)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // 1. Brak podpiętego konta Google → przekieruj do Account/LinkGoogle
            if (string.IsNullOrEmpty(user.GoogleId) || string.IsNullOrEmpty(user.GoogleAccessToken))
            {
                TempData["Error"] = "Musisz połączyć swoje konto Google, aby eksportować grafik.";
                return RedirectToAction("LinkGoogle", "Account", new { returnUrl = Url.Action("ExportMyShiftsToGoogle", new { month }) });
            }

            // 2. Obsługa wygaśnięcia tokena
            if (user.GoogleTokenExpiry.HasValue && user.GoogleTokenExpiry.Value <= DateTime.UtcNow)
            {
                var refreshed = await _googleCalendarHelper.RefreshAccessTokenAsync(user);
                if (!refreshed)
                {
                    TempData["Error"] = "Sesja Google wygasła, połącz konto ponownie.";
                    return RedirectToAction("LinkGoogle", "Account", new { returnUrl = Url.Action("ExportMyShiftsToGoogle", new { month }) });
                }
                await _userManager.UpdateAsync(user);
            }

            // 3. Pobierz grafik i wyślij do Google Calendar
            var target = month ?? DateTime.Today;
            var firstDay = new DateTime(target.Year, target.Month, 1);
            var lastDay = firstDay.AddMonths(1);

            var shifts = await _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.UserId == user.Id && ws.Date >= firstDay && ws.Date < lastDay)
                .ToListAsync();

            var success = await _googleCalendarHelper.SyncShiftsAsync(user, shifts, user.GoogleAccessToken);

            if (!success)
            {
                TempData["Error"] = "Niektóre zmiany nie zostały zapisane w Google Calendar. Sprawdź logi systemowe.";
            }
            else
            {
                TempData["Message"] = "Twój grafik został zsynchronizowany z Google Calendar ✅";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MySchedule", new { month });
        }
    }
}
