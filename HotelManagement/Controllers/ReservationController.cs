using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Services;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
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

        public ReservationController(
            HotelManagementContext context,
            ReservationPriceCalculator priceCalculator,
            RoomAllocatorService roomAllocator)
        {
            _context = context;
            _priceCalculator = priceCalculator;
            _roomAllocator = roomAllocator;
        }

        // Tworzenie gościa
        [HttpGet]
        public IActionResult CreateGuest() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGuest(Guest guest)
        {
            if (!ModelState.IsValid)
                return View(guest);

            _context.Guests.Add(guest);
            await _context.SaveChangesAsync();
            HttpContext.Session.SetInt32("GuestId", guest.Id);
            return RedirectToAction(nameof(CreateReservation));
        }

        // GET: CreateReservation
        [HttpGet]
        public async Task<IActionResult> CreateReservation()
        {
            var guestId = HttpContext.Session.GetInt32("GuestId");
            if (!guestId.HasValue)
                return RedirectToAction(nameof(CreateGuest));

            var guest = await _context.Guests.FindAsync(guestId.Value);
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
            var guestId = HttpContext.Session.GetInt32("GuestId");
            if (!guestId.HasValue)
            {
                ModelState.AddModelError("", "Gość nie został znaleziony.");
                vm = await BuildReservationViewModel(vm.Reservation, vm.Guest);
                return View(vm);
            }

            var guest = await _context.Guests.FindAsync(guestId.Value);
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
                reservation.Id);  // <- uwzględniamy edytowaną rezerwację

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

            // Aktualizacja pól rezerwacji
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

            // Aktualizacja usług
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

            // Aktualizacja gościa
            reservation.Guest.FirstName = vm.Guest.FirstName;
            reservation.Guest.LastName = vm.Guest.LastName;
            reservation.Guest.Email = vm.Guest.Email;
            reservation.Guest.PhoneNumber = vm.Guest.PhoneNumber;
            reservation.Guest.Preferences = vm.Guest.Preferences;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // Budowanie ViewModelu
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


        // Index
        public async Task<IActionResult> Index()
        {
            var reservations = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .ToListAsync();

            ViewBag.Arrivals = reservations
                .Where(r => r.CheckIn.Date == DateTime.Today)
                .ToList();

            ViewBag.InStay = reservations
                .Where(r => r.CheckIn.Date < DateTime.Today && r.CheckOut.Date > DateTime.Today)
                .ToList();

            ViewBag.Departures = reservations
                .Where(r => r.CheckOut.Date == DateTime.Today)
                .ToList();

            ViewBag.TodayDate = DateTime.Today.ToString("dd.MM.yyyy");

            return View(reservations);
        }
    }
}
