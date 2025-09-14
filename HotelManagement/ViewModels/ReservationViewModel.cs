using HotelManagement.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HotelManagement.ViewModels
{
    public class ReservationViewModel
    {
        public Guest Guest { get; set; } = new Guest();
        public Reservation Reservation { get; set; } = new Reservation();

        public IEnumerable<Service> Services { get; set; } = new List<Service>();
        public List<int> SelectedServiceIds { get; set; } = new List<int>();

        public int PersonCount { get; set; } = 1;
        public bool Breakfast { get; set; }
        public bool Parking { get; set; }
        public bool ExtraBed { get; set; }
        public decimal TotalPrice { get; set; }

        public int? RoomId { get; set; }

        [BindNever]
        public SelectList? RoomTypes { get; set; }

        [BindNever]
        public SelectList? AvailableRooms { get; set; }
    }
}