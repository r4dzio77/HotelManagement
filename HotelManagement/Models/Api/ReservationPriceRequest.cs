namespace HotelManagement.Models
{
    public class ReservationPriceRequest
    {
        public int RoomTypeId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public bool Breakfast { get; set; }
        public bool Parking { get; set; }

        public bool Pet { get; set; }
        public bool ExtraBed { get; set; }
        public int PersonCount { get; set; }
        public List<int> SelectedServiceIds { get; set; } = new();
    }
}
