using System;
using System.Threading.Tasks;

namespace HotelManagement.Services
{
    public interface IBusinessDateProvider
    {
        /// <summary>
        /// Zwraca aktualną datę operacyjną (dzień hotelowy).
        /// </summary>
        Task<DateTime> GetCurrentBusinessDateAsync();

        /// <summary>
        /// Przesuwa datę operacyjną na kolejny dzień i zapisuje do bazy.
        /// </summary>
        Task RollToNextDayAsync(string? userId);

        /// <summary>
        /// Ustawia konkretną datę operacyjną (ręczna synchronizacja przez uprawnionego użytkownika).
        /// </summary>
        Task SetCurrentBusinessDateAsync(DateTime date, string? userId);
    }
}
