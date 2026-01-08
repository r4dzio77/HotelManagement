using System;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Services;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Pracownik,Kierownik")]
    public class DashboardController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;

        public DashboardController(
            HotelManagementContext context,
            IBusinessDateProvider businessDate)
        {
            _context = context;
            _businessDate = businessDate;
        }

        public async Task<IActionResult> Index()
        {
            // =========================
            // DATA OPERACYJNA
            // =========================
            var today = (await _businessDate.GetCurrentBusinessDateAsync()).Date;

            var model = new DashboardViewModel
            {
                BusinessDate = today
            };

            // =========================
            // PODSTAWOWE STATYSTYKI
            // =========================

            model.TodayArrivals = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckIn.Date == today);

            model.TodayDepartures = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckOut.Date == today);

            model.TodayStays = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckIn.Date < today && r.CheckOut.Date > today);

            // =========================
            // ŚNIADANIA – 7 DNI
            // =========================

            var startDate = today;
            var endDate = today.AddDays(7);

            for (var d = startDate; d < endDate; d = d.AddDays(1))
            {
                var date = d;

                var count = await _context.Reservations
                    .AsNoTracking()
                    .Where(r => r.Breakfast == true)
                    .Where(r => r.CheckIn.Date < date && r.CheckOut.Date >= date)
                    .SumAsync(r => (int?)r.PersonCount) ?? 0;

                model.BreakfastsNext7Days.Add(new DashboardBreakfastItem
                {
                    Date = date,
                    BreakfastCount = count
                });
            }

            // =========================
            // 🔥 CHAT – WIADOMOŚCI DO ODPISANIA
            // =========================

            var chatsWaitingForStaff = await _context.ChatConversations
                .Include(c => c.User)
                .Include(c => c.Messages)
                .Where(c =>
                    c.Messages.Any() &&
                    !c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .First().IsFromStaff)
                .OrderByDescending(c => c.Messages.Max(m => m.SentAt))
                .Take(5)
                .ToListAsync();

            ViewBag.ChatWaitingCount = chatsWaitingForStaff.Count;
            ViewBag.ChatWaitingList = chatsWaitingForStaff;

            // =========================
            // 👥 KTO DZIŚ PRACUJE
            // =========================

            var todayShifts = await _context.WorkShifts
                .Include(ws => ws.User)
                .Where(ws => ws.Date.Date == today)

                .OrderBy(ws => ws.StartTime)
                .ToListAsync();

            ViewBag.TodayShifts = todayShifts;

            // =========================
            // 🧹 POKOJE PROBLEMATYCZNE
            // =========================

            var dirtyRoomsCount = await _context.Rooms
                .CountAsync(r => r.IsDirty);

            var blockedRoomsCount = await _context.Rooms
                .CountAsync(r => r.IsBlocked);

            ViewBag.DirtyRoomsCount = dirtyRoomsCount;
            ViewBag.BlockedRoomsCount = blockedRoomsCount;

            // =========================
            // 📊 OBŁOŻENIE PROCENTOWE
            // =========================

            var totalRooms = await _context.Rooms.CountAsync();

            var occupiedRooms = await _context.Reservations
                .Where(r => r.CheckIn.Date <= today && r.CheckOut.Date > today)
                .Select(r => r.RoomId)
                .Distinct()
                .CountAsync();

            var occupancyPercent = totalRooms == 0
                ? 0
                : (int)Math.Round((double)occupiedRooms / totalRooms * 100);

            ViewBag.OccupancyPercent = occupancyPercent;
            ViewBag.OccupiedRooms = occupiedRooms;
            ViewBag.TotalRooms = totalRooms;

            // =========================
            // ✅ TO-DO LISTA DNIA
            // =========================
            // (zakładamy model DailyTask – edycja w osobnym controllerze)

            var dailyTasks = await _context.DailyTasks
                .Where(t => t.BusinessDate.Date == today)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.DailyTasks = dailyTasks;

            return View(model);
        }
    }
}
