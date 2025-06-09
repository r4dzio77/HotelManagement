using HotelManagement.Data;
using HotelManagement.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    public class HousekeepingController : Controller
    {
        private readonly HotelManagementContext _context;

        public HousekeepingController(HotelManagementContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Floor(int? floor)
        {
            var today = DateTime.Today;

            var rooms = await _context.Rooms
                .Where(r => floor == null || r.Floor == floor)
                .Include(r => r.Reservations)
                .OrderBy(r => r.Number)
                .ToListAsync();

            foreach (var room in rooms)
            {
                var activeReservation = room.Reservations
                    .FirstOrDefault(r =>
                        (r.Status == ReservationStatus.Confirmed && r.CheckIn.Date == today) ||
                        (r.Status == ReservationStatus.CheckedIn));

                room.Tag = null;

                if (activeReservation != null)
                {
                    if (activeReservation.Status == ReservationStatus.Confirmed && activeReservation.CheckIn.Date == today)
                        room.Tag = "przyjazd";
                    else if (activeReservation.Status == ReservationStatus.CheckedIn && activeReservation.CheckOut.Date == today)
                        room.Tag = "wyjazd";
                    else if (activeReservation.Status == ReservationStatus.CheckedIn)
                        room.Tag = "pobyt";
                }
            }

            ViewBag.SelectedFloor = floor;
            ViewBag.Floors = await _context.Rooms.Select(r => r.Floor).Distinct().OrderBy(f => f).ToListAsync();

            return View(rooms);
        }


        [HttpPost]
        public async Task<IActionResult> MarkDirty(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsDirty = true;
            room.IsClean = false;
            await _context.SaveChangesAsync();
            return RedirectToAction("Floor", new { floor = room.Floor });
        }

        [HttpPost]
        public async Task<IActionResult> MarkClean(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsDirty = false;
            room.IsClean = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("Floor", new { floor = room.Floor });
        }

        [HttpPost]
        public async Task<IActionResult> Block(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsBlocked = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("Floor", new { floor = room.Floor });
        }

        [HttpPost]
        public async Task<IActionResult> Unblock(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsBlocked = false;
            await _context.SaveChangesAsync();
            return RedirectToAction("Floor", new { floor = room.Floor });
        }
    }
}
