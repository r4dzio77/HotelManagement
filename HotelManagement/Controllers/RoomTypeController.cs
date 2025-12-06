using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.ViewModels;
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

        // ===== INDEX — z obsługą filtrów dat =====
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Klient")]
        public async Task<IActionResult> Index(DateTime? checkIn, DateTime? checkOut)
        {
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.Rooms)
                .ToListAsync();

            DateTime from = checkIn ?? DateTime.Today;
            DateTime to = checkOut ?? DateTime.Today.AddDays(1);

            // pobierz wszystkie rezerwacje w okresie
            var reservations = await _context.Reservations
                .Where(r => r.CheckOut > from && r.CheckIn < to)
                .ToListAsync();

            // oblicz dostępność
            var availability = new Dictionary<int, int>();

            foreach (var rt in roomTypes)
            {
                int totalRooms = rt.Rooms.Count;

                int reservedRooms = reservations
                    .Where(r => r.RoomTypeId == rt.Id)
                    .GroupBy(r => r.RoomId)
                    .Count(); // liczba zajętych pokoi fizycznych

                int availableRooms = totalRooms - reservedRooms;
                if (availableRooms < 0) availableRooms = 0;

                availability[rt.Id] = availableRooms;
            }

            ViewBag.Availability = availability;
            ViewBag.CheckIn = from.ToString("yyyy-MM-dd");
            ViewBag.CheckOut = to.ToString("yyyy-MM-dd");

            bool isAdminOrManager = User.IsInRole("Kierownik") || User.IsInRole("Admin");
            ViewBag.IsAdminOrManager = isAdminOrManager;

            return View(roomTypes);
        }

        // ==================== CREATE ========================
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin")]
        public async Task<IActionResult> Create(RoomType roomType, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
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

                    roomType.ImagePath = "/images/roomtypes/" + fileName;
                }

                _context.RoomTypes.Add(roomType);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "RoomType");
            }

            return View(roomType);
        }

        // ==================== EDIT ========================
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
            {
                return NotFound();
            }
            return View(roomType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin")]
        public async Task<IActionResult> Edit(int id, RoomType roomType, IFormFile imageFile)
        {
            if (id != roomType.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(roomType);
            }

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "roomtypes");
                    Directory.CreateDirectory(folderPath);
                    var fullPath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    roomType.ImagePath = "/images/roomtypes/" + fileName;
                }
                else
                {
                    var existingRoomType = await _context.RoomTypes.AsNoTracking().FirstOrDefaultAsync(rt => rt.Id == id);
                    roomType.ImagePath = existingRoomType?.ImagePath;
                }

                _context.Update(roomType);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.RoomTypes.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // ==================== REZERWACJA ========================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Reserve(int roomTypeId)
        {
            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
                return NotFound();

            ViewBag.RoomType = roomType;

            return View(new Reservation
            {
                RoomTypeId = roomTypeId,
                CheckIn = DateTime.Today,
                CheckOut = DateTime.Today.AddDays(1)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Reserve(Reservation reservation, int personCount)
        {
            var roomType = await _context.RoomTypes.FindAsync(reservation.RoomTypeId);
            if (roomType == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                ViewBag.RoomType = roomType;
                return View(reservation);
            }

            var viewModel = new ReservationGuestViewModel
            {
                RoomTypeId = reservation.RoomTypeId,
                CheckIn = reservation.CheckIn,
                CheckOut = reservation.CheckOut,
                PersonCount = personCount,
                Breakfast = reservation.Breakfast,
                Parking = reservation.Parking,
                ExtraBed = reservation.ExtraBed,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = ""
            };

            return View("EnterGuestDetails", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnterGuestDetails(ReservationGuestViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            Guest guest;

            if (user != null && user.GuestId.HasValue)
            {
                guest = await _context.Guests.FindAsync(user.GuestId.Value);
                if (guest != null)
                {
                    guest.FirstName = model.FirstName;
                    guest.LastName = model.LastName;
                    guest.Email = model.Email;
                    guest.PhoneNumber = model.PhoneNumber;
                    guest.Preferences = model.Preferences;

                    _context.Guests.Update(guest);
                }
            }
            else
            {
                guest = new Guest
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Preferences = model.Preferences,
                    LoyaltyStatus = LoyaltyStatus.Classic,
                    TotalNights = 0
                };

                _context.Guests.Add(guest);
                await _context.SaveChangesAsync();

                if (user != null)
                {
                    user.GuestId = guest.Id;
                    _context.Update(user);
                }
            }

            await _context.SaveChangesAsync();

            var roomType = await _context.RoomTypes.FindAsync(model.RoomTypeId);

            var reservation = new Reservation
            {
                GuestId = guest.Id,
                RoomTypeId = model.RoomTypeId,
                CheckIn = model.CheckIn,
                CheckOut = model.CheckOut,
                Status = ReservationStatus.Confirmed,
                TotalPrice = roomType.PricePerNight * (decimal)(model.CheckOut - model.CheckIn).TotalDays,
                Breakfast = model.Breakfast,
                Parking = model.Parking,
                ExtraBed = model.ExtraBed,
                PersonCount = model.PersonCount
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Confirmation");
        }

        [HttpGet]
        [Authorize]
        public IActionResult Confirmation()
        {
            return View();
        }
    }
}
