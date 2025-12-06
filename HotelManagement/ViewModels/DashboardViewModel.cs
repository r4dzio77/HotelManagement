using System;
using System.Collections.Generic;

namespace HotelManagement.ViewModels
{
    public class DashboardBreakfastItem
    {
        public DateTime Date { get; set; }
        public int BreakfastCount { get; set; }
    }

    public class DashboardViewModel
    {
        // Data operacyjna (z nocnego audytu)
        public DateTime BusinessDate { get; set; }

        public int TodayArrivals { get; set; }
        public int TodayStays { get; set; }
        public int TodayDepartures { get; set; }

        public List<DashboardBreakfastItem> BreakfastsNext7Days { get; set; } = new();
    }
}
