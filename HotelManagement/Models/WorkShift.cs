using System.Xml.Linq;

namespace HotelManagement.Models

{
    public class WorkShift
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }

}
