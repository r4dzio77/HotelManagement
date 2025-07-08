using System.ComponentModel.DataAnnotations;

namespace HotelManagement.ViewModels
{
    public class AccountSettingsViewModel
    {
        [Required]
        [Display(Name = "Imię")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Nazwisko")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone]
        [Display(Name = "Numer telefonu")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Preferencje")]
        public string Preferences { get; set; }
    }
}
