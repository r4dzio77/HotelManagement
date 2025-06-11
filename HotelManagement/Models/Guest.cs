using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models
{
    public class Guest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Imię jest wymagane.")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwisko jest wymagane.")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Numer telefonu jest wymagany.")]
        [Phone(ErrorMessage = "Nieprawidłowy numer telefonu.")]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Preferences { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        public void AddPreference(string preference)
        {
            if (Preferences == null)
                Preferences = preference;
            else
                Preferences += ", " + preference;
        }

        public void RemovePreference(string preference)
        {
            if (Preferences != null)
            {
                Preferences = Preferences.Replace(preference, string.Empty).Trim(',').Trim();
            }
        }
    }
}
