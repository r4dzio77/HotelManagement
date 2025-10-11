using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly HotelManagementContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            HotelManagementContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _context = context;
        }

        // ================= LOGIN =================

        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Email i hasło są wymagane.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(email, password, false, false);

            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło.");
            return View();
        }

        // ================= LOGIN GOOGLE =================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                TempData["Error"] = $"Błąd logowania zewnętrznego: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction(nameof(Login));

            // Logowanie użytkownika jeśli istnieje
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

            ApplicationUser user;

            if (signInResult.Succeeded)
            {
                user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            }
            else
            {
                // Nowy użytkownik – tworzymy
                var email = info.Principal.FindFirst(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
                user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = info.Principal.Identity?.Name?.Split(' ').FirstOrDefault() ?? "",
                        LastName = info.Principal.Identity?.Name?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                        EmailConfirmed = true
                    };

                    await _userManager.CreateAsync(user);
                    await _userManager.AddToRoleAsync(user, "Pracownik"); // domyślna rola
                }

                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
            }

            // 🔑 Pobieramy tokeny Google i zapisujemy w ApplicationUser
            var tokens = info.AuthenticationTokens?.ToList();
            if (tokens != null)
            {
                user.GoogleId = info.ProviderKey;
                user.GoogleAccessToken = tokens.FirstOrDefault(t => t.Name == "access_token")?.Value;
                user.GoogleRefreshToken = tokens.FirstOrDefault(t => t.Name == "refresh_token")?.Value;

                var expiry = tokens.FirstOrDefault(t => t.Name == "expires_at")?.Value;
                if (DateTime.TryParse(expiry, out var exp))
                    user.GoogleTokenExpiry = exp;

                await _userManager.UpdateAsync(user);
            }

            return LocalRedirect(returnUrl);
        }

        // ================= LINKOWANIE GOOGLE DO ISTNIEJĄCEGO KONTA =================

        [HttpGet]
        [Authorize]
        public IActionResult LinkGoogle(string returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(LinkGoogleCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> LinkGoogleCallback(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction("Login");

            var user = await _userManager.GetUserAsync(User);

            // 🔑 Zapisz dane Google w ApplicationUser
            user.GoogleId = info.ProviderKey;

            var tokens = info.AuthenticationTokens?.ToList();
            if (tokens != null)
            {
                user.GoogleAccessToken = tokens.FirstOrDefault(t => t.Name == "access_token")?.Value;
                user.GoogleRefreshToken = tokens.FirstOrDefault(t => t.Name == "refresh_token")?.Value;

                var expiry = tokens.FirstOrDefault(t => t.Name == "expires_at")?.Value;
                if (DateTime.TryParse(expiry, out var exp))
                    user.GoogleTokenExpiry = exp;
            }

            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Twoje konto Google zostało połączone.";
            return LocalRedirect(returnUrl);
        }

        // ================= REGISTER =================

        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string firstName, string lastName, string email, string phoneNumber, string password)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
                || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phoneNumber)
                || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Wszystkie pola są wymagane.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                var guest = new Guest
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    LoyaltyStatus = LoyaltyStatus.Classic,
                    TotalNights = 0
                };
                guest.AssignLoyaltyCard();

                _context.Guests.Add(guest);
                await _context.SaveChangesAsync();

                user.GuestId = guest.Id;
                await _userManager.UpdateAsync(user);

                await _userManager.AddToRoleAsync(user, "Klient");
                await _signInManager.SignInAsync(user, isPersistent: false);

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View();
        }

        // ================= LOGOUT =================

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // ================= RESET HASŁA =================

        [HttpGet]
        public IActionResult RequestPasswordReset() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPasswordReset(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "Podaj adres e-mail.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return RedirectToAction(nameof(PasswordResetConfirmation));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action(nameof(ResetPassword), "Account", new { token, email = user.Email }, Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Reset hasła",
                $"Kliknij tutaj, aby zresetować hasło: <a href='{resetLink}'>Resetuj hasło</a>");

            return RedirectToAction(nameof(PasswordResetConfirmation));
        }

        public IActionResult PasswordResetConfirmation() => View();

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                return BadRequest("Błędny link resetu hasła.");
            }

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        public IActionResult ResetPasswordConfirmation() => View();
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Hasło musi mieć co najmniej 6 znaków.")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Hasła muszą się zgadzać.")]
        public string ConfirmPassword { get; set; }
    }
}
