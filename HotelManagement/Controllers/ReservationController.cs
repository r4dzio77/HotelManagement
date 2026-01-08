using System.Linq;
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.Services;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Pracownik,Kierownik")]
    public class ReservationController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly ReservationPriceCalculator _priceCalculator;
        private readonly RoomAllocatorService _roomAllocator;
        private readonly LoyaltyService _loyaltyService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBusinessDateProvider _businessDate;

        public ReservationController(
            HotelManagementContext context,
            ReservationPriceCalculator priceCalculator,
            RoomAllocatorService roomAllocator,
            LoyaltyService loyaltyService,
            UserManager<ApplicationUser> userManager,
            IBusinessDateProvider businessDate)
        {
            _context = context;
            _priceCalculator = priceCalculator;
            _roomAllocator = roomAllocator;
            _loyaltyService = loyaltyService;
            _userManager = userManager;
            _businessDate = businessDate;
        }

        [HttpGet]
        public IActionResult CreateGuest() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGuest(
            Guest guest,
            string? CompanyName,
            string? CompanyVatNumber,
            string? CompanyAddress,
            string? CompanyPostalCode,
            string? CompanyCity,
            string? CompanyCountry)
        {
            if (!ModelState.IsValid)
                return View(guest);

            Company? company = null;

            if (guest.CompanyId.HasValue)
            {
                company = await _context.Companies.FindAsync(guest.CompanyId.Value);
            }

            bool hasAnyCompanyData =
                !string.IsNullOrWhiteSpace(CompanyName) ||
                !string.IsNullOrWhiteSpace(CompanyVatNumber) ||
                !string.IsNullOrWhiteSpace(CompanyAddress) ||
                !string.IsNullOrWhiteSpace(CompanyCity);

            if (company == null && hasAnyCompanyData)
            {
                string? normalizedVat = null;

                if (!string.IsNullOrWhiteSpace(CompanyVatNumber))
                    normalizedVat = new string(CompanyVatNumber.Where(char.IsDigit).ToArray());

                if (!string.IsNullOrWhiteSpace(normalizedVat))
                {
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c =>
                            c.VatNumber != null &&
                            new string(c.VatNumber.Where(char.IsDigit).ToArray()) == normalizedVat);
                }

                if (company == null && !string.IsNullOrWhiteSpace(CompanyName))
                {
                    var lowerName = CompanyName.Trim().ToLower();
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == lowerName);
                }

                if (company == null)
                {
                    company = new Company
                    {
                        Name = CompanyName ?? string.Empty,
                        VatNumber = CompanyVatNumber,
                        Address = CompanyAddress,
                        PostalCode = CompanyPostalCode,
                        City = CompanyCity,
                        Country = string.IsNullOrWhiteSpace(CompanyCountry) ? "Polska" : CompanyCountry
                    };

                    _context.Companies.Add(company);
                    await _context.SaveChangesAsync();
                }

                guest.CompanyId = company.Id;
            }

            guest.LoyaltyCardNumber = null;
            guest.LoyaltyStatus = LoyaltyStatus.Classic;
            guest.TotalNights = 0;

            _context.Guests.Add(guest);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("GuestId", guest.Id);
            return RedirectToAction(nameof(CreateReservation));
        }

        [HttpGet]
        public async Task<IActionResult> CreateReservation()
        {
            var guest = await GetCurrentGuestAsync();
            if (guest == null)
                return RedirectToAction(nameof(CreateGuest));

            var businessToday = await _businessDate.GetCurrentBusinessDateAsync();

            var vm = await BuildReservationViewModel(new Reservation
            {
                CheckIn = businessToday,
                CheckOut = businessToday.AddDays(1)
            }, guest);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(ReservationViewModel vm)
        {
            var guest = await GetCurrentGuestAsync();
            if (guest == null)
            {
                ModelState.AddModelError("", "Gość nie został znaleziony.");
                vm = await BuildReservationViewModel(vm.Reservation, vm.Guest);
                return View(vm);
            }

            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Guest.")).ToList())
            {
                ModelState.Remove(key);
            }

            if (!ModelState.IsValid)
            {
                vm = await BuildReservationViewModel(vm.Reservation, guest);
                return View(vm);
            }

            Room? allocatedRoom = null;

            if (vm.RoomId.HasValue)
            {
                bool isAvailable = await _roomAllocator.IsRoomAvailableAsync(
                    vm.RoomId.Value, vm.Reservation.CheckIn, vm.Reservation.CheckOut);

                if (!isAvailable)
                {
                    ModelState.AddModelError("", "Wybrany pokój nie jest dostępny.");
                    vm = await BuildReservationViewModel(vm.Reservation, guest);
                    return View(vm);
                }

                allocatedRoom = await _context.Rooms.FindAsync(vm.RoomId.Value);
            }
            else
            {
                allocatedRoom = await _roomAllocator.AllocateRoomAsync(
                    vm.Reservation.RoomTypeId, vm.Reservation.CheckIn, vm.Reservation.CheckOut);

                if (allocatedRoom == null)
                {
                    ModelState.AddModelError("", "Brak dostępnych pokoi w tym typie.");
                    vm = await BuildReservationViewModel(vm.Reservation, guest);
                    return View(vm);
                }
            }

            var selectedServiceIdsCreate = vm.SelectedServiceIds ?? new List<int>();
            var personCountCreate = vm.PersonCount > 0 ? vm.PersonCount : 1;

            var breakdown = await _priceCalculator.CalculateAsync(
                vm.Reservation.RoomTypeId,
                vm.Reservation.CheckIn,
                vm.Reservation.CheckOut,
                vm.Breakfast,
                vm.Parking,
                vm.ExtraBed,
                vm.Pet,
                personCountCreate,
                selectedServiceIdsCreate
            );

            var totalPrice = Math.Round(breakdown.TotalPrice, 2, MidpointRounding.AwayFromZero);

            var reservation = new Reservation
            {
                GuestId = guest.Id,
                RoomId = allocatedRoom.Id,
                RoomTypeId = vm.Reservation.RoomTypeId,
                CheckIn = vm.Reservation.CheckIn,
                CheckOut = vm.Reservation.CheckOut,
                Breakfast = vm.Breakfast,
                Parking = vm.Parking,
                ExtraBed = vm.ExtraBed,
                Pet = vm.Pet,
                PersonCount = personCountCreate,
                TotalPrice = totalPrice,
                ServicesUsed = selectedServiceIdsCreate.Select(serviceId => new ServiceUsage
                {
                    ServiceId = serviceId,
                    Quantity = 1
                }).ToList()
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> Edit(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var roomTypes = await GetAvailableRoomTypesAsync(reservation.CheckIn, reservation.CheckOut, reservation.Id);

            if (!roomTypes.Any(rt => rt.Id == reservation.RoomTypeId))
            {
                var currentRt = await _context.RoomTypes.FindAsync(reservation.RoomTypeId);
                if (currentRt != null)
                    roomTypes.Add(currentRt);
            }

            var services = await _context.Services.ToListAsync();

            var availableRooms = await GetAvailableRoomsAsync(
                reservation.RoomTypeId, reservation.CheckIn, reservation.CheckOut, reservation.Id);

            var vm = new ReservationViewModel
            {
                Guest = reservation.Guest,
                Reservation = reservation,
                RoomId = reservation.RoomId,
                RoomTypes = new SelectList(roomTypes, "Id", "Name", reservation.RoomTypeId),
                AvailableRooms = new SelectList(availableRooms, "Id", "Number", reservation.RoomId),
                Services = services,
                SelectedServiceIds = reservation.ServicesUsed.Select(su => su.ServiceId).ToList(),
                Breakfast = reservation.Breakfast,
                Parking = reservation.Parking,
                ExtraBed = reservation.ExtraBed,
                Pet = reservation.Pet,
                PersonCount = reservation.PersonCount
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> Edit(int id, ReservationViewModel vm)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.ServicesUsed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            reservation.RoomTypeId = vm.Reservation.RoomTypeId;
            reservation.CheckIn = vm.Reservation.CheckIn;
            reservation.CheckOut = vm.Reservation.CheckOut;
            reservation.Breakfast = vm.Breakfast;
            reservation.Parking = vm.Parking;
            reservation.ExtraBed = vm.ExtraBed;
            reservation.Pet = vm.Pet;

            var personCount = vm.PersonCount > 0 ? vm.PersonCount : 1;
            reservation.PersonCount = personCount;

            if (vm.RoomId.HasValue)
                reservation.RoomId = vm.RoomId.Value;

            var selectedServiceIds = vm.SelectedServiceIds ?? new List<int>();

            // 🔥 IDENTYCZNE LICZENIE JAK W CREATE
            var breakdown = await _priceCalculator.CalculateAsync(
                reservation.RoomTypeId,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Breakfast,
                reservation.Parking,
                reservation.ExtraBed,
                reservation.Pet,
                personCount,
                selectedServiceIds
            );

            reservation.TotalPrice = Math.Round(
                breakdown.TotalPrice,
                2,
                MidpointRounding.AwayFromZero
            );

            reservation.ServicesUsed.Clear();

            foreach (var serviceId in selectedServiceIds)
            {
                reservation.ServicesUsed.Add(new ServiceUsage
                {
                    ReservationId = reservation.Id,
                    ServiceId = serviceId,
                    Quantity = 1
                });
            }

            reservation.Guest.FirstName = vm.Guest.FirstName;
            reservation.Guest.LastName = vm.Guest.LastName;
            reservation.Guest.Email = vm.Guest.Email;
            reservation.Guest.PhoneNumber = vm.Guest.PhoneNumber;
            reservation.Guest.Preferences = vm.Guest.Preferences;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = reservation.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> AssignRoom(int id, int? roomId, int? roomTypeId, bool updatePrice = false, int? originalRoomTypeId = null)
        {
            if (!roomId.HasValue)
            {
                TempData["Error"] = "Nie wybrano pokoju.";
                return RedirectToAction(nameof(Index));
            }

            var reservation = await _context.Reservations
                .Include(r => r.RoomType)
                .Include(r => r.ServicesUsed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                TempData["Error"] = "Rezerwacja nie została znaleziona.";
                return RedirectToAction(nameof(Index));
            }

            var room = await _context.Rooms
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == roomId.Value);

            if (room == null)
            {
                TempData["Error"] = "Wybrany pokój nie istnieje.";
                return RedirectToAction(nameof(Index));
            }

            if (!roomTypeId.HasValue)
            {
                roomTypeId = room.RoomTypeId;
            }

            if (room.RoomTypeId != roomTypeId.Value)
            {
                TempData["Error"] = "Wybrany pokój nie należy do wybranego typu pokoju.";
                return RedirectToAction(nameof(Index));
            }

            if (room.IsBlocked)
            {
                TempData["Error"] = "Wybrany pokój jest zablokowany i nie może być przydzielony.";
                return RedirectToAction(nameof(Index));
            }

            bool isAvailable = await _roomAllocator.IsRoomAvailableAsync(
                room.Id,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Id);

            if (!isAvailable)
            {
                TempData["Error"] = "Pokój nie jest dostępny w wybranym terminie.";
                return RedirectToAction(nameof(Index));
            }

            bool roomTypeChanged = reservation.RoomTypeId != roomTypeId.Value;

            reservation.RoomId = room.Id;
            reservation.RoomTypeId = roomTypeId.Value;

            if (roomTypeChanged && updatePrice)
            {
                var breakdown = await _priceCalculator.CalculateAsync(
                    roomTypeId.Value,
                    reservation.CheckIn,
                    reservation.CheckOut,
                    reservation.Breakfast,
                    reservation.Parking,
                    reservation.ExtraBed,
                    reservation.Pet,
                    reservation.PersonCount,
                    reservation.ServicesUsed.Select(su => su.ServiceId).ToList()
                );

                reservation.TotalPrice = Math.Round(breakdown.TotalPrice, 2, MidpointRounding.AwayFromZero);
            }

            await _context.SaveChangesAsync();

            var notification =
                $"Przydzielono pokój {room.Number} (typ: {room.RoomType.Code}) do rezerwacji #{reservation.Id}."
                + (roomTypeChanged
                    ? (updatePrice
                        ? " Typ pokoju został zmieniony, cena została przeliczona."
                        : " Typ pokoju został zmieniony, cena pozostała bez zmian.")
                    : string.Empty);

            if (!room.IsClean)
            {
                notification += " Uwaga: wybrany pokój jest oznaczony jako brudny – upewnij się, że housekeeping przygotuje go przed przyjazdem gościa.";
            }

            TempData["Notification"] = notification;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.RoomId == null)
            {
                TempData["Error"] = "Nie można zameldować gościa bez przydzielonego pokoju.";
                return RedirectToAction(nameof(Index));
            }

            reservation.Status = ReservationStatus.CheckedIn;

            if (reservation.Room != null)
            {
                reservation.Room.IsClean = false;
                reservation.Room.IsDirty = true;
            }

            await _context.SaveChangesAsync();

            TempData["Notification"] =
                $"Zameldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {reservation.Room.Number}";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var paidAmount = reservation.Payments?.Sum(p => p.Amount) ?? 0m;

            if (paidAmount < reservation.TotalPrice)
            {
                var remaining = reservation.TotalPrice - paidAmount;

                TempData["Error"] =
                    $"Nie można wymeldować gościa. Do zapłaty pozostało {remaining:0.00} zł.";

                return RedirectToAction(nameof(Index));
            }

            reservation.Status = ReservationStatus.CheckedOut;
            await _context.SaveChangesAsync();

            _loyaltyService.AwardPointsForCheckout(reservation);

            TempData["Notification"] =
                $"Wymeldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {reservation.Room?.Number ?? "-"}";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ReservationViewModel> BuildReservationViewModel(Reservation reservation, Guest guest)
        {
            var roomTypes = await GetAvailableRoomTypesAsync(reservation.CheckIn, reservation.CheckOut);

            if (!roomTypes.Any())
            {
                roomTypes = await _context.RoomTypes.ToListAsync();
            }

            int selectedRoomTypeId;
            if (reservation.RoomTypeId != 0 && roomTypes.Any(rt => rt.Id == reservation.RoomTypeId))
            {
                selectedRoomTypeId = reservation.RoomTypeId;
            }
            else
            {
                selectedRoomTypeId = roomTypes.First().Id;
            }

            var services = await _context.Services.ToListAsync();
            var availableRooms = await GetAvailableRoomsAsync(selectedRoomTypeId, reservation.CheckIn, reservation.CheckOut);

            return new ReservationViewModel
            {
                Guest = guest,
                Reservation = reservation,
                RoomId = reservation.RoomId,
                RoomTypes = new SelectList(roomTypes, "Id", "Name", selectedRoomTypeId),
                AvailableRooms = new SelectList(availableRooms, "Id", "Number"),
                Services = services,
                SelectedServiceIds = new List<int>(),
                PersonCount = reservation.PersonCount > 0 ? reservation.PersonCount : 1
            };
        }

        private async Task<List<RoomType>> GetAvailableRoomTypesAsync(DateTime checkIn, DateTime checkOut, int? reservationId = null)
        {
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.Rooms)
                .ToListAsync();

            var result = new List<RoomType>();

            foreach (var rt in roomTypes)
            {
                var candidateRooms = rt.Rooms
                    .Where(r => r.IsClean && !r.IsBlocked)
                    .ToList();

                foreach (var room in candidateRooms)
                {
                    bool isAvailable = await _roomAllocator.IsRoomAvailableAsync(room.Id, checkIn, checkOut, reservationId);
                    if (isAvailable)
                    {
                        result.Add(rt);
                        break;
                    }
                }
            }

            return result;
        }

        private async Task<List<Room>> GetAvailableRoomsAsync(int roomTypeId, DateTime checkIn, DateTime checkOut, int? reservationId = null)
        {
            var rooms = await _context.Rooms
                .Where(r => r.RoomTypeId == roomTypeId && !r.IsBlocked)
                .OrderBy(r => r.Number)
                .ToListAsync();

            var availableRooms = new List<Room>();

            foreach (var room in rooms)
            {
                bool isAvailable = await _roomAllocator.IsRoomAvailableAsync(room.Id, checkIn, checkOut, reservationId);
                if (isAvailable)
                    availableRooms.Add(room);
            }

            return availableRooms;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableRooms(int roomTypeId, DateTime checkIn, DateTime checkOut, int? reservationId)
        {
            var availableRooms = await GetAvailableRoomsAsync(roomTypeId, checkIn, checkOut, reservationId);
            var result = availableRooms.Select(r => new
            {
                id = r.Id,
                number = r.Number,
                isClean = r.IsClean
            });
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableRoomsForReservation(int id)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
            {
                return Json(Array.Empty<object>());
            }

            var rooms = await GetAvailableRoomsAsync(
                reservation.RoomTypeId,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Id);

            var result = rooms.Select(r => new
            {
                id = r.Id,
                number = r.Number,
                isClean = r.IsClean
            });

            return Json(result);
        }

        [HttpGet]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> Index()
        {
            var today = await _businessDate.GetCurrentBusinessDateAsync();

            var reservations = await _context.Reservations
                .Include(r => r.RoomType)
                .Include(r => r.Room).ThenInclude(rt => rt.RoomType)
                .Include(r => r.Guest)
                .ToListAsync();

            ViewBag.Arrivals = reservations
                .Where(r => r.Status == ReservationStatus.Confirmed && (r.CheckIn.Date == today || r.CheckIn.Date == today.AddDays(-1)))
                .ToList();

            ViewBag.InStay = reservations
                .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckIn <= today && r.CheckOut > today)
                .ToList();

            ViewBag.Departures = reservations
                .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOut.Date == today)
                .ToList();

            ViewBag.TodayDate = today.ToString("yyyy-MM-dd");
            ViewBag.AllRoomTypes = await _context.RoomTypes.ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchGuests(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new { results = new object[0] });
            }

            term = term.Trim();
            var lower = term.ToLower();

            var guests = await _context.Guests
                .Where(g =>
                    g.FirstName.ToLower().Contains(lower) ||
                    g.LastName.ToLower().Contains(lower) ||
                    g.Email.ToLower().Contains(lower) ||
                    g.PhoneNumber.ToLower().Contains(lower))
                .OrderBy(g => g.LastName)
                .ThenBy(g => g.FirstName)
                .Take(20)
                .ToListAsync();

            var results = guests.Select(g => new
            {
                id = g.Id,
                name = $"{g.FirstName} {g.LastName}",
                email = g.Email,
                phone = g.PhoneNumber,
                loyaltyStatus = g.LoyaltyStatus.ToString(),
                loyaltyCardNumber = g.LoyaltyCardNumber
            });

            return Json(new { results });
        }

        [HttpGet]
        [Authorize(Roles = "Pracownik,Kierownik")]
        public async Task<IActionResult> Details(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var breakdown = await _priceCalculator.CalculateAsync(
                reservation.RoomTypeId,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Breakfast,
                reservation.Parking,
                reservation.ExtraBed,
                reservation.Pet,
                reservation.PersonCount,
                reservation.ServicesUsed.Select(su => su.ServiceId).ToList()
            );

            ViewBag.PriceBreakdown = breakdown;

            reservation.TotalPrice = Math.Round(breakdown.TotalPrice, 2, MidpointRounding.AwayFromZero);

            return View(reservation);
        }

        [HttpPost]
        public async Task<IActionResult> SelectGuest(int id)
        {
            var guest = await _context.Guests.FindAsync(id);
            if (guest == null)
            {
                TempData["Error"] = "Wybrany gość nie istnieje.";
                return RedirectToAction(nameof(CreateGuest));
            }

            HttpContext.Session.SetInt32("GuestId", guest.Id);
            return RedirectToAction(nameof(CreateReservation));
        }

        private async Task<Guest?> GetCurrentGuestAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && user.GuestId.HasValue)
            {
                return await _context.Guests.FindAsync(user.GuestId.Value);
            }

            var guestId = HttpContext.Session.GetInt32("GuestId");
            if (guestId.HasValue)
            {
                return await _context.Guests.FindAsync(guestId.Value);
            }

            return null;
        }
    }
}
