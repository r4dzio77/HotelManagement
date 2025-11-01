using System;
using System.Threading.Tasks;

namespace HotelManagement.Services
{
    public interface IBusinessDateProvider
    {
        Task<DateTime> GetCurrentBusinessDateAsync();
        Task<DateTime> AdvanceToNextDateAsync(string? userId = null);
    }
}
