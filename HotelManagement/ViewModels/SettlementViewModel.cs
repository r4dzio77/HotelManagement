using HotelManagement.Enums;
using HotelManagement.Models;

public class SettlementViewModel
{
    public Reservation Reservation { get; set; }
    public List<Service> AvailableServices { get; set; } = new();
    public List<ServiceUsage> ServicesUsed { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();

    public int NewServiceId { get; set; }
    public int NewServiceQuantity { get; set; } = 1;

    public decimal TotalToPay { get; set; }
    public decimal AlreadyPaid { get; set; }
    public decimal RemainingToPay { get; set; }

    public decimal NewPaymentAmount { get; set; }
    public PaymentMethod? NewPaymentMethod { get; set; }
    public DocumentType DocumentType { get; set; }

    public bool IsCompany { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyNip { get; set; }
    public string? CompanyAddress { get; set; }
    public string? PersonalName { get; set; }
    public string? PersonalAddress { get; set; }
}
