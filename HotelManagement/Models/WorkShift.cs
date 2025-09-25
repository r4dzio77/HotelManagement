using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelManagement.Models
{
    public class WorkShift
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public string ShiftType { get; set; } = string.Empty;

        // ⏰ opcjonalnie, jeśli już masz
        public TimeSpan? StartHour { get; set; }
        public TimeSpan? EndHour { get; set; }

        // Powiązanie z użytkownikiem
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        // 🔑 Google Calendar
        public string? GoogleEventId { get; set; }
    }
}
