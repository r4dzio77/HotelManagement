namespace HotelManagement.Models
{
    public class PublishedSchedule
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public bool IsPublished { get; set; } = false;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }
}
