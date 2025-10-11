namespace HotelManagement.Models.ViewModels
{
    public class RoomAvailabilityViewModel
    {
        public string RoomTypeName { get; set; }
        public Dictionary<DateTime, int> Availability { get; set; } = new();
    }
}
