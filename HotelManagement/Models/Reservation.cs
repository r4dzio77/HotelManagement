using System;
using System.Collections.Generic;
using HotelManagement.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HotelManagement.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        // Klucz obcy do gościa
        public int GuestId { get; set; }

        [ValidateNever]
        public Guest Guest { get; set; }

        // Klucz obcy do pokoju
        public int? RoomId { get; set; }

        [ValidateNever]
        public Room? Room { get; set; }

        // Typ pokoju
        public int RoomTypeId { get; set; }

        [ValidateNever]
        public RoomType RoomType { get; set; }

        // Daty
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }

        // Status rezerwacji
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

        // Cena
        public decimal TotalPrice { get; set; }
        public int PersonCount { get; set; }

        // Usługi dodatkowe
        public bool Breakfast { get; set; }
        public bool Parking { get; set; }
        public bool ExtraBed { get; set; }

        // Powiązane usługi
        public ICollection<ServiceUsage> ServicesUsed { get; set; } = new List<ServiceUsage>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        // 🔒 Flaga zamknięcia rachunku
        public bool IsClosed { get; set; } = false;
    }
}
