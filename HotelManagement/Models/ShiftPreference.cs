namespace HotelManagement.Models
{
    public class ShiftPreference
    {
        public int Id { get; set; }

        // Pracownik
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        // Dzień, którego dotyczy preferencja
        public DateTime Date { get; set; }

        // Typ zmiany, której pracownik nie może wykonywać
        public bool CannotWorkDay { get; set; } = false;   // 07:00-19:00
        public bool CannotWorkNight { get; set; } = false; // 19:00-07:00
    }

}
