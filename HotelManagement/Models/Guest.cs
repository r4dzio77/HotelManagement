using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models
{
    public class Guest
    {
        public int Id { get; set; }

        // Imię i nazwisko
        [Required(ErrorMessage = "Imię jest wymagane.")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nazwisko jest wymagane.")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        // Opcjonalna nazwa firmy
        [MaxLength(100)]
        public string? CompanyName { get; set; }

        // Dane kontaktowe
        [Required(ErrorMessage = "Email jest wymagany.")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Numer telefonu jest wymagany.")]
        [Phone(ErrorMessage = "Nieprawidłowy numer telefonu.")]
        public string PhoneNumber { get; set; } = string.Empty;

        // Preferencje gościa
        public string? Preferences { get; set; }

        // Kolekcja rezerwacji
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();  // Powiązanie z rezerwacjami

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Metody do zarządzania preferencjami
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
