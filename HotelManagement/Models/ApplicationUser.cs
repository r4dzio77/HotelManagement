using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        // 🔹 Dział / klasa pracownika (Recepcja, Housekeeping, itp.)
        public string? Department { get; set; }

        public string? Preferences { get; set; }
        public int? GuestId { get; set; }

        [ForeignKey("GuestId")]
        public Guest? Guest { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<WorkShift> Shifts { get; set; } = new List<WorkShift>();

        // 🔧 Preferencje zmian
        public ICollection<ShiftPreference> ShiftPreferences { get; set; } = new List<ShiftPreference>();

        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();

        // 🌐 Google Calendar integracja
        public string? GoogleId { get; set; }               // unikalny identyfikator Google
        public string? GoogleAccessToken { get; set; }      // token dostępu (krótkotrwały)
        public string? GoogleRefreshToken { get; set; }     // token odświeżający (długotrwały)
        public DateTime? GoogleTokenExpiry { get; set; }    // czas wygaśnięcia access tokena
    }
}
