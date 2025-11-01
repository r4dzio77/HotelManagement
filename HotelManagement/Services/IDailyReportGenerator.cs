using System;
using System.Threading.Tasks;

namespace HotelManagement.Services
{
    public interface IDailyReportGenerator
    {
        /// <summary>
        /// Generuje PDF Raportu Dobowego dla wskazanej daty operacyjnej
        /// i zwraca publiczny URL (w wwwroot) do wygenerowanego pliku.
        /// </summary>
        Task<string> GenerateAsync(DateTime businessDate);
    }
}
