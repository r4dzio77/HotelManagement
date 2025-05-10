using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    public class RoomController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoomController(HotelManagementContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Room/Create
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin")]
        public IActionResult Create()
        {
            // Pobieramy wszystkie dostępne typy pokoi
            ViewBag.RoomTypes = _context.RoomTypes.ToList();
            return View();
        }

        // POST: Room/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin")]
        public async Task<IActionResult> Create(int roomTypeId, Room room, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // Przetwarzanie zdjęcia pokoju
                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "rooms");
                    var fullPath = Path.Combine(folderPath, fileName);

                    Directory.CreateDirectory(folderPath);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    //room.ImagePath = "/images/rooms/" + fileName;
                }

                // Ustawiamy RoomTypeId z formularza, aby pokój był przypisany do konkretnego typu pokoju
                room.RoomTypeId = roomTypeId;

                // Zapisz pokój do bazy danych
                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "RoomType");
            }

            // Jeśli coś poszło nie tak, wyświetlamy ponownie formularz
            ViewBag.RoomTypes = _context.RoomTypes.ToList();
            return View(room);
        }
    }
}
