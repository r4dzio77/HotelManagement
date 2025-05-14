using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Dodajemy właściwości FirstName i LastName
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        [NotMapped]  // Mówi EF, żeby nie traktować tego jako pole w bazie danych
        public string FullName => $"{FirstName} {LastName}";


        // Relacje
        public Guest Guest { get; set; }  // Powiązanie z Guest
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<WorkShift> Shifts { get; set; } = new List<WorkShift>();
        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
    }
}
