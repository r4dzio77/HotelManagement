using HotelManagement.Enums;

namespace HotelManagement.Models

{

    public class ReviewRating
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }
        public Review Review { get; set; }

        public RatingCategory Category { get; set; }

        // np. 1–5
        public int Score { get; set; }
    }
}
