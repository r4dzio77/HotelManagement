using HotelManagement.Data;
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
            int roomTypeId,
            DateTime checkIn,
            DateTime checkOut,
            bool breakfast,
            bool parking,
            bool extraBed,
            bool pet,
            int personCount,
            List<int> selectedServiceIds)
        {
            int nights = (checkOut.Date - checkIn.Date).Days;
            if (nights < 1)
                throw new ArgumentException("Niepoprawny zakres dat");

            decimal total = 0;

            // ===== CENA POKOJU =====
            var roomType = await _context.RoomTypes.FindAsync(roomTypeId)
                ?? throw new Exception("Nie znaleziono typu pokoju");

            total += roomType.PricePerNight * nights;

            // ===== USŁUGI Z BAZY (RABAT DZIAŁA) =====
            if (selectedServiceIds != null && selectedServiceIds.Any())
            {
                var services = await _context.Services
                    .Where(s => selectedServiceIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var s in services)
                {
                    total += s.Price;
                }
            }

            return total;
        }
    }
}
