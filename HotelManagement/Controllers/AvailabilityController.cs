using HotelManagement.Data;
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
            // 📅 domyślnie używamy daty operacyjnej, jeśli użytkownik nie wybrał ręcznie
            var businessDate = await _businessDate.GetCurrentBusinessDateAsync();
            var start = startDate?.Date ?? businessDate;
            var end = start.AddDays(days - 1);

            var availability = _availabilityService.GetAvailability(start, end);

            var viewModel = await _context.RoomTypes
                .Select(rt => new RoomAvailabilityViewModel
                {
                    RoomTypeName = rt.Name,
                    Availability = availability.ContainsKey(rt.Id)
                        ? availability[rt.Id]
                        : new Dictionary<DateTime, int>()
                })
                .ToListAsync();

            ViewBag.Dates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
            ViewBag.StartDate = start;
            ViewBag.Days = days;
            ViewBag.BusinessDate = businessDate; // 🟢 przekazujemy do widoku

            return PartialView("_AvailabilityTable", viewModel);
        }
    }
}
