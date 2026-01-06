using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelManagementContext _context;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<ApplicationUser> userManager,
        HotelManagementContext context)
    {
        _logger = logger;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // ===== ZALOGOWANY U¯YTKOWNIK =====
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewData["UserName"] = user?.FullName;
        }

        // ===== OSTATNIE POZYTYWNE OPINIE (Z BAZY) =====
        ViewBag.LastPositiveReviews = await _context.Reviews
            .AsNoTracking()
           .Where(r => r.AverageRating >= 4m)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new ReviewPreviewVm
            {
                Rating = (double)r.AverageRating,
                Comment = r.Comment ?? string.Empty
            })
            .ToListAsync();

        return View();
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]


    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

}
