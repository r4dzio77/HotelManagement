using HotelManagement.Enums;

namespace HotelManagement.Models
{
    public class Document
    {
        public int Id { get; set; }

        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; }

        public DocumentType Type { get; set; }
        public DateTime IssueDate { get; set; }
        public decimal TotalAmount { get; set; }

        public string Number { get; set; } = string.Empty;

        public string? BuyerName { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerNip { get; set; }
    }
}
