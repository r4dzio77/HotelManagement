using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Pracownik,Kierownik")]
    public class ShiftController : Controller

    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly GoogleCalendarHelper _googleCalendarHelper;

        public ShiftController(
            HotelManagementContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            GoogleCalendarHelper googleCalendarHelper)
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

        // ==================== GRAFIKI (WIELE NA MIESIĄC) ====================

        [Authorize(Roles = "Kierownik")]
        public async Task<IActionResult> ManageSchedule(DateTime? month, int? scheduleId)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var firstDay = month.HasValue
                ? new DateTime(month.Value.Year, month.Value.Month, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            int year = firstDay.Year;
            int m = firstDay.Month;

            // wszystkie grafiki dla miesiąca
            var schedulesForMonth = await _context.WorkSchedules
                .Include(s => s.CreatedBy)
                .Where(s => s.Year == year && s.Month == m)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            WorkSchedule? activeSchedule = null;

            if (scheduleId.HasValue)
            {
                activeSchedule = schedulesForMonth.FirstOrDefault(s => s.Id == scheduleId.Value);
            }

            if (activeSchedule == null)
            {
                activeSchedule = schedulesForMonth.FirstOrDefault();
            }

            // jeśli nie ma jeszcze żadnego grafiku dla tego miesiąca → tworzymy domyślny
            if (activeSchedule == null)
            {
                activeSchedule = new WorkSchedule
                {
                    Year = year,
                    Month = m,
                    Name = $"Grafik {firstDay:MMMM yyyy}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = currentUser?.Id
                };

                _context.WorkSchedules.Add(activeSchedule);
                await _context.SaveChangesAsync();

                schedulesForMonth.Insert(0, activeSchedule);
            }

            int activeScheduleId = activeSchedule.Id;

            var shifts = await _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.WorkScheduleId == activeScheduleId)
                .OrderBy(ws => ws.Date)
                .ThenBy(ws => ws.StartTime)
                .ToListAsync();

            var employees = await _userManager.GetUsersInRoleAsync("Pracownik");
            ViewBag.Employees = employees;
            ViewBag.Month = firstDay;
            ViewBag.IsPublished = activeSchedule.IsPublished;
            ViewBag.Schedules = schedulesForMonth;
            ViewBag.ActiveScheduleId = activeScheduleId;
            ViewBag.ActiveScheduleName = activeSchedule.Name;

            return View("ManageSchedule", shifts);
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(DateTime month, string name)
        {
            var user = await _userManager.GetUserAsync(User);

            if (string.IsNullOrWhiteSpace(name))
                name = $"Grafik {month:MMMM yyyy}";

            var schedule = new WorkSchedule
            {
                Year = month.Year,
                Month = month.Month,
                Name = name.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = user?.Id
            };

            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            var firstDay = new DateTime(month.Year, month.Month, 1);

            return RedirectToAction("ManageSchedule", new
            {
                month = firstDay.ToString("yyyy-MM-01"),
                scheduleId = schedule.Id
            });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuplicateSchedule(int scheduleId, string? name)
        {
            var original = await _context.WorkSchedules
                .Include(s => s.Shifts)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (original == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (string.IsNullOrWhiteSpace(name))
                name = original.Name + " – kopia";

            var newSchedule = new WorkSchedule
            {
                Year = original.Year,
                Month = original.Month,
                Name = name.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = user?.Id
            };

            _context.WorkSchedules.Add(newSchedule);
            await _context.SaveChangesAsync(); // potrzebujemy Id

            // kopiowanie zmian
            foreach (var s in original.Shifts)
            {
                var copy = new WorkShift
                {
                    UserId = s.UserId,
                    Date = s.Date,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    WorkScheduleId = newSchedule.Id,
                    GoogleEventId = null
                };
                _context.WorkShifts.Add(copy);
            }

            await _context.SaveChangesAsync();

            var firstDay = new DateTime(original.Year, original.Month, 1);

            return RedirectToAction("ManageSchedule", new
            {
                month = firstDay.ToString("yyyy-MM-01"),
                scheduleId = newSchedule.Id
            });
        }

        // ZMIANA NAZWY GRAFIKU
        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameSchedule(int scheduleId, string name)
        {
            var schedule = await _context.WorkSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                TempData["Error"] = "Nie znaleziono grafiku.";
                return RedirectToAction("ManageSchedule");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Nazwa nie może być pusta.";
                return RedirectToAction("ManageSchedule", new
                {
                    month = $"{schedule.Year}-{schedule.Month:00}-01",
                    scheduleId
                });
            }

            schedule.Name = name.Trim();
            await _context.SaveChangesAsync();

            TempData["Message"] = "Nazwa grafiku została zmieniona.";
            return RedirectToAction("ManageSchedule", new
            {
                month = $"{schedule.Year}-{schedule.Month:00}-01",
                scheduleId
            });
        }

        // USUWANIE GRAFIKU
        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int scheduleId)
        {
            var schedule = await _context.WorkSchedules
                .Include(s => s.Shifts)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
            {
                TempData["Error"] = "Grafik nie istnieje.";
                return RedirectToAction("ManageSchedule");
            }

            if (schedule.IsPublished)
            {
                TempData["Error"] = "Nie można usunąć opublikowanego grafiku.";
                return RedirectToAction("ManageSchedule", new
                {
                    month = $"{schedule.Year}-{schedule.Month:00}-01",
                    scheduleId
                });
            }

            int year = schedule.Year;
            int month = schedule.Month;

            // usuwamy wszystkie zmiany tego grafiku
            _context.WorkShifts.RemoveRange(schedule.Shifts);

            // usuwamy sam grafik
            _context.WorkSchedules.Remove(schedule);
            await _context.SaveChangesAsync();

            // znajdź inny grafik dla tego miesiąca
            var replacement = await _context.WorkSchedules
                .Where(s => s.Year == year && s.Month == month)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            TempData["Message"] = "Grafik został usunięty.";

            return RedirectToAction("ManageSchedule", new
            {
                month = $"{year}-{month:00}-01",
                scheduleId = replacement?.Id
            });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignShift(DateTime date, string userId, string startTime, string endTime, int scheduleId)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(startTime) ||
                string.IsNullOrWhiteSpace(endTime))
            {
                TempData["Error"] = "Wszystkie pola są wymagane.";
                return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01"), scheduleId });
            }

            if (!TimeSpan.TryParse(startTime, out var start) ||
                !TimeSpan.TryParse(endTime, out var end))
            {
                TempData["Error"] = "Nieprawidłowy format godzin.";
                return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01"), scheduleId });
            }

            if (start >= end)
            {
                TempData["Error"] = "Godzina zakończenia musi być późniejsza niż rozpoczęcia.";
                return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01"), scheduleId });
            }

            // sprawdź nakładanie się zmian danego pracownika w tym grafiku i dniu
            var overlaps = await _context.WorkShifts
                .AnyAsync(ws =>
                    ws.WorkScheduleId == scheduleId &&
                    ws.Date.Date == date.Date &&
                    ws.UserId == userId &&
                    ws.StartTime < end &&
                    start < ws.EndTime);

            if (overlaps)
            {
                TempData["Error"] = "Ta zmiana nachodzi na inną zmianę tego pracownika w tym dniu.";
                return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01"), scheduleId });
            }

            var shift = new WorkShift
            {
                Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
                UserId = userId,
                StartTime = start,
                EndTime = end,
                WorkScheduleId = scheduleId
            };

            _context.WorkShifts.Add(shift);
            await _context.SaveChangesAsync();

            return RedirectToAction("ManageSchedule", new { month = date.ToString("yyyy-MM-01"), scheduleId });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShift(int id, string userId, string startTime, string endTime)
        {
            var shift = await _context.WorkShifts
                .Include(ws => ws.WorkSchedule)
                .FirstOrDefaultAsync(ws => ws.Id == id);

            if (shift == null) return NotFound();

            var scheduleId = shift.WorkScheduleId;
            var monthStr = new DateTime(shift.WorkSchedule.Year, shift.WorkSchedule.Month, 1).ToString("yyyy-MM-01");

            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(startTime) ||
                string.IsNullOrWhiteSpace(endTime))
            {
                TempData["Error"] = "Wszystkie pola są wymagane.";
                return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
            }

            if (!TimeSpan.TryParse(startTime, out var start) ||
                !TimeSpan.TryParse(endTime, out var end))
            {
                TempData["Error"] = "Nieprawidłowy format godzin.";
                return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
            }

            if (start >= end)
            {
                TempData["Error"] = "Godzina zakończenia musi być późniejsza niż rozpoczęcia.";
                return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
            }

            // sprawdź nakładanie się (z pominięciem tej zmiany)
            var overlaps = await _context.WorkShifts
                .AnyAsync(ws =>
                    ws.Id != shift.Id &&
                    ws.WorkScheduleId == scheduleId &&
                    ws.Date.Date == shift.Date.Date &&
                    ws.UserId == userId &&
                    ws.StartTime < end &&
                    start < ws.EndTime);

            if (overlaps)
            {
                TempData["Error"] = "Ta zmiana nachodzi na inną zmianę tego pracownika w tym dniu.";
                return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
            }

            shift.UserId = userId;
            shift.StartTime = start;
            shift.EndTime = end;

            _context.WorkShifts.Update(shift);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Zmieniono zmianę.";
            return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var shift = await _context.WorkShifts
                .Include(ws => ws.WorkSchedule)
                .FirstOrDefaultAsync(ws => ws.Id == id);

            if (shift != null)
            {
                var scheduleId = shift.WorkScheduleId;
                var monthStr = new DateTime(shift.WorkSchedule.Year, shift.WorkSchedule.Month, 1).ToString("yyyy-MM-01");

                _context.WorkShifts.Remove(shift);
                await _context.SaveChangesAsync();

                return RedirectToAction("ManageSchedule", new { month = monthStr, scheduleId });
            }

            return RedirectToAction("ManageSchedule");
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishSchedule(int scheduleId)
        {
            var schedule = await _context.WorkSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId);
            if (schedule == null) return NotFound();

            // tylko jeden opublikowany grafik na miesiąc
            var others = await _context.WorkSchedules
                .Where(s => s.Year == schedule.Year && s.Month == schedule.Month && s.Id != schedule.Id)
                .ToListAsync();

            foreach (var s in others)
            {
                s.IsPublished = false;
            }

            schedule.IsPublished = true;

            await _context.SaveChangesAsync();

            var monthDate = new DateTime(schedule.Year, schedule.Month, 1);
            return RedirectToAction("ManageSchedule", new
            {
                month = monthDate.ToString("yyyy-MM-01"),
                scheduleId = schedule.Id
            });
        }

        // ==================== MÓJ GRAFIK ====================

        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> MySchedule(DateTime? month, bool all = false, int? scheduleId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var target = month ?? DateTime.Today;
            var firstDay = new DateTime(target.Year, target.Month, 1);
            int year = firstDay.Year;
            int m = firstDay.Month;

            var publishedSchedules = await _context.WorkSchedules
                .Where(s => s.Year == year && s.Month == m && s.IsPublished)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            WorkSchedule? activeSchedule = null;

            if (scheduleId.HasValue)
            {
                activeSchedule = publishedSchedules.FirstOrDefault(s => s.Id == scheduleId.Value);
            }

            if (activeSchedule == null)
            {
                activeSchedule = publishedSchedules.FirstOrDefault();
            }

            bool isPublished = activeSchedule != null;

            ViewBag.Month = firstDay;
            ViewBag.IsPublished = isPublished;
            ViewBag.ShowAll = all;
            ViewBag.ScheduleId = activeSchedule?.Id;
            ViewBag.ScheduleName = activeSchedule?.Name;
            ViewBag.Schedules = publishedSchedules;

            if (!isPublished)
            {
                return View("MySchedule", new List<WorkShift>());
            }

            int activeScheduleId = activeSchedule.Id;

            IQueryable<WorkShift> query = _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.WorkScheduleId == activeScheduleId);

            if (!all)
            {
                query = query.Where(ws => ws.UserId == user.Id);
            }

            var shifts = await query
                .OrderBy(ws => ws.Date)
                .ThenBy(ws => ws.StartTime)
                .ToListAsync();

            return View("MySchedule", shifts);
        }

        // ==================== EKSPORT DO GOOGLE CALENDAR ====================

        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> ExportMyShiftsToGoogle(DateTime? month, int? scheduleId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (string.IsNullOrEmpty(user.GoogleId) || string.IsNullOrEmpty(user.GoogleAccessToken))
            {
                TempData["Error"] = "Musisz połączyć swoje konto Google, aby eksportować grafik.";
                return RedirectToAction("LinkGoogle", "Account", new { returnUrl = Url.Action("ExportMyShiftsToGoogle", new { month, scheduleId }) });
            }

            if (user.GoogleTokenExpiry.HasValue && user.GoogleTokenExpiry.Value <= DateTime.UtcNow)
            {
                var refreshed = await _googleCalendarHelper.RefreshAccessTokenAsync(user);
                if (!refreshed)
                {
                    TempData["Error"] = "Sesja Google wygasła, połącz konto ponownie.";
                    return RedirectToAction("LinkGoogle", "Account", new { returnUrl = Url.Action("ExportMyShiftsToGoogle", new { month, scheduleId }) });
                }
                await _userManager.UpdateAsync(user);
            }

            var target = month ?? DateTime.Today;
            var firstDay = new DateTime(target.Year, target.Month, 1);
            int year = firstDay.Year;
            int m = firstDay.Month;

            var publishedSchedules = await _context.WorkSchedules
                .Where(s => s.Year == year && s.Month == m && s.IsPublished)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            WorkSchedule? activeSchedule = null;

            if (scheduleId.HasValue)
            {
                activeSchedule = publishedSchedules.FirstOrDefault(s => s.Id == scheduleId.Value);
            }

            if (activeSchedule == null)
            {
                activeSchedule = publishedSchedules.FirstOrDefault();
            }

            if (activeSchedule == null)
            {
                TempData["Error"] = "Brak opublikowanego grafiku dla wybranego miesiąca.";
                return RedirectToAction("MySchedule", new { month });
            }

            int activeScheduleId = activeSchedule.Id;

            var shifts = await _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.UserId == user.Id && ws.WorkScheduleId == activeScheduleId)
                .OrderBy(ws => ws.Date)
                .ThenBy(ws => ws.StartTime)
                .ToListAsync();

            var success = await _googleCalendarHelper.SyncShiftsAsync(user, shifts, user.GoogleAccessToken!);

            if (!success)
            {
                TempData["Error"] = "Niektóre zmiany nie zostały zapisane w Google Calendar. Sprawdź logi systemowe.";
            }
            else
            {
                TempData["Message"] = "Twój grafik został zsynchronizowany z Google Calendar ✅";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MySchedule", new { month, scheduleId });
        }
    }
}
