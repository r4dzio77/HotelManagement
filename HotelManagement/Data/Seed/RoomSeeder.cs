using HotelManagement.Models;

namespace HotelManagement.Data.Seed
{
    public static class RoomSeeder
    {
        public static void Seed(HotelManagementContext context)
        {
            if (context.Rooms.Any()) return;

            var rooms = new List<Room>();

            for (int floor = 1; floor <= 4; floor++)
            {
                for (int roomTypeId = 1; roomTypeId <= 9; roomTypeId++)
                {
                    string roomNumber = $"{floor}{roomTypeId:D2}";

                    rooms.Add(new Room
                    {
                        Number = roomNumber,
                        Floor = floor,
                        Capacity = GetCapacityForRoomType(roomTypeId),
                        IsClean = true,
                        IsDirty = false,
                        IsBlocked = false,
                        RoomTypeId = roomTypeId
                    });
                }
            }

            context.Rooms.AddRange(rooms);
            context.SaveChanges();
        }

        private static int GetCapacityForRoomType(int roomTypeId)
        {
            return roomTypeId switch
            {
                7 or 8 => 3,
                9 => 4,
                _ => 2
            };
        }

        private static decimal GetPriceForRoomType(int roomTypeId)
        {
            return roomTypeId switch
            {
                1 or 2 => 200,
                3 or 4 => 250,
                5 or 6 => 300,
                7 or 8 => 350,
                9 => 500,
                _ => 200
            };
        }
    }
}
