using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;  // Dodajemy Imię
        public string LastName { get; set; } = string.Empty;   // Dodajemy Nazwisko

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<WorkShift> Shifts { get; set; } = new List<WorkShift>();
        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
    }
}
