using System;
using System.Collections.Generic;

namespace HotelManagement.Models
{
    public class WorkShift
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Dzień zmiany (bez części czasu).
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Godzina rozpoczęcia zmiany (np. 07:00).
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Godzina zakończenia zmiany (np. 15:00).
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Id grafiku, do którego należy ta zmiana.
        /// </summary>
        public int WorkScheduleId { get; set; }
        public WorkSchedule WorkSchedule { get; set; } = null!;

        /// <summary>
        /// Identyfikator eventu w Google Calendar (jeśli został zsynchronizowany).
        /// </summary>
        public string? GoogleEventId { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
