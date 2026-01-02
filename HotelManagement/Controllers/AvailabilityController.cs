using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Models.ViewModels;
using HotelManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Pracownik,Kierownik")]
    public class AvailabilityController : Controller
    {
        private readonly AvailabilityService _availabilityService;
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;

        public AvailabilityController(
            AvailabilityService availabilityService,
            HotelManagementContext context,
            IBusinessDateProvider businessDate)
        {
            _availabilityService = availabilityService;
            _context = context;
            _businessDate = businessDate;
        }

        public async Task<IActionResult> Partial(DateTime? startDate, int days = 7)
        {
            // Bieżąca data operacyjna (z nocnego audytu)
            var businessDate = await _businessDate.GetCurrentBusinessDateAsync();
            var start = startDate?.Date ?? businessDate;

            if (days < 1) days = 1;
            if (days > 30) days = 30;

            // Lista dat do tabeli
            var dates = Enumerable.Range(0, days)
                                  .Select(i => start.AddDays(i))
                                  .ToList();

            var firstDate = dates.First();
            var lastDate = dates.Last().AddDays(1); // zakres otwarty z prawej strony

            // Pobieramy typy pokoi razem z pokojami i ich rezerwacjami
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.Rooms)
                    .ThenInclude(r => r.Reservations)
                .ToListAsync();

            // 🔹 Wszystkie rezerwacje w interesującym nas zakresie dat
            var reservationsInRange = await _context.Reservations
                .Where(r =>
                    r.CheckIn < lastDate &&
                    r.CheckOut > firstDate)
                .ToListAsync();

            var viewModel = new List<RoomAvailabilityViewModel>();

            foreach (var rt in roomTypes)
            {
                var vm = new RoomAvailabilityViewModel
                {
                    RoomTypeName = rt.Name,
                    Availability = new Dictionary<DateTime, int>()
                };

                foreach (var date in dates)
                {
                    bool IsBlockedOnDate(Room room)
                    {
                        if (!room.IsBlocked)
                            return false;

                        if (room.BlockFrom.HasValue && room.BlockTo.HasValue)
                        {
                            var from = room.BlockFrom.Value.Date;
                            var to = room.BlockTo.Value.Date;
                            return date.Date >= from && date.Date <= to;
                        }

                        return false;
                    }

                    var roomsNotBlocked = rt.Rooms
                        .Where(room => !IsBlockedOnDate(room))
                        .ToList();

                    var totalRoomsNotBlocked = roomsNotBlocked.Count;

                    var reservationCountForTypeAndDate = reservationsInRange
                        .Count(res =>
                            res.RoomTypeId == rt.Id &&
                            res.CheckIn.Date <= date.Date &&
                            res.CheckOut.Date > date.Date);

                    var availableCount = totalRoomsNotBlocked - reservationCountForTypeAndDate;
                    if (availableCount < 0)
                        availableCount = 0;

                    vm.Availability[date] = availableCount;
                }

                viewModel.Add(vm);
            }

            ViewBag.Dates = dates;
            ViewBag.StartDate = start;
            ViewBag.Days = days;
            ViewBag.BusinessDate = businessDate;

            return PartialView("_AvailabilityTable", viewModel);
        }
    }
}
