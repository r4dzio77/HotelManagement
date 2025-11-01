using System;

namespace HotelManagement.Models
{
    public class BusinessDateState
    {
        public int Id { get; set; } = 1;
        public DateTime CurrentDate { get; set; }
        public DateTime? LastAuditAtUtc { get; set; }
        public string? LastAuditUserId { get; set; }
    }
}
