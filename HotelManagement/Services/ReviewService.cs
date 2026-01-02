using HotelManagement.Models;

namespace HotelManagement.Services
{
    public class ReviewService
    {
        public decimal CalculateAverage(IEnumerable<ReviewRating> ratings)
        {
            if (!ratings.Any())
                return 0m;

            return Math.Round(
                (decimal)ratings.Average(r => r.Score),
                2
            );
        }

        public bool CanAddReview(Reservation reservation)
        {
            return reservation.IsClosed && reservation.Review == null;
        }
    }
}
