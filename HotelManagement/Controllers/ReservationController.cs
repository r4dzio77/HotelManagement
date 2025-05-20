using HotelManagement.Data;
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

                // Zapisz GuestId w sesji
                HttpContext.Session.SetInt32("GuestId", guest.Id);

                Console.WriteLine($"Gość zapisany: {guest.FirstName} {guest.LastName}, GuestId zapisane w sesji: {guest.Id}");

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





        // POST: /Reservation/CreateReservation
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

            if (roomType == null)
            {
                ModelState.AddModelError("", "Wybrany typ pokoju nie istnieje.");
                vm.RoomTypes = new SelectList(_context.RoomTypes.ToList(), "Id", "Name");
                vm.AvailableRooms = new SelectList(_context.Rooms.ToList(), "Id", "Number");
                return View(vm);
            }

            if (selectedRoom == null)
            {
                ModelState.AddModelError("", "Wybrany pokój nie istnieje.");
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






        // GET: /Reservation/Index
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Now;

            var reservations = await _context.Reservations
                .Include(r => r.RoomType)
                .ToListAsync();

            ViewBag.Arrivals = reservations.Where(r => r.CheckIn.Date == today.Date).ToList();
            ViewBag.InStay = reservations.Where(r => r.CheckIn <= today && r.CheckOut >= today).ToList();
            ViewBag.Departures = reservations.Where(r => r.CheckOut.Date == today.Date).ToList();
            ViewBag.TodayDate = today.ToString("yyyy-MM-dd");

            return View();
        }
    }
}
