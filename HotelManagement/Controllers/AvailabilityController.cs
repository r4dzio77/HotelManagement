using HotelManagement.Data;
using HotelManagement.Models.ViewModels;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace HotelManagement.Controllers
{
    public class AvailabilityController : Controller
    {
        private readonly AvailabilityService _availabilityService;
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate; // ⬅️ data operacyjna (audytowa)

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
            // 📅 domyślny start z daty audytowej
            var start = startDate ?? await _businessDate.GetCurrentBusinessDateAsync();
            var end = start.AddDays(days - 1);

            // 🧮 pobierz dostępność z serwisu
            var availability = _availabilityService.GetAvailability(start, end);

            // 🧱 budowa modelu widoku
            var viewModel = _context.RoomTypes
                .Select(rt => new RoomAvailabilityViewModel
                {
                    RoomTypeName = rt.Name,
                    Availability = availability.ContainsKey(rt.Id)
                        ? availability[rt.Id] // Dictionary<DateTime, int>
                        : new Dictionary<DateTime, int>()
                })
                .ToList();

            ViewBag.Dates = Enumerable.Range(0, days)
                .Select(i => start.AddDays(i))
                .ToList();

            ViewBag.StartDate = start;
            ViewBag.Days = days;

            return PartialView("_AvailabilityTable", viewModel);
        }
    }
}
