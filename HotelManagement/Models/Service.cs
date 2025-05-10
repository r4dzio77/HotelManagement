namespace HotelManagement.Models
{
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }

        public ICollection<ServiceUsage> Usages { get; set; } = new List<ServiceUsage>();
    }

}
