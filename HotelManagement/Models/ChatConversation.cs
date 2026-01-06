using System;
using System.Collections.Generic;

namespace HotelManagement.Models
{
    public class ChatConversation
    {
        public int Id { get; set; }

        // 🔐 właściciel rozmowy (zalogowany użytkownik)
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        // 🧾 opcjonalna rezerwacja podana przez użytkownika
        public int? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsClosed { get; set; } = false;

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
