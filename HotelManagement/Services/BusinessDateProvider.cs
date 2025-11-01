using System;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services
{
    public class BusinessDateProvider : IBusinessDateProvider
    {
        private readonly HotelManagementContext _context;

        public BusinessDateProvider(HotelManagementContext context)
        {
            _context = context;
        }

        public async Task<DateTime> GetCurrentBusinessDateAsync()
        {
            var state = await _context.Set<BusinessDateState>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            if (state == null)
            {
                state = new BusinessDateState { Id = 1, CurrentDate = DateTime.UtcNow.Date };
                _context.Add(state);
                await _context.SaveChangesAsync();
            }
            return state.CurrentDate.Date;
        }

        public async Task<DateTime> AdvanceToNextDateAsync(string? userId = null)
        {
            var state = await _context.Set<BusinessDateState>().FirstOrDefaultAsync(x => x.Id == 1);
            if (state == null)
            {
                state = new BusinessDateState { Id = 1, CurrentDate = DateTime.UtcNow.Date };
                _context.Add(state);
            }

            state.CurrentDate = state.CurrentDate.Date.AddDays(1);
            state.LastAuditAtUtc = DateTime.UtcNow;
            state.LastAuditUserId = userId;

            await _context.SaveChangesAsync();
            return state.CurrentDate.Date;
        }
    }
}
