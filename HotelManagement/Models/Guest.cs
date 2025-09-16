using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Enums;

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

        // ====== SYSTEM LOJALNOŚCIOWY ======
        [MaxLength(20)]
        public string? LoyaltyCardNumber { get; set; } // null = brak karty

        public LoyaltyStatus LoyaltyStatus { get; set; } = LoyaltyStatus.Classic;

        public int TotalNights { get; set; } = 0;

        [NotMapped]
        public bool HasLoyaltyCard => !string.IsNullOrEmpty(LoyaltyCardNumber);

        public void AssignLoyaltyCard()
        {
            if (string.IsNullOrEmpty(LoyaltyCardNumber))
                LoyaltyCardNumber = Guid.NewGuid().ToString("N")[..10].ToUpper();
        }

        public void UpdateLoyaltyStatus()
        {
            if (!HasLoyaltyCard)
            {
                LoyaltyStatus = LoyaltyStatus.Classic;
                return;
            }

            LoyaltyStatus = LoyaltyRules.GetStatusByNights(TotalNights);
        }

        public decimal GetDiscountPercentage()
        {
            return LoyaltyRules.GetDiscount(LoyaltyStatus);
        }

        // ====== PREFERENCJE ======
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
