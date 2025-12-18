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

        // 💰 stałe cenowe dodatków (przed rabatem)
        private const decimal BreakfastPriceBase = 40m;  // za osobę / noc
        private const decimal ParkingPriceBase = 30m;    // za noc
        private const decimal ExtraBedPriceBase = 80m;   // za noc
        // jeżeli dodasz zwierzę, dodaj tu np:
        // private const decimal PetPriceBase = 50m;

        public RoomTypeController(HotelManagementContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ===== INDEX — z obsługą filtrów dat i pełną dostępnością (rezerwacje + blokady) =====
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Klient")]
        public async Task<IActionResult> Index(DateTime? checkIn, DateTime? checkOut)
        {
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.Rooms)
                    .ThenInclude(r => r.Reservations)
                .ToListAsync();

            Dictionary<int, int>? availabilityForStay = null;

            if (checkIn.HasValue && checkOut.HasValue && checkOut.Value.Date > checkIn.Value.Date)
            {
                var from = checkIn.Value.Date;
                var to = checkOut.Value.Date;

                var nightsCount = (to - from).Days;
                if (nightsCount < 1) nightsCount = 1;

                var stayDates = Enumerable.Range(0, nightsCount)
                    .Select(i => from.AddDays(i))
                    .ToList();

                availabilityForStay = new Dictionary<int, int>();

                foreach (var rt in roomTypes)
                {
                    int minAvailable = int.MaxValue;

                    foreach (var date in stayDates)
                    {
                        int availableThatNight = rt.Rooms
                            .Where(room => !IsBlockedOnDate(room, date))
                            .Count(room => !HasReservationOnDate(room, date));

                        if (availableThatNight < minAvailable)
                            minAvailable = availableThatNight;
                    }

                    if (minAvailable == int.MaxValue)
                        minAvailable = 0;

                    availabilityForStay[rt.Id] = minAvailable;
                }

                ViewBag.CheckIn = from.ToString("yyyy-MM-dd");
                ViewBag.CheckOut = to.ToString("yyyy-MM-dd");
            }
            else
            {
                ViewBag.CheckIn = checkIn?.ToString("yyyy-MM-dd");
                ViewBag.CheckOut = checkOut?.ToString("yyyy-MM-dd");
            }

            ViewBag.Availability = availabilityForStay;

            bool isAdminOrManager = User.IsInRole("Kierownik") || User.IsInRole("Admin");
            ViewBag.IsAdminOrManager = isAdminOrManager;

            return View(roomTypes);
        }

        private bool IsBlockedOnDate(Room room, DateTime date)
        {
            if (!room.IsBlocked)
                return false;

            if (room.BlockFrom.HasValue && room.BlockTo.HasValue)
            {
                var from = room.BlockFrom.Value.Date;
                var to = room.BlockTo.Value.Date;
                return date.Date >= from && date.Date <= to;
            }

            return true;
        }

        private bool HasReservationOnDate(Room room, DateTime date)
        {
            return room.Reservations.Any(res =>
                res.Status == ReservationStatus.Confirmed &&
                res.CheckIn.Date <= date.Date &&
                res.CheckOut.Date > date.Date);
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
                    var existingRoomType = await _context.RoomTypes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(rt => rt.Id == id);

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
        public async Task<IActionResult> Reserve(int roomTypeId, DateTime? checkIn, DateTime? checkOut)
        {
            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
                return NotFound();

            var from = checkIn?.Date ?? DateTime.Today;
            var to = checkOut?.Date ?? from.AddDays(1);
            if (to <= from)
                to = from.AddDays(1);

            // ⭐ program lojalnościowy + ceny dodatków (do widoku Reserve)
            var user = await _userManager.GetUserAsync(User);

            bool hasLoyaltyCard = false;
            decimal loyaltyDiscount = 0m;
            int loyaltyDiscountPercent = 0;
            string? loyaltyStatus = null;

            if (user?.GuestId != null)
            {
                var guest = await _context.Guests.FindAsync(user.GuestId.Value);
                if (guest != null && guest.HasLoyaltyCard)
                {
                    hasLoyaltyCard = true;
                    loyaltyDiscount = guest.GetDiscountPercentage();
                    loyaltyDiscountPercent = (int)(loyaltyDiscount * 100);
                    loyaltyStatus = guest.LoyaltyStatus.ToString();
                }
            }

            ViewBag.RoomType = roomType;

            ViewBag.HasLoyaltyCard = hasLoyaltyCard;
            ViewBag.LoyaltyDiscount = loyaltyDiscount;
            ViewBag.LoyaltyDiscountPercent = loyaltyDiscountPercent;
            ViewBag.LoyaltyStatus = loyaltyStatus;

            ViewBag.BreakfastPrice = BreakfastPriceBase;
            ViewBag.ParkingPrice = ParkingPriceBase;
            ViewBag.ExtraBedPrice = ExtraBedPriceBase;

            return View(new Reservation
            {
                RoomTypeId = roomTypeId,
                CheckIn = from,
                CheckOut = to
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
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            Guest guest;

            // --- UPDATE lub CREATE guest ---
            if (user != null && user.GuestId.HasValue)
            {
                guest = await _context.Guests.FindAsync(user.GuestId.Value);
                if (guest == null)
                    return NotFound();

                guest.FirstName = model.FirstName;
                guest.LastName = model.LastName;
                guest.Email = model.Email;
                guest.PhoneNumber = model.PhoneNumber;
                guest.Preferences = model.Preferences;

                _context.Guests.Update(guest);
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

            // --- POBRANIE TYPU POKOJU ---
            var roomType = await _context.RoomTypes.FindAsync(model.RoomTypeId);
            if (roomType == null)
                return NotFound();

            // 🔢 ILE NOCY
            var nightsDouble = (model.CheckOut.Date - model.CheckIn.Date).TotalDays;
            if (nightsDouble < 1) nightsDouble = 1;
            var nights = (decimal)nightsDouble;

            // 💰 CENA POKOJU
            decimal baseRoomCost = roomType.PricePerNight * nights;

            // 💰 KOSZT USŁUG DODATKOWYCH – używamy stałych z góry klasy
            decimal extrasCost = 0m;

            if (model.Breakfast)
            {
                extrasCost += BreakfastPriceBase * model.PersonCount * nights;
            }

            if (model.Parking)
            {
                extrasCost += ParkingPriceBase * nights;
            }

            if (model.ExtraBed)
            {
                extrasCost += ExtraBedPriceBase * nights;
            }

            // jeśli kiedyś dodasz zwierzaka:
            // if (model.Pet)
            // {
            //     extrasCost += PetPriceBase * nights;
            // }

            // ⭐ RABAT LOJALNOŚCIOWY
            decimal discountFactor = 0m;
            if (guest.HasLoyaltyCard)
            {
                discountFactor = guest.GetDiscountPercentage(); // np. 0.10 = 10%
            }

            decimal subtotal = baseRoomCost + extrasCost;
            decimal discountAmount = subtotal * discountFactor;
            decimal finalTotal = subtotal - discountAmount;

            // --- UTWORZENIE REZERWACJI Z PEŁNĄ CENĄ ---
            var reservation = new Reservation
            {
                GuestId = guest.Id,
                RoomTypeId = model.RoomTypeId,
                CheckIn = model.CheckIn,
                CheckOut = model.CheckOut,
                Status = ReservationStatus.Confirmed,
                TotalPrice = finalTotal,          // pokój + usługi – rabat
                Breakfast = model.Breakfast,
                Parking = model.Parking,
                ExtraBed = model.ExtraBed,
                PersonCount = model.PersonCount
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // ✅ przejście do ekranu wyboru metody płatności (Stripe / na miejscu)
            return RedirectToAction("Pay", "Payments", new { reservationId = reservation.Id });
        }

        [HttpGet]
        [Authorize]
        public IActionResult Confirmation()
        {
            return View();
        }
    }
}
