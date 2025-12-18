using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services
{
    public class ReservationPriceCalculator
    {
        private readonly HotelManagementContext _context;

        public ReservationPriceCalculator(HotelManagementContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculateTotalPriceAsync(
            int roomTypeId, DateTime checkIn, DateTime checkOut,
            bool breakfast, bool parking, bool Pet,  bool extraBed, int personCount,
            List<int> selectedServiceIds)
        {
            decimal totalPrice = 0;

            int nights = (checkOut - checkIn).Days;
            if (nights <= 0)
                throw new ArgumentException("Niepoprawny zakres dat");

            var roomType = await _context.RoomTypes.FindAsync(roomTypeId);
            if (roomType == null)
                throw new Exception("Nie znaleziono typu pokoju");

            totalPrice += roomType.PricePerNight * nights;

            if (breakfast)
                totalPrice += 60 * nights * personCount;
            if (parking)
                totalPrice += 40 * nights;
            if (extraBed)
                totalPrice += 80 * nights;
            if (Pet)
                totalPrice += 80 * nights;

            if (selectedServiceIds != null && selectedServiceIds.Any())
            {
                var services = await _context.Services
                    .Where(s => selectedServiceIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var service in services)
                {
                    totalPrice += service.Price;
                }
            }

            return totalPrice;
        }
    }
}
