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
    public class ReservationController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly ReservationPriceCalculator _priceCalculator;
        private readonly RoomAllocatorService _roomAllocator;
        private readonly LoyaltyService _loyaltyService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReservationController(
            HotelManagementContext context,
            ReservationPriceCalculator priceCalculator,
            RoomAllocatorService roomAllocator,
            LoyaltyService loyaltyService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _priceCalculator = priceCalculator;
            _roomAllocator = roomAllocator;
            _loyaltyService = loyaltyService;
            _userManager = userManager;
        }

        // Tworzenie gościa (tylko dla pracownika)
        [HttpGet]
        public IActionResult CreateGuest() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGuest(Guest guest)
        {
            if (!ModelState.IsValid)
                return View(guest);

            // 👇 Gość tworzony ręcznie przez pracownika -> brak karty lojalnościowej
            guest.LoyaltyCardNumber = null;
            guest.LoyaltyStatus = LoyaltyStatus.Classic;
            guest.TotalNights = 0;

            _context.Guests.Add(guest);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("GuestId", guest.Id);
            return RedirectToAction(nameof(CreateReservation));
        }

        // GET: CreateReservation
        [HttpGet]
        public async Task<IActionResult> CreateReservation()
        {
            var guest = await GetCurrentGuestAsync();
            if (guest == null)
                return RedirectToAction(nameof(CreateGuest));

            var vm = await BuildReservationViewModel(new Reservation
            {
                CheckIn = DateTime.Today,
                CheckOut = DateTime.Today.AddDays(1)
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

            var totalPrice = await _priceCalculator.CalculateTotalPriceAsync(
                vm.Reservation.RoomTypeId,
                vm.Reservation.CheckIn,
                vm.Reservation.CheckOut,
                vm.Breakfast,
                vm.Parking,
                vm.ExtraBed,
                vm.PersonCount,
                vm.SelectedServiceIds
            );

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
                PersonCount = vm.PersonCount,
                TotalPrice = totalPrice,
                ServicesUsed = vm.SelectedServiceIds.Select(serviceId => new ServiceUsage
                {
                    ServiceId = serviceId,
                    Quantity = 1
                }).ToList()
            };

            _context.Reservations.Add(reservation);

            // ✅ Aktualizujemy dane gościa zamiast tworzyć nowego
            guest.FirstName = vm.Guest.FirstName;
            guest.LastName = vm.Guest.LastName;
            guest.Email = vm.Guest.Email;
            guest.PhoneNumber = vm.Guest.PhoneNumber;
            guest.Preferences = vm.Guest.Preferences;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Edit(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var roomTypes = await _context.RoomTypes.ToListAsync();
            var services = await _context.Services.ToListAsync();

            var availableRooms = await GetAvailableRoomsAsync(
                reservation.RoomTypeId,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Id);

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
                PersonCount = reservation.PersonCount
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Edit(int id, ReservationViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.RoomTypes = new SelectList(await _context.RoomTypes.ToListAsync(), "Id", "Name");
                vm.Services = await _context.Services.ToListAsync();
                vm.AvailableRooms = new SelectList(await GetAvailableRoomsAsync(vm.Reservation.RoomTypeId, vm.Reservation.CheckIn, vm.Reservation.CheckOut, id), "Id", "Number");
                return View(vm);
            }

            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.ServicesUsed)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            Room? allocatedRoom = null;

            if (vm.RoomId.HasValue)
            {
                bool isAvailable = await _roomAllocator.IsRoomAvailableAsync(
                    vm.RoomId.Value,
                    vm.Reservation.CheckIn,
                    vm.Reservation.CheckOut,
                    reservation.Id);

                if (!isAvailable)
                {
                    ModelState.AddModelError("", "Wybrany pokój nie jest dostępny.");
                    vm.RoomTypes = new SelectList(await _context.RoomTypes.ToListAsync(), "Id", "Name");
                    vm.Services = await _context.Services.ToListAsync();
                    vm.AvailableRooms = new SelectList(await GetAvailableRoomsAsync(vm.Reservation.RoomTypeId, vm.Reservation.CheckIn, vm.Reservation.CheckOut, id), "Id", "Number");
                    return View(vm);
                }

                allocatedRoom = await _context.Rooms.FindAsync(vm.RoomId.Value);
            }
            else
            {
                allocatedRoom = await _roomAllocator.AllocateRoomAsync(
                    vm.Reservation.RoomTypeId,
                    vm.Reservation.CheckIn,
                    vm.Reservation.CheckOut,
                    reservation.Id);

                if (allocatedRoom == null)
                {
                    ModelState.AddModelError("", "Brak dostępnych pokoi w tym typie.");
                    vm.RoomTypes = new SelectList(await _context.RoomTypes.ToListAsync(), "Id", "Name");
                    vm.Services = await _context.Services.ToListAsync();
                    vm.AvailableRooms = new SelectList(await GetAvailableRoomsAsync(vm.Reservation.RoomTypeId, vm.Reservation.CheckIn, vm.Reservation.CheckOut, id), "Id", "Number");
                    return View(vm);
                }
            }

            reservation.RoomId = allocatedRoom.Id;
            reservation.RoomTypeId = vm.Reservation.RoomTypeId;
            reservation.CheckIn = vm.Reservation.CheckIn;
            reservation.CheckOut = vm.Reservation.CheckOut;
            reservation.Breakfast = vm.Breakfast;
            reservation.Parking = vm.Parking;
            reservation.ExtraBed = vm.ExtraBed;
            reservation.PersonCount = vm.PersonCount;

            var totalPrice = await _priceCalculator.CalculateTotalPriceAsync(
                vm.Reservation.RoomTypeId,
                vm.Reservation.CheckIn,
                vm.Reservation.CheckOut,
                vm.Breakfast,
                vm.Parking,
                vm.ExtraBed,
                vm.PersonCount,
                vm.SelectedServiceIds
            );

            reservation.TotalPrice = totalPrice;

            reservation.ServicesUsed.Clear();
            foreach (var serviceId in vm.SelectedServiceIds)
            {
                reservation.ServicesUsed.Add(new ServiceUsage
                {
                    ReservationId = reservation.Id,
                    ServiceId = serviceId,
                    Quantity = 1
                });
            }

            // ✅ aktualizacja danych gościa powiązanego z rezerwacją
            reservation.Guest.FirstName = vm.Guest.FirstName;
            reservation.Guest.LastName = vm.Guest.LastName;
            reservation.Guest.Email = vm.Guest.Email;
            reservation.Guest.PhoneNumber = vm.Guest.PhoneNumber;
            reservation.Guest.Preferences = vm.Guest.Preferences;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            reservation.Status = ReservationStatus.CheckedIn;

            if (reservation.Room != null)
            {
                reservation.Room.IsClean = false;
                reservation.Room.IsDirty = true;
            }
            await _context.SaveChangesAsync();

            TempData["Notification"] = $"Zameldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {(reservation.Room != null ? reservation.Room.Number : "nieprzydzielony")}";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            reservation.Status = ReservationStatus.CheckedOut;
            await _context.SaveChangesAsync();

            // ✅ Nalicz punkty lojalnościowe po wymeldowaniu
            _loyaltyService.AwardPointsForCheckout(reservation);

            var roomNumber = reservation.Room != null ? reservation.Room.Number : "nieprzydzielony";
            TempData["Notification"] = $"Wymeldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {roomNumber}";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ReservationViewModel> BuildReservationViewModel(Reservation reservation, Guest guest)
        {
            var roomTypes = await _context.RoomTypes.ToListAsync();
            var services = await _context.Services.ToListAsync();

            var selectedRoomTypeId = reservation.RoomTypeId != 0
                ? reservation.RoomTypeId
                : roomTypes.First().Id;

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
                PersonCount = 1
            };
        }

        private async Task<List<Room>> GetAvailableRoomsAsync(int roomTypeId, DateTime checkIn, DateTime checkOut, int? reservationId = null)
        {
            var rooms = await _context.Rooms
                .Where(r => r.RoomTypeId == roomTypeId && r.IsClean && !r.IsBlocked)
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

        // Modal do AJAX
        [HttpGet]
        public async Task<IActionResult> GetAvailableRooms(int roomTypeId, DateTime checkIn, DateTime checkOut)
        {
            var availableRooms = await GetAvailableRoomsAsync(roomTypeId, checkIn, checkOut);
            var result = availableRooms.Select(r => new
            {
                id = r.Id,
                number = r.Number,
                isClean = r.IsClean
            });
            return Json(result);
        }

        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Search(string? reservationNumber, string? firstName, string? lastName, DateTime? fromDate, DateTime? toDate)
        {
            bool hasFilter = !string.IsNullOrWhiteSpace(reservationNumber)
                || !string.IsNullOrWhiteSpace(firstName)
                || !string.IsNullOrWhiteSpace(lastName)
                || fromDate.HasValue
                || toDate.HasValue;

            List<Reservation> filteredReservations = new List<Reservation>();

            if (hasFilter)
            {
                var query = _context.Reservations
                    .Include(r => r.Guest)
                    .Include(r => r.Room)
                    .Include(r => r.RoomType)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(reservationNumber))
                {
                    if (int.TryParse(reservationNumber, out int resId))
                    {
                        query = query.Where(r => r.Id == resId);
                    }
                }

                if (!string.IsNullOrWhiteSpace(firstName))
                {
                    var lowerFirstName = firstName.ToLower();
                    query = query.Where(r => r.Guest.FirstName.ToLower().Contains(lowerFirstName));
                }

                if (!string.IsNullOrWhiteSpace(lastName))
                {
                    var lowerLastName = lastName.ToLower();
                    query = query.Where(r => r.Guest.LastName.ToLower().Contains(lowerLastName));
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(r => r.CheckIn.Date == fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(r => r.CheckOut.Date == toDate.Value.Date);
                }

                filteredReservations = await query.ToListAsync();
            }

            ViewBag.ReservationNumber = reservationNumber;
            ViewBag.FirstName = firstName;
            ViewBag.LastName = lastName;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(filteredReservations);
        }

        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

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

            return View();
        }

        // ✅ Helper do pobierania aktualnego Guest
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
