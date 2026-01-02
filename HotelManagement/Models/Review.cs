namespace HotelManagement.Models
{
    public class Review
    {
        public int Id { get; set; }

        // 🔗 Rezerwacja – tylko po niej można wystawić opinię
        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; }

        // 🔗 Gość (denormalizacja – wygodne do list)
        public int GuestId { get; set; }
        public Guest Guest { get; set; }

        // 📝 Treść opinii
        public string? Comment { get; set; }

        // 📊 Ocena końcowa (liczona automatycznie)
        public decimal AverageRating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔢 Oceny cząstkowe
        public ICollection<ReviewRating> Ratings { get; set; } = new List<ReviewRating>();
    }
}
