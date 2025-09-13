using System.Xml.Linq;

namespace HotelManagement.Models

{
    public class WorkShift
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public DateTime Date { get; set; }            // Dzień zmiany
        public string ShiftType { get; set; } = null!; // "Day" lub "Night"

        public TimeSpan? StartHour { get; set; }      // np. 07:00
        public TimeSpan? EndHour { get; set; }        // np. 19:00

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }



}
