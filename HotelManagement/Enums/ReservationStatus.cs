namespace HotelManagement.Enums
{
    public enum ReservationStatus
    {
        Pending = 0,      // złożona ale niepotwierdzona
        Confirmed = 1,    // potwierdzona, ale jeszcze nie zameldowana
        CheckedIn = 2,    // gość zameldowany
        CheckedOut = 3,   // gość wymeldowany
        Cancelled = 4,    // anulowana
        Completed = 5,    // zakończona rezerwacja (np. zarchiwizowana)
        NoShow = 6        // gość nie przyjechał (no-show)
    }
}
