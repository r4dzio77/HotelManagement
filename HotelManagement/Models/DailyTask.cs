using System;
using HotelManagement.Models;

namespace HotelManagement.Models
{
    public class DailyTask
    {
        public int Id { get; set; }

        // 🔑 Data operacyjna (nocny audyt)
        public DateTime BusinessDate { get; set; }

        // 📝 Treść zadania
        public string Title { get; set; } = string.Empty;

        // ✅ Status wykonania
        public bool IsCompleted { get; set; }

        // 👤 Kto oznaczył jako wykonane (pracownik)
        public string? CompletedByUserId { get; set; }
        public ApplicationUser? CompletedByUser { get; set; }

        // ⏱️ Metadane
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
