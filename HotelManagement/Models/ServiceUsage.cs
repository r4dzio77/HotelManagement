namespace HotelManagement.Models
{
    public class ServiceUsage
    {
        public int Id { get; set; }

        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public int Quantity { get; set; }
    }

}
