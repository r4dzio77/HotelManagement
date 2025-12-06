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
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;

        public DashboardController(HotelManagementContext context, IBusinessDateProvider businessDate)
        {
            _context = context;
            _businessDate = businessDate;
        }

        public async Task<IActionResult> Index()
        {
            // DATA OPERACYJNA z nocnego audytu
            var today = (await _businessDate.GetCurrentBusinessDateAsync()).Date;

            var model = new DashboardViewModel
            {
                BusinessDate = today
            };

            // Przyjazdy: CheckIn == dzień operacyjny
            model.TodayArrivals = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckIn.Date == today);

            // Wyjazdy: CheckOut == dzień operacyjny
            model.TodayDepartures = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckOut.Date == today);

            // Pobyty: gość jest w hotelu w tym dniu
            model.TodayStays = await _context.Reservations
                .AsNoTracking()
                .CountAsync(r => r.CheckIn.Date < today && r.CheckOut.Date > today);

            // ŚNIADANIA NA 7 DNI DO PRZODU (liczymy WSZYSTKIE rezerwacje ze śniadaniem)
            var startDate = today;
            var endDate = today.AddDays(7); // today .. today+6

            for (var d = startDate; d < endDate; d = d.AddDays(1))
            {
                var date = d;

                var count = await _context.Reservations
                    .AsNoTracking()
                    .Where(r => r.Breakfast == true)                       // ma śniadanie
                    .Where(r => r.CheckIn.Date < date && r.CheckOut.Date >= date) // pobyt obejmuje ten dzień
                    .SumAsync(r => (int?)r.PersonCount) ?? 0;              // liczymy osoby

                model.BreakfastsNext7Days.Add(new DashboardBreakfastItem
                {
                    Date = date,
                    BreakfastCount = count
                });
            }

            return View(model);
        }
    }
}
