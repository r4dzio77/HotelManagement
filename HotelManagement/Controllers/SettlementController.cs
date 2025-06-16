using HotelManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Data;
using HotelManagement.Enums;

public class SettlementController : Controller
{
    private readonly HotelManagementContext _context;

    public SettlementController(HotelManagementContext context)
    {
        _context = context;
    }

    public IActionResult Settle(int reservationId)
    {
        var reservation = _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
            .FirstOrDefault(r => r.Id == reservationId);

        if (reservation == null) return NotFound();

        var payments = _context.Payments.Where(p => p.ReservationId == reservationId).ToList();

        decimal servicesTotal = reservation.ServicesUsed.Sum(su => su.Quantity * su.Service.Price);
        decimal total = reservation.TotalPrice + servicesTotal;
        decimal paid = payments.Sum(p => p.Amount);
        decimal toPay = total - paid;

        var model = new SettlementViewModel
        {
            Reservation = reservation,
            AvailableServices = _context.Services.ToList(),
            ServicesUsed = reservation.ServicesUsed.ToList(),
            Payments = payments,
            TotalToPay = total,
            AlreadyPaid = paid,
            RemainingToPay = toPay
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult AddService(SettlementViewModel model)
    {
        if (model.NewServiceId > 0)
        {
            var usage = new ServiceUsage
            {
                ReservationId = model.Reservation.Id,
                ServiceId = model.NewServiceId,
                Quantity = model.NewServiceQuantity
            };
            _context.ServiceUsages.Add(usage);
            _context.SaveChanges();
        }
        return RedirectToAction(nameof(Settle), new { reservationId = model.Reservation.Id });
    }

    [HttpPost]
    public IActionResult Settle(SettlementViewModel model)
    {
        var reservation = _context.Reservations
            .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
            .FirstOrDefault(r => r.Id == model.Reservation.Id);

        if (reservation == null) return NotFound();

        if (model.NewPaymentAmount > 0 && model.NewPaymentMethod != null)
        {
            var payment = new Payment
            {
                ReservationId = reservation.Id,
                PaidAt = DateTime.UtcNow,
                Amount = model.NewPaymentAmount,
                Method = model.NewPaymentMethod.Value,
                GuestId = reservation.GuestId
            };
            _context.Payments.Add(payment);
        }

        decimal servicesTotal = reservation.ServicesUsed.Sum(su => su.Quantity * su.Service.Price);
        decimal total = reservation.TotalPrice + servicesTotal;

        var document = new Document
        {
            ReservationId = reservation.Id,
            Type = model.DocumentType,
            IssueDate = DateTime.UtcNow,
            TotalAmount = total
        };

        _context.Documents.Add(document);
        _context.SaveChanges();

        return RedirectToAction("Index", "Reservations");
    }
}
