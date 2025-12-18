using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Models.ViewModels;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
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
            //    (tu są zarówno rezerwacje z kanału gościa, jak i te założone przez pracowników)
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
                    // 🔸 Funkcja pomocnicza do sprawdzenia blokady pokoju w konkretnym dniu
                    bool IsBlockedOnDate(Room room)
                    {
                        if (!room.IsBlocked)
                            return false;

                        // Jeśli mamy zakres dat blokady – sprawdzamy konkretny dzień
                        if (room.BlockFrom.HasValue && room.BlockTo.HasValue)
                        {
                            var from = room.BlockFrom.Value.Date;
                            var to = room.BlockTo.Value.Date;
                            return date.Date >= from && date.Date <= to;
                        }

                        // Brak zakresu – nie blokujemy w widoku dostępności
                        // (blokady z UI i tak powinny mieć daty)
                        return false;
                    }

                    // 🔹 Liczba fizycznych pokoi tego typu NIEzablokowanych w danym dniu
                    var roomsNotBlocked = rt.Rooms
                        .Where(room => !IsBlockedOnDate(room))
                        .ToList();

                    var totalRoomsNotBlocked = roomsNotBlocked.Count;

                    // 🔹 Wszystkie rezerwacje (gości + pracowników) tego typu,
                    //    które obejmują tę dobę [CheckIn, CheckOut)
                    var reservationCountForTypeAndDate = reservationsInRange
                        .Count(res =>
                            res.RoomTypeId == rt.Id &&
                            res.CheckIn.Date <= date.Date &&
                            res.CheckOut.Date > date.Date);

                    // Dostępne pokoje = liczba pokoi nieblokowanych – liczba rezerwacji
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
