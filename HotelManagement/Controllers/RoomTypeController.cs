using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    public class RoomTypeController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoomTypeController(HotelManagementContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: RoomType/Create
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: RoomType/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin")]
        public async Task<IActionResult> Create(RoomType roomType, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // Zapis zdjęcia typu pokoju
                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "roomtypes");
                    var fullPath = Path.Combine(folderPath, fileName);

                    Directory.CreateDirectory(folderPath);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    roomType.ImagePath = "/images/roomtypes/" + fileName; // Przypisanie ścieżki do zdjęcia
                }

                // Zapisz typ pokoju do bazy danych
                _context.RoomTypes.Add(roomType);
                await _context.SaveChangesAsync();

                // Przekierowanie na stronę z dostępnymi typami pokoi
                return RedirectToAction("Index", "RoomType");
            }

            return View(roomType);
        }

        // GET: RoomType/Index
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Klient")]
        public async Task<IActionResult> Index()
        {
            var roomTypes = await _context.RoomTypes.ToListAsync(); // Pobierz wszystkie typy pokoi

            // Sprawdzamy rolę użytkownika
            var user = await _userManager.GetUserAsync(User);
            var isAdminOrManager = User.IsInRole("Kierownik") || User.IsInRole("Admin");

            // Ustawiamy wartość ViewBag.IsAdminOrManager
            ViewBag.IsAdminOrManager = isAdminOrManager;

            return View(roomTypes); // Zwracamy widok z listą typów pokoi
        }
    }
}
