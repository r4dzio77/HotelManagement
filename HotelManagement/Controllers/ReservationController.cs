
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HotelManagement.Controllers
{
    public class ReservationController : Controller
    {
        private readonly HotelManagementContext _context;

        public ReservationController(HotelManagementContext context)
        {
            _context = context;
        }

        // GET: /Reservation/CreateGuest
        [HttpGet]
        public IActionResult CreateGuest()
        {
            return View();
        }

        // POST: /Reservation/CreateGuest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGuest(Guest guest)
        {
            if (!ModelState.IsValid)
            {
                return View(guest);
            }

            try
            {
                _context.Guests.Add(guest);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetInt32("GuestId", guest.Id);
                return RedirectToAction(nameof(CreateReservation));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas zapisywania gościa: {ex.Message}");
                ModelState.AddModelError("", "Błąd podczas zapisywania gościa.");
                return View(guest);
            }
        }

        // GET: /Reservation/CreateReservation
        [HttpGet]
        public IActionResult CreateReservation()
        {
            var guestId = HttpContext.Session.GetInt32("GuestId");
            if (!guestId.HasValue)
                return RedirectToAction(nameof(CreateGuest));

            var guest = _context.Guests.FirstOrDefault(g => g.Id == guestId.Value);
            if (guest == null)
                return RedirectToAction(nameof(CreateGuest));

            var reservation = new Reservation
            {
                CheckIn = DateTime.Today,
                CheckOut = DateTime.Today.AddDays(1)
            };

            var vm = new ReservationViewModel
            {
                Guest = guest,
                Reservation = reservation,
                RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name"),
                AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number")
            };

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
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            var guest = _context.Guests.FirstOrDefault(g => g.Id == guestId.Value);
            if (guest == null)
            {
                ModelState.AddModelError("", "Gość nie został znaleziony.");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            vm.Guest = guest;

            if (!ModelState.IsValid)
            {
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            var roomType = await _context.RoomTypes.FirstOrDefaultAsync(rt => rt.Id == vm.Reservation.RoomTypeId);
            var selectedRoom = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == vm.RoomId);

            if (roomType == null || selectedRoom == null)
            {
                ModelState.AddModelError("", "Niepoprawny wybór pokoju lub typu pokoju.");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            decimal price = roomType.PricePerNight * (vm.Reservation.CheckOut - vm.Reservation.CheckIn).Days;
            if (vm.Breakfast) price += 20;
            if (vm.Parking) price += 15;
            if (vm.ExtraBed) price += 50;

            vm.Reservation.TotalPrice = price;
            vm.Reservation.GuestId = guest.Id;
            vm.Reservation.RoomId = selectedRoom.Id;
            vm.Reservation.RoomTypeId = roomType.Id;
            vm.Reservation.Breakfast = vm.Breakfast;
            vm.Reservation.Parking = vm.Parking;
            vm.Reservation.ExtraBed = vm.ExtraBed;

            try
            {
                _context.Reservations.Add(vm.Reservation);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Reservation");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Błąd podczas zapisywania rezerwacji: {ex.Message}");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Edit(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Include(r => r.Guest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var vm = new ReservationViewModel
            {
                Reservation = reservation,
                Guest = reservation.Guest,
                //RoomId = reservation.RoomId,
                Breakfast = reservation.Breakfast,
                Parking = reservation.Parking,
                ExtraBed = reservation.ExtraBed,
                RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name"),
                AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number")
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Edit(int id, ReservationViewModel vm)
        {
            if (id != vm.Reservation.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == vm.RoomId);
            var roomType = await _context.RoomTypes.FirstOrDefaultAsync(rt => rt.Id == vm.Reservation.RoomTypeId);

            if (room == null || roomType == null)
            {
                ModelState.AddModelError("", "Wybrany pokój lub typ pokoju nie istnieje.");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            reservation.RoomId = room.Id;
            reservation.RoomTypeId = roomType.Id;
            reservation.CheckIn = vm.Reservation.CheckIn;
            reservation.CheckOut = vm.Reservation.CheckOut;
            reservation.Breakfast = vm.Breakfast;
            reservation.Parking = vm.Parking;
            reservation.ExtraBed = vm.ExtraBed;

            decimal price = roomType.PricePerNight * (reservation.CheckOut - reservation.CheckIn).Days;
            if (reservation.Breakfast) price += 20;
            if (reservation.Parking) price += 15;
            if (reservation.ExtraBed) price += 50;
            reservation.TotalPrice = price;

            reservation.Guest.FirstName = vm.Guest.FirstName;
            reservation.Guest.LastName = vm.Guest.LastName;
            reservation.Guest.Email = vm.Guest.Email;
            reservation.Guest.PhoneNumber = vm.Guest.PhoneNumber;
            reservation.Guest.CompanyName = vm.Guest.CompanyName;
            reservation.Guest.Preferences = vm.Guest.Preferences;

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Błąd podczas zapisu: {ex.Message}");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }
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
            await _context.SaveChangesAsync();

            TempData["Notification"] = $"Zameldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {reservation.Room.Number}";

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

            TempData["Notification"] = $"Wymeldowano pomyślnie: {reservation.Guest.FirstName} {reservation.Guest.LastName}, pokój {reservation.Room.Number}";

            return RedirectToAction(nameof(Index));
        }


        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Details(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Include(r => r.ServicesUsed).ThenInclude(s => s.Service)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }



        // GET: /Reservation/Index
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

            // Tylko rezerwacje potwierdzone, które mają przyjazd dzisiaj
            ViewBag.Arrivals = reservations
                .Where(r => r.Status == ReservationStatus.Confirmed && (r.CheckIn.Date == today || r.CheckIn.Date == today.AddDays(-1)))
                .ToList();

            // Tylko rezerwacje, które są zameldowane i trwają dzisiaj
            ViewBag.InStay = reservations
                .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckIn <= today && r.CheckOut > today)
                .ToList();

            // Tylko rezerwacje zameldowane, które kończą się dzisiaj
            ViewBag.Departures = reservations
                .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOut.Date == today)
                .ToList();

            ViewBag.TodayDate = today.ToString("yyyy-MM-dd");

            return View();
        }

    }
}
