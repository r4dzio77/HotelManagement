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
                int count = 0;

                for (int number = 1; number <= 26; number++)
                {
                    if (number == 13) continue; // pomijamy 13

                    string roomNumber = $"{floor}{number:D2}";
                    int roomTypeId;

                    // Przydział według końcówki numeru i ilości
                    if (number == 1)
                    {
                        roomTypeId = 9; // Apartament
                    }
                    else if (number == 2)
                    {
                        roomTypeId = 5; // Privilege Double
                    }
                    else if (number == 3)
                    {
                        roomTypeId = 7; // Privilege Double + Sofa
                    }
                    else if (number >= 4 && number <= 8)
                    {
                        roomTypeId = 3; // Superior Double
                    }
                    else if (number >= 9 && number <= 13)
                    {
                        roomTypeId = 4; // Superior Twin
                    }
                    else if ((number % 2) == 0)
                    {
                        roomTypeId = 1; // Standard Double
                    }
                    else
                    {
                        roomTypeId = 2; // Standard Twin
                    }

                    rooms.Add(new Room
                    {
                        Number = roomNumber,
                        Floor = floor,
                        Capacity = GetCapacityForRoomType(roomTypeId),
                        PricePerNight = GetPriceForRoomType(roomTypeId),
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
