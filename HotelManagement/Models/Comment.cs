namespace HotelManagement.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }

        public int WorkShiftId { get; set; }
        public WorkShift WorkShift { get; set; }

        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
