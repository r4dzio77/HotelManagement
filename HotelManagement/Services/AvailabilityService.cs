using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Enums;

namespace HotelManagement.Services
{
    public class AvailabilityService
    {
        private readonly HotelManagementContext _context;

        public AvailabilityService(HotelManagementContext context)
        {
            _context = context;
        }

        public Dictionary<int, Dictionary<DateTime, int>> GetAvailability(DateTime startDate, DateTime endDate)
        {
            var roomTypes = _context.RoomTypes
                .Select(rt => new { rt.Id, TotalRooms = rt.Rooms.Count })
                .ToList();

            var reservations = _context.Reservations
                .Where(r =>
                         (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn) &&
                             r.CheckOut > startDate &&
                            r.CheckIn < endDate)

                .ToList();

            var availability = new Dictionary<int, Dictionary<DateTime, int>>();

            foreach (var rt in roomTypes)
            {
                availability[rt.Id] = new Dictionary<DateTime, int>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var reservedCount = reservations
                        .Where(r => r.RoomTypeId == rt.Id &&
                                    r.CheckIn <= date &&
                                    r.CheckOut > date)
                        .Count();

                    availability[rt.Id][date] = rt.TotalRooms - reservedCount;
                }
            }

            return availability;
        }
    }
}
