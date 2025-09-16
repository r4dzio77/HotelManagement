using HotelManagement.Models;

namespace HotelManagement.ViewModels
{
    public class LoyaltyViewModel
    {
        public string LoyaltyCardNumber { get; set; }
        public string LoyaltyStatus { get; set; }
        public int LoyaltyPoints { get; set; }
        public int TotalNights { get; set; }
        public List<LoyaltyPoint> History { get; set; } = new();
    }
}
