using HotelManagement.Models;

namespace HotelManagement.ViewModels
{
    public class AccountSettingsViewModel
    {
        // ===== DANE UŻYTKOWNIKA =====
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Preferences { get; set; }

        // ===== REZERWACJE =====
        public IEnumerable<Reservation> ActiveReservations { get; set; }
            = new List<Reservation>();

        public IEnumerable<Reservation> PastReservations { get; set; }
            = new List<Reservation>();
    }
}
