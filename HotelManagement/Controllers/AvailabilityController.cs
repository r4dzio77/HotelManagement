using HotelManagement.Data;
using HotelManagement.Models.ViewModels;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    public class AvailabilityController : Controller
    {
        private readonly AvailabilityService _availabilityService;
        private readonly HotelManagementContext _context;

        public AvailabilityController(AvailabilityService availabilityService, HotelManagementContext context)
        {
            _availabilityService = availabilityService;
            _context = context;
        }

        public IActionResult Partial(DateTime? startDate, int days = 7)
        {
            var start = startDate ?? DateTime.Today;
            var end = start.AddDays(days - 1);

            var availability = _availabilityService.GetAvailability(start, end);

            var viewModel = _context.RoomTypes
                .Select(rt => new RoomAvailabilityViewModel
                {
                    RoomTypeName = rt.Name,
                    Availability = availability[rt.Id]
                })
                .ToList();

            ViewBag.Dates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
            ViewBag.StartDate = start;
            ViewBag.Days = days;

            return PartialView("_AvailabilityTable", viewModel);
        }
    }
}
