using System;
using System.Collections.Generic;

namespace HotelManagement.Models
{
    /// <summary>
    /// Nagłówek grafiku (plan pracy) dla danego miesiąca.
    /// Możesz mieć wiele grafików na ten sam miesiąc z różnymi nazwami.
    /// </summary>
    public class WorkSchedule
    {
        public int Id { get; set; }

        /// <summary>
        /// Rok, którego dotyczy grafik.
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Miesiąc (1-12), którego dotyczy grafik.
        /// </summary>
        public int Month { get; set; }

        /// <summary>
        /// Nazwa grafiku, np. "Grafik główny", "Wersja A", "Święta".
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Czy grafik jest opublikowany (widoczny dla pracowników).
        /// Tylko jeden grafik na dany miesiąc powinien być opublikowany.
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Data utworzenia grafiku.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Kto utworzył grafik (opcjonalnie).
        /// </summary>
        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }

        /// <summary>
        /// Zmiany przypisane do tego grafiku.
        /// </summary>
        public ICollection<WorkShift> Shifts { get; set; } = new List<WorkShift>();
    }
}
