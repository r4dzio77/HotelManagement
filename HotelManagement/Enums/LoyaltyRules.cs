using System.Collections.Generic;

namespace HotelManagement.Enums
{
    public static class LoyaltyRules
    {
        
        public static readonly Dictionary<LoyaltyStatus, int> NightsRequired = new()
        {
            { LoyaltyStatus.Classic, 0 },
            { LoyaltyStatus.Silver, 10 },
            { LoyaltyStatus.Gold, 25 },
            { LoyaltyStatus.Platinum, 50 },
            { LoyaltyStatus.Diamond, 100 }
        };

       
        public static readonly Dictionary<LoyaltyStatus, decimal> Discounts = new()
        {
            { LoyaltyStatus.Classic, 0.00m },
            { LoyaltyStatus.Silver, 0.05m },
            { LoyaltyStatus.Gold, 0.10m },
            { LoyaltyStatus.Platinum, 0.15m },
            { LoyaltyStatus.Diamond, 0.20m }
        };

      
        public static decimal GetDiscount(LoyaltyStatus status)
        {
            return Discounts.TryGetValue(status, out var discount) ? discount : 0m;
        }

        public static LoyaltyStatus GetStatusByNights(int totalNights)
        {
            if (totalNights >= NightsRequired[LoyaltyStatus.Diamond]) return LoyaltyStatus.Diamond;
            if (totalNights >= NightsRequired[LoyaltyStatus.Platinum]) return LoyaltyStatus.Platinum;
            if (totalNights >= NightsRequired[LoyaltyStatus.Gold]) return LoyaltyStatus.Gold;
            if (totalNights >= NightsRequired[LoyaltyStatus.Silver]) return LoyaltyStatus.Silver;
            return LoyaltyStatus.Classic;
        }
    }
}
