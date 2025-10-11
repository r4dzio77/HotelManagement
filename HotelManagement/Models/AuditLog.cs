namespace HotelManagement.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}
