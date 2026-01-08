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

        public async Task<ReservationPriceBreakdown> CalculateAsync(
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
            if (checkOut <= checkIn)
                throw new ArgumentException("Niepoprawny zakres dat");

            int nights = (checkOut.Date - checkIn.Date).Days;

            var roomType = await _context.RoomTypes.FindAsync(roomTypeId)
                ?? throw new Exception("Nie znaleziono typu pokoju");

            // =========================
            // 1️⃣ NOCLEG
            // =========================
            decimal roomPrice = roomType.PricePerNight * nights;

            // =========================
            // 2️⃣ OPCJE STANDARDOWE
            // =========================
            decimal options = 0;

            if (breakfast)
                options += 40m * personCount * nights;

            if (parking)
                options += 30m * nights;

            if (extraBed)
                options += 80m * nights;

            if (pet)
                options += 80m * nights;

            // =========================
            // 3️⃣ USŁUGI Z BAZY
            // =========================
            decimal services = 0;

            if (selectedServiceIds != null && selectedServiceIds.Any())
            {
                var dbServices = await _context.Services
                    .Where(s => selectedServiceIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var s in dbServices)
                {
                    services += s.Price;
                }
            }

            return new ReservationPriceBreakdown
            {
                Nights = nights,
                RoomPrice = roomPrice,
                ServicesPrice = options + services,
                TotalPrice = roomPrice + options + services
            };
        }

        public class ReservationPriceBreakdown
        {
            public int Nights { get; set; }
            public decimal RoomPrice { get; set; }
            public decimal ServicesPrice { get; set; }
            public decimal TotalPrice { get; set; }
        }
    }

}
