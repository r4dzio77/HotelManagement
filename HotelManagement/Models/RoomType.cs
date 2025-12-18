using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models
{
    public class RoomType
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Kod typu pokoju")]
        public string? Code { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(1, 10)]
        public int Capacity { get; set; } // 1 lub 2 osoby, ale pozwalamy na rozszerzenie w przyszłości

        [Required]
        [Range(0, 10000)]
        public decimal PricePerNight { get; set; }

        public string? ImagePath { get; set; } // np. "/images/roomtypes/deluxe.jpg"

        public ICollection<Room> Rooms { get; set; } = new List<Room>();

    }
}

