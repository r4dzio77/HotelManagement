using System.ComponentModel.DataAnnotations;

namespace HotelManagement.ViewModels
{
    public class CreateReviewViewModel
    {
        public int ReservationId { get; set; }

        [Range(1, 5)]
        public int Cleanliness { get; set; }

        [Range(1, 5)]
        public int Comfort { get; set; }

        [Range(1, 5)]
        public int Staff { get; set; }

        [Range(1, 5)]
        public int Location { get; set; }

        [Range(1, 5)]
        public int ValueForMoney { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}
