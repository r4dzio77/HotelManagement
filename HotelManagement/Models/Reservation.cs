using System.Reflection.Metadata;
using HotelManagement.Enums;

namespace HotelManagement.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        public int RoomId { get; set; }
        public Room Room { get; set; }

        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

        public ICollection<ServiceUsage> ServicesUsed { get; set; } = new List<ServiceUsage>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }

}
