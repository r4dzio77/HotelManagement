using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Kierownik")]
    public class EmployeesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeesController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Lista zwykłych pracowników (recepcja).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Jedyna rola pracownika w systemie
            var employeeRoleNames = new[]
            {
                "Pracownik"
            };

            var employees = new List<ApplicationUser>();

            foreach (var roleName in employeeRoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                    continue;

                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);

                foreach (var u in usersInRole)
                {
                    if (employees.All(e => e.Id != u.Id))
                    {
                        employees.Add(u);
                    }
                }
            }

            var ordered = employees
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToList();

            return View(ordered);
        }

        /// <summary>
        /// Lista kierowników (w praktyce: tylko Kierownik).
        /// </summary>
        public async Task<IActionResult> Managers()
        {
            var managerRoleNames = new[]
            {
                "Kierownik"
            };

            var managers = new List<ApplicationUser>();

            foreach (var roleName in managerRoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                    continue;

                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);

                foreach (var u in usersInRole)
                {
                    if (managers.All(e => e.Id != u.Id))
                    {
                        managers.Add(u);
                    }
                }
            }

            var ordered = managers
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToList();

            return View(ordered);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(
            string firstName,
            string lastName,
            string email,
            string password,
            string department)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Wszystkie pola formularza są wymagane.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Error"] = "Użytkownik z podanym adresem e-mail już istnieje.";
                return RedirectToAction(nameof(Index));
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim(),
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                TempData["Error"] = "Nie udało się dodać pracownika: " +
                                    string.Join(", ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // upewniamy się, że rola "Pracownik" istnieje
            if (!await _roleManager.RoleExistsAsync("Pracownik"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Pracownik"));
            }

            // nowy pracownik zawsze ma rolę "Pracownik"
            await _userManager.AddToRoleAsync(user, "Pracownik");

            TempData["Message"] = "Pracownik został dodany.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(
            string id,
            string firstName,
            string lastName,
            string department)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Niepoprawne ID pracownika.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Nie znaleziono wskazanego pracownika.";
                return RedirectToAction(nameof(Index));
            }

            user.FirstName = (firstName ?? "").Trim();
            user.LastName = (lastName ?? "").Trim();
            user.Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Nie udało się zapisać zmian: " +
                                    string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Message"] = "Dane pracownika zostały zaktualizowane.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Niepoprawne ID pracownika.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Nie znaleziono wskazanego pracownika.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Nie udało się usunąć pracownika: " +
                                    string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Message"] = "Pracownik został usunięty.";
            return RedirectToAction(nameof(Index));
        }
    }
}
