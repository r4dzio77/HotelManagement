using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services
{
    public class RoomAllocatorService
    {
        private readonly HotelManagementContext _context;

        public RoomAllocatorService(HotelManagementContext context)
        {
            _context = context;
        }

        public async Task<Room?> AllocateRoomAsync(int roomTypeId, DateTime checkIn, DateTime checkOut, int? reservationId = null)
        {
            var roomsOfType = await _context.Rooms
                .Where(r => r.RoomTypeId == roomTypeId && r.IsClean && !r.IsBlocked)
                .OrderBy(r => r.Number)
                .ToListAsync();

            foreach (var room in roomsOfType)
            {
                bool isAvailable = await IsRoomAvailableAsync(room.Id, checkIn, checkOut, reservationId);
                if (isAvailable)
                    return room;
            }

            return null;
        }

        public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? reservationId = null)
        {
            return !await _context.Reservations
                .Where(r => r.RoomId == roomId)
                .Where(r => reservationId == null || r.Id != reservationId)
                .AnyAsync(r =>
                    r.CheckIn < checkOut &&
                    r.CheckOut > checkIn);
        }
    }
}
