using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class AccountSettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HotelManagementContext _context;

        public AccountSettingsController(UserManager<ApplicationUser> userManager, HotelManagementContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /AccountSettings (profil użytkownika)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new AccountSettingsViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Preferences = user.Preferences
            };

            return View(model);
        }

        // GET: /AccountSettings/Loyalty (program lojalnościowy)
        [HttpGet]
        public async Task<IActionResult> Loyalty()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.GuestId.HasValue) return NotFound();

            var guest = await _context.Guests
                .FirstOrDefaultAsync(g => g.Id == user.GuestId.Value);

            if (guest == null) return NotFound();

            var model = new LoyaltyViewModel
            {
                LoyaltyCardNumber = guest.LoyaltyCardNumber,
                LoyaltyStatus = guest.LoyaltyStatus.ToString(),
                LoyaltyPoints = await _context.LoyaltyPoints
                    .Where(lp => lp.GuestId == guest.Id)
                    .SumAsync(lp => lp.Points),
                TotalNights = guest.TotalNights,
                History = await _context.LoyaltyPoints
                    .Where(lp => lp.GuestId == guest.Id)
                    .OrderByDescending(lp => lp.AwardedAt)
                    .ToListAsync()
            };

            return View(model);
        }

        // POST: Aktualizacja telefonu z modal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePhone(AccountSettingsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            TempData["Message"] = "Telefon został zaktualizowany";
            return RedirectToAction(nameof(Index));
        }

        // POST: Aktualizacja preferencji z modal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePreferences(AccountSettingsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.Preferences = model.Preferences;
            await _userManager.UpdateAsync(user);

            TempData["Message"] = "Preferencje zostały zaktualizowane";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AccountSettings/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /AccountSettings/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            TempData["Message"] = "Hasło zostało zmienione pomyślnie.";
            return RedirectToAction(nameof(Index));
        }
    }
}
