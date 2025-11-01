namespace HotelManagement.Models
{
    public class NightAuditProgress
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }

        public int Percent { get; set; } = 0;
        public int CurrentStep { get; set; } = 0;

        public List<string> Steps { get; set; } = new()
        {
            "Zamykanie otwartych rachunków",
            "No-show: oznaczenie nieprzyjazdów",
            "Zamknięcie dnia i naliczenia doby",
            "Aktualizacja dostępności pokoi",
            "Przeniesienie daty operacyjnej na kolejny dzień",
            "Generowanie Raportu Dobowego (PDF)"
        };

        public List<string> Messages { get; set; } = new();
        public bool IsCompleted { get; set; } = false;
        public bool IsSuccess { get; set; } = false;

        // Ścieżka (URL) do wygenerowanego raportu dobowego
        public string? ReportPath { get; set; }
    }
}
