using System.Xml.Linq;

namespace HotelManagement.Models

{
    public class WorkShift
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public DateTime Date { get; set; }        // dzień zmiany
        public string ShiftType { get; set; } = null!; // "Day" lub "Night"

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }


}
