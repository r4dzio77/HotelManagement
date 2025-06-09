namespace HotelManagement.Enums
{
    public enum ReservationStatus
    {
        Pending,     // złożona ale niepotwierdzona
        Confirmed, // potwierdzona, ale jeszcze nie zameldowana
        CheckedIn,    // gość zameldowany
        CheckedOut,  // gość wymeldowany
        Cancelled,   // anulowana
        Completed    // zakończona rezerwacja (np. zarchiwizowana)
    }


}
