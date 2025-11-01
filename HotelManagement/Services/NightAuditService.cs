using System;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services
{
    public class NightAuditService
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;

        public NightAuditService(HotelManagementContext context, IBusinessDateProvider businessDate)
        {
            _context = context;
            _businessDate = businessDate;
        }

        public async Task RunAsync(string? userId)
        {
            var businessDate = await _businessDate.GetCurrentBusinessDateAsync();

            var reservationsForStay = await _context.Reservations
                .Include(r => r.RoomType)
                .Include(r => r.Room)
                .Where(r => r.CheckIn.Date <= businessDate && r.CheckOut.Date > businessDate)
                .ToListAsync();

            foreach (var r in reservationsForStay)
            {
                if (r.Status == ReservationStatus.Confirmed)
                    r.Status = ReservationStatus.CheckedIn;

                if (r.RoomType != null)
                    r.TotalPrice += r.RoomType.PricePerNight;

                if (r.Breakfast)
                {
                    var breakfast = await _context.Services.FirstOrDefaultAsync(s => s.Name == "Breakfast" || s.Name == "Śniadanie");
                    if (breakfast != null)
                    {
                        _context.ServiceUsages.Add(new ServiceUsage
                        {
                            ReservationId = r.Id,
                            ServiceId = breakfast.Id,
                            Quantity = Math.Max(1, r.PersonCount)
                        });
                    }
                }

                if (r.Parking)
                {
                    var parking = await _context.Services.FirstOrDefaultAsync(s => s.Name == "Parking");
                    if (parking != null)
                    {
                        _context.ServiceUsages.Add(new ServiceUsage
                        {
                            ReservationId = r.Id,
                            ServiceId = parking.Id,
                            Quantity = 1
                        });
                    }
                }

                if (r.ExtraBed)
                {
                    var extraBed = await _context.Services.FirstOrDefaultAsync(s => s.Name == "ExtraBed" || s.Name == "Dostawka");
                    if (extraBed != null)
                    {
                        _context.ServiceUsages.Add(new ServiceUsage
                        {
                            ReservationId = r.Id,
                            ServiceId = extraBed.Id,
                            Quantity = 1
                        });
                    }
                }
            }

            var toCheckout = await _context.Reservations
                .Include(r => r.Room)
                .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOut.Date <= businessDate)
                .ToListAsync();

            foreach (var r in toCheckout)
            {
                r.Status = ReservationStatus.CheckedOut;
                if (r.Room != null)
                {
                    r.Room.IsDirty = true;
                    r.Room.IsClean = false;
                }
            }

            var noShows = await _context.Reservations
                .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckIn.Date < businessDate)
                .ToListAsync();

            foreach (var r in noShows)
            {
                r.Status = ReservationStatus.NoShow;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Action = "NightAudit",
                Entity = "System",
                UserId = null,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await _businessDate.AdvanceToNextDateAsync(userId);
        }
    }
}
