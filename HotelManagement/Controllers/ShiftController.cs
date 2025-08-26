using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class ShiftController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HotelManagementContext _context;
        private readonly ICompositeViewEngine _viewEngine;

        // TYLKO JEDEN KONSTRUKTOR
        public ShiftController(UserManager<ApplicationUser> userManager, HotelManagementContext context, ICompositeViewEngine viewEngine)
        {
            _userManager = userManager;
            _context = context;
            _viewEngine = viewEngine;
        }

        [Authorize(Roles = "Pracownik,Kierownik")]
        [HttpGet]
        public async Task<IActionResult> MyAvailability()
        {
            var user = await _userManager.GetUserAsync(User);
            var preferences = await _context.ShiftPreferences
                .Where(sp => sp.UserId == user.Id)
                .OrderBy(sp => sp.Date)
                .ToListAsync();

            return View("~/Views/Shift/MyAvailability.cshtml", preferences);
        }

        [Authorize(Roles = "Pracownik,Kierownik")]
        [HttpPost]
        public async Task<IActionResult> UpdatePreference(DateTime date, bool cannotWorkDay, bool cannotWorkNight)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var preference = await _context.ShiftPreferences
                .FirstOrDefaultAsync(sp => sp.UserId == user.Id && sp.Date.Date == date.Date);

            if (preference == null)
            {
                preference = new ShiftPreference
                {
                    UserId = user.Id,
                    Date = date,
                    CannotWorkDay = cannotWorkDay,
                    CannotWorkNight = cannotWorkNight
                };
                _context.ShiftPreferences.Add(preference);
            }
            else
            {
                preference.CannotWorkDay = cannotWorkDay;
                preference.CannotWorkNight = cannotWorkNight;
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "Dyspozycja została zapisana.";

            return RedirectToAction(nameof(MyAvailability));
        }

        [Authorize(Roles = "Kierownik")]
        public async Task<IActionResult> ManageShifts()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Pracownik");
            var preferences = await _context.ShiftPreferences
                .Include(sp => sp.User)
                .ToListAsync();

            var model = new ManageShiftsViewModel
            {
                Employees = employees,
                Preferences = preferences
            };

            return View("~/Views/Shift/ManageShifts.cshtml", model);
        }

        [Authorize(Roles = "Kierownik")]
        [HttpGet]
        public IActionResult AddEmployee()
        {
            return View("~/Views/Shift/AddEmployee.cshtml");
        }

        [Authorize(Roles = "Kierownik")]
        [HttpPost]
        public async Task<IActionResult> AddEmployee(AddEmployeeViewModel model)
        {
            if (!ModelState.IsValid) return View("~/Views/Shift/AddEmployee.cshtml", model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View("~/Views/Shift/AddEmployee.cshtml", model);
            }

            await _userManager.AddToRoleAsync(user, "Pracownik");
            TempData["Message"] = "Pracownik został dodany.";
            return RedirectToAction(nameof(ManageShifts));
        }

        // Tymczasowa metoda testowa widoków
        [Authorize(Roles = "Kierownik")]
        [HttpGet]
        public IActionResult TestViewDetection()
        {
            var views = new string[]
            {
                "~/Views/Shift/MyAvailability.cshtml",
                "~/Views/Shift/ManageShifts.cshtml",
                "~/Views/Shift/AddEmployee.cshtml"
            };

            var results = new List<string>();

            foreach (var viewPath in views)
            {
                var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
                if (viewResult.Success)
                {
                    results.Add($"{viewPath} – znaleziono!");
                }
                else
                {
                    results.Add($"{viewPath} – NIE znaleziono!");
                }
            }

            return Content(string.Join("\n", results));
        }
    }
}
