using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelManagement.Models
{
    public class Room
    {
        public int Id { get; set; }

        public string Number { get; set; } = string.Empty;

        public int Floor { get; set; }

        public int Capacity { get; set; }

        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string? Tag { get; set; }

        public bool IsClean { get; set; } = true;

        public bool IsDirty { get; set; } = false;

        public bool IsBlocked { get; set; } = false;

        // 🆕 Zakres blokady + powód
        public DateTime? BlockFrom { get; set; }

        public DateTime? BlockTo { get; set; }

        public string? BlockReason { get; set; }

        public int RoomTypeId { get; set; }

        public RoomType? RoomType { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
