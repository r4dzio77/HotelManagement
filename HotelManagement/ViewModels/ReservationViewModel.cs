using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models
{
    public class ReservationViewModel
    {
        [ValidateNever]
        public Guest Guest { get; set; }

        [Required(ErrorMessage = "Rezerwacja jest wymagana.")]
        public Reservation Reservation { get; set; }

        [Required(ErrorMessage = "Wybór pokoju jest wymagany.")]
        public string RoomNumber { get; set; }

        [ValidateNever]
        public SelectList AvailableRooms { get; set; } = new SelectList(Array.Empty<Room>(), "Number", "Number");

        [ValidateNever]
        public SelectList RoomTypes { get; set; } = new SelectList(Array.Empty<RoomType>(), "Id", "Name");

        public bool Breakfast { get; set; }
        public bool Parking { get; set; }
        public bool ExtraBed { get; set; }
    }
}
