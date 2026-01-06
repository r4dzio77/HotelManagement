using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Kierownik,Recepcjonista")]
    public class HousekeepingController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDateProvider;

        // 🔴 JEDYNY konstruktor – nie zostawiaj żadnego innego!
        public HousekeepingController(
            HotelManagementContext context,
            IBusinessDateProvider businessDateProvider)
        {
            _context = context;
            _businessDateProvider = businessDateProvider;
        }

        // Prosty redirect na pierwsze dostępne piętro
        public async Task<IActionResult> Index()
        {
            var floors = await _context.Rooms
                .Select(r => r.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            if (!floors.Any())
                return RedirectToAction(nameof(Floor));

            return RedirectToAction(nameof(Floor), new { floor = floors.First() });
        }

        /// <summary>
        /// Widok planu piętra – zasila Floor.cshtml
        /// </summary>
        public async Task<IActionResult> Floor(int? floor)
        {
            var businessDate = await _businessDateProvider.GetCurrentBusinessDateAsync();

            // lista pięter
            var floors = await _context.Rooms
                .Select(r => r.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            int selectedFloor;

            if (floor.HasValue && floors.Contains(floor.Value))
            {
                selectedFloor = floor.Value;
            }
            else
            {
                selectedFloor = floors.Any() ? floors.First() : 0;
            }

            // Pokoje na wybranym piętrze + rezerwacje
            var rooms = await _context.Rooms
                .Where(r => r.Floor == selectedFloor)
                .Include(r => r.Reservations)
                .Include(r => r.RoomType)
                .ToListAsync();

            // ✅ AUTO-ODBLOKOWANIE pokoi po upływie daty blokady
            var roomsToUnblock = rooms
                .Where(r =>
                    r.IsBlocked &&
                    r.BlockTo.HasValue &&
                    r.BlockTo.Value.Date < businessDate.Date)
                .ToList();

            if (roomsToUnblock.Any())
            {
                foreach (var room in roomsToUnblock)
                {
                    room.IsBlocked = false;
                    room.BlockFrom = null;
                    room.BlockTo = null;
                    room.BlockReason = null;
                }

                await _context.SaveChangesAsync();
            }

            // Ustawiamy Tag ("pobyt", "przyjazd", "wyjazd") na bazie rezerwacji dla daty operacyjnej
            foreach (var room in rooms)
            {
                room.Tag = null;

                var todaysReservations = room.Reservations
                    .Where(res =>
                        res.Status == ReservationStatus.Confirmed &&
                        res.CheckIn.Date <= businessDate.Date &&
                        res.CheckOut.Date > businessDate.Date)
                    .ToList();

                if (!todaysReservations.Any())
                    continue;

                // Priorytet: wyjazd > przyjazd > pobyt
                var isDeparture = todaysReservations.Any(res => res.CheckOut.Date == businessDate.Date);
                var isArrival = todaysReservations.Any(res => res.CheckIn.Date == businessDate.Date);

                if (isDeparture)
                    room.Tag = "wyjazd";
                else if (isArrival)
                    room.Tag = "przyjazd";
                else
                    room.Tag = "pobyt";
            }

            ViewBag.Floors = floors;
            ViewBag.SelectedFloor = selectedFloor;

            return View(rooms);
        }

        /// <summary>
        /// Oznaczenie pokoju jako brudny
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDirty(int id)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return NotFound();

            room.IsDirty = true;
            room.IsClean = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Floor), new { floor = room.Floor });
        }

        /// <summary>
        /// Oznaczenie pokoju jako czysty
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkClean(int id)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return NotFound();

            room.IsDirty = false;
            room.IsClean = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Floor), new { floor = room.Floor });
        }

        /// <summary>
        /// Blokowanie pokoju na zakres dat + powód
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Block(int id, DateTime fromDate, DateTime toDate, string reason)
        {
            if (toDate.Date < fromDate.Date)
            {
                ModelState.AddModelError("", "Data do nie może być wcześniejsza niż data od.");
                return RedirectToAction(nameof(Index));
            }

            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return NotFound();

            room.IsBlocked = true;
            room.BlockFrom = fromDate.Date;
            room.BlockTo = toDate.Date;
            room.BlockReason = reason;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Floor), new { floor = room.Floor });
        }

        /// <summary>
        /// Odblokowanie pokoju – ręczne
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unblock(int id)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return NotFound();

            room.IsBlocked = false;
            room.BlockFrom = null;
            room.BlockTo = null;
            room.BlockReason = null;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Floor), new { floor = room.Floor });
        }
    }
}
