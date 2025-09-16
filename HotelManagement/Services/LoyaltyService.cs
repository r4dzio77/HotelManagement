using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services
{
    public class LoyaltyService
    {
        private readonly HotelManagementContext _context;

        public LoyaltyService(HotelManagementContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Nalicz punkty i zaktualizuj status lojalnościowy po wymeldowaniu.
        /// </summary>
        public void AwardPointsForCheckout(Reservation reservation)
        {
            var guest = reservation.Guest;
            if (guest == null) return;

            // brak karty lojalnościowej → nie naliczamy punktów
            if (!guest.HasLoyaltyCard) return;

            int nights = (reservation.CheckOut - reservation.CheckIn).Days;
            guest.TotalNights += nights;

            int points = (int)Math.Floor(reservation.TotalPrice);

            var loyaltyPoint = new LoyaltyPoint
            {
                GuestId = guest.Id,
                Points = points,
                AwardedAt = DateTime.UtcNow,
                Reason = $"Punkty za pobyt {reservation.CheckIn:dd.MM.yyyy} - {reservation.CheckOut:dd.MM.yyyy}"
            };

            // 🔹 Jeśli Guest jest powiązany z ApplicationUser → ustaw UserId
            if (reservation.Guest?.Id != 0 && reservation.Guest.CompanyId == null)
            {
                var user = _context.Users.FirstOrDefault(u => u.GuestId == guest.Id);
                if (user != null)
                {
                    loyaltyPoint.UserId = user.Id;
                }
            }

            _context.LoyaltyPoints.Add(loyaltyPoint);

            // 🔹 Aktualizacja statusu lojalnościowego
            guest.UpdateLoyaltyStatus();

            _context.Guests.Update(guest);
            _context.SaveChanges();
        }

        /// <summary>
        /// Pobiera łączną liczbę punktów gościa.
        /// </summary>
        public int GetGuestPoints(int guestId)
        {
            return _context.LoyaltyPoints
                .Where(lp => lp.GuestId == guestId)
                .Sum(lp => lp.Points);
        }

        /// <summary>
        /// Pobiera aktualny status gościa.
        /// </summary>
        public LoyaltyStatus GetGuestStatus(int guestId)
        {
            var guest = _context.Guests.FirstOrDefault(g => g.Id == guestId);
            return guest?.LoyaltyStatus ?? LoyaltyStatus.Classic;
        }
    }
}
