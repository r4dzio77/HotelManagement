﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        public string? Preferences { get; set; }
        public int? GuestId { get; set; }

        [ForeignKey("GuestId")]
        public Guest? Guest { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<WorkShift> Shifts { get; set; } = new List<WorkShift>();
        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
    }
}
