namespace HotelManagement.Models
{
    public class LoyaltyPoint
    {
        public int Id { get; set; }

        // 🔹 Opcjonalne powiązanie z kontem użytkownika (ApplicationUser)
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // 🔹 Obowiązkowe powiązanie z gościem
        public int GuestId { get; set; }
        public Guest Guest { get; set; }

        public int Points { get; set; }
        public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

        public string Reason { get; set; } = "Za nocleg";
    }
}
