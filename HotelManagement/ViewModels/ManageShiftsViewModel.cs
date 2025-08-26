using HotelManagement.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace HotelManagement.ViewModels
{
    public class ManageShiftsViewModel
    {
        // Lista wszystkich pracowników (rola "Pracownik")
        public IList<ApplicationUser> Employees { get; set; } = new List<ApplicationUser>();

        // Wszystkie zgłoszone dyspozycje (ShiftPreferences)
        public IList<ShiftPreference> Preferences { get; set; } = new List<ShiftPreference>();

        // Możesz też dodać dodatkowe właściwości pomocnicze np. filtr daty
        public DateTime? SelectedMonth { get; set; }
    }

    public class AddEmployeeViewModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string FirstName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string LastName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
