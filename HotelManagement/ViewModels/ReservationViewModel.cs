using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace HotelManagement.Models
{
    public class ReservationViewModel
    {
        [ValidateNever]
        public Guest Guest { get; set; }

        public Reservation Reservation { get; set; }

        [ValidateNever]
        public SelectList RoomTypes { get; set; }

        public int RoomId { get; set; }  // <-- dodajesz to jeœli jeszcze nie istnieje
        public SelectList AvailableRooms { get; set; }  // do listy rozwijalnej


        // Dodatkowe opcje
        public bool Breakfast { get; set; }
        public bool Parking { get; set; }
        public bool ExtraBed { get; set; }
    }
}
