using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nazwa firmy jest wymagana.")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "VAT Number")]
        public string? VatNumber { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(20)]
        [Display(Name = "Kod pocztowy")]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [Phone]
        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        public ICollection<Guest> Guests { get; set; } = new List<Guest>();
    }
}
