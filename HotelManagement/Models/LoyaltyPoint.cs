namespace HotelManagement.Models
{
    public class LoyaltyPoint
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        public int Points { get; set; }
        public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    }

}
