using HotelManagement.Models;

namespace HotelManagement.Data.Seed
{
    public static class RoomTypeSeeder
    {
        public static void Seed(HotelManagementContext context)
        {
            Console.WriteLine("Seeder startuje...");

            var roomTypes = new List<RoomType>
{
    new RoomType { Id = 1, Code = "dbc", Name = "Standard Double King-size", Capacity = 2, Description = "Pokój standardowy z dużym łóżkiem king-size.", PricePerNight = 200, ImagePath = "/images/roomtypes/standard_double.jpg" },
    new RoomType { Id = 2, Code = "twc", Name = "Standard Twin", Capacity = 2, Description = "Pokój standardowy z dwoma pojedynczymi łóżkami.", PricePerNight = 200, ImagePath = "/images/roomtypes/standard_twin.jpg" },
    new RoomType { Id = 3, Code = "dbbz", Name = "Superior Double King-size", Capacity = 2, Description = "Wyższy standard z dużym łóżkiem king-size.", PricePerNight = 250, ImagePath = "/images/roomtypes/superior_double.jpg" },
    new RoomType { Id = 4, Code = "twb", Name = "Superior Twin", Capacity = 2, Description = "Wyższy standard z dwoma pojedynczymi łóżkami.", PricePerNight = 250, ImagePath = "/images/roomtypes/superior_twin.jpg" },
    new RoomType { Id = 5, Code = "dbb", Name = "Privilege Double King-size", Capacity = 2, Description = "Luksusowy pokój z łóżkiem king-size i dodatkowymi udogodnieniami.", PricePerNight = 300, ImagePath = "/images/roomtypes/privilege_double.jpg" },
    new RoomType { Id = 6, Code = "twbz", Name = "Privilege Twin", Capacity = 2, Description = "Luksusowy pokój z dwoma łóżkami pojedynczymi i udogodnieniami.", PricePerNight = 300, ImagePath = "/images/roomtypes/privilege_twin.jpg" },
    new RoomType { Id = 7, Code = "dfb", Name = "Privilege Double King-size + Sofa", Capacity = 3, Description = "Pokój privilege z dużym łóżkiem i rozkładaną sofą.", PricePerNight = 350, ImagePath = "/images/roomtypes/privilege_double_sofa.jpg" },
    new RoomType { Id = 8, Code = "tfb", Name = "Privilege Twin + Sofa", Capacity = 3, Description = "Pokój privilege z dwoma łóżkami i sofą dla trzeciej osoby.", PricePerNight = 350, ImagePath = "/images/roomtypes/privilege_twin_sofa.jpg" },
    new RoomType { Id = 9, Code = "sla", Name = "Apartament", Capacity = 4, Description = "Przestronny apartament dla maksymalnie 4 osób z osobnym salonem.", PricePerNight = 500, ImagePath = "/images/roomtypes/apartment.jpg" }
};



            foreach (var roomType in roomTypes)
            {
                if (!context.RoomTypes.Any(rt => rt.Id == roomType.Id))
                {
                    context.RoomTypes.Add(roomType);
                    Console.WriteLine($"Dodano: {roomType.Name} (Id = {roomType.Id})");
                }
                else
                {
                    Console.WriteLine($"Pomijam (już istnieje): {roomType.Name} (Id = {roomType.Id})");
                }
            }

            try
            {
                context.SaveChanges();
                Console.WriteLine("✅ Zapis zakończony sukcesem.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Błąd przy zapisie:");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
