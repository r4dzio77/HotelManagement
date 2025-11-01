using System;
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
            var state = await _context.BusinessDateStates.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new BusinessDateState
                {
                    Id = 1,
                    CurrentDate = DateTime.UtcNow.Date
                };
                _context.BusinessDateStates.Add(state);
                await _context.SaveChangesAsync();
            }
            return state.CurrentDate;
        }

        public async Task RollToNextDayAsync(string? userId)
        {
            var state = await _context.BusinessDateStates.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new BusinessDateState { Id = 1, CurrentDate = DateTime.UtcNow.Date };
                _context.BusinessDateStates.Add(state);
            }

            state.CurrentDate = state.CurrentDate.AddDays(1);
            state.LastAuditAtUtc = DateTime.UtcNow;
            state.LastAuditUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task SetCurrentBusinessDateAsync(DateTime date, string? userId)
        {
            var state = await _context.BusinessDateStates.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new BusinessDateState { Id = 1, CurrentDate = date.Date };
                _context.BusinessDateStates.Add(state);
            }
            else
            {
                state.CurrentDate = date.Date;
            }

            state.LastAuditAtUtc = DateTime.UtcNow;
            state.LastAuditUserId = userId;

            await _context.SaveChangesAsync();
        }

    }
}
