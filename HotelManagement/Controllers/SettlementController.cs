using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class SettlementController : Controller
{
    private readonly HotelManagementContext _context;
    private readonly PdfDocumentGenerator _pdfGen;

    public SettlementController(HotelManagementContext context, PdfDocumentGenerator pdfGen)
    {
        _context = context;
        _pdfGen = pdfGen;
    }

    public IActionResult Settle(int reservationId)
    {
        var reservation = _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
            .Include(r => r.Documents)
            .FirstOrDefault(r => r.Id == reservationId);

        if (reservation == null) return NotFound();

        var payments = _context.Payments
            .Where(p => p.ReservationId == reservationId)
            .ToList();

        decimal additional = reservation.ServicesUsed.Sum(su => su.Quantity * su.Service.Price);
        decimal total = reservation.TotalPrice +
            (reservation.Breakfast ? 40 : 0) +
            (reservation.Parking ? 20 : 0) +
            (reservation.ExtraBed ? 30 : 0) +
            additional;

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
        var reservation = _context.Reservations.Find(model.Reservation.Id);
        if (reservation == null) return NotFound();

        // 🔒 Blokada dodawania usług, jeśli rachunek zamknięty
        if (reservation.IsClosed)
        {
            TempData["Error"] = "Rachunek jest zamknięty – nie można dodać nowych usług.";
            return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
        }

        if (model.NewServiceId > 0)
        {
            var usage = new ServiceUsage
            {
                ReservationId = reservation.Id,
                ServiceId = model.NewServiceId,
                Quantity = model.NewServiceQuantity
            };
            _context.ServiceUsages.Add(usage);
            _context.SaveChanges();
        }

        return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
    }

    [HttpPost]
    public IActionResult AddPayment(SettlementViewModel model)
    {
        var reservation = _context.Reservations.Find(model.Reservation.Id);
        if (reservation == null) return NotFound();

        // 🔒 Blokada dodawania płatności, jeśli rachunek zamknięty
        if (reservation.IsClosed)
        {
            TempData["Error"] = "Rachunek jest zamknięty – nie można dodać nowych płatności.";
            return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
        }

        if (model.NewPaymentAmount > 0 && model.NewPaymentMethod != null)
        {
            var payment = new Payment
            {
                ReservationId = reservation.Id,
                PaidAt = DateTime.Now, // 🕒 lokalny czas
                Amount = model.NewPaymentAmount,
                Method = model.NewPaymentMethod.Value,
                GuestId = reservation.GuestId
            };
            _context.Payments.Add(payment);
            _context.SaveChanges();
        }

        return RedirectToAction("Settle", new { reservationId = reservation.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Finalize(SettlementViewModel model)
    {
        var reservation = _context.Reservations
            .Include(r => r.ServicesUsed).ThenInclude(su => su.Service)
            .Include(r => r.Documents)
            .FirstOrDefault(r => r.Id == model.Reservation.Id);

        if (reservation == null) return NotFound();

        if (reservation.Documents.Any())
        {
            TempData["Error"] = "Rezerwacja została już rozliczona.";
            return RedirectToAction("Settle", new { reservationId = reservation.Id });
        }

        decimal servicesTotal = reservation.ServicesUsed.Sum(su => su.Quantity * su.Service.Price);
        decimal total = reservation.TotalPrice +
                        (reservation.Breakfast ? 40 : 0) +
                        (reservation.Parking ? 20 : 0) +
                        (reservation.ExtraBed ? 30 : 0) +
                        servicesTotal;

        string buyerName = model.IsCompany ? model.CompanyName : model.PersonalName;
        string buyerAddress = model.IsCompany ? model.CompanyAddress : model.PersonalAddress;
        string? buyerNip = model.IsCompany ? model.CompanyNip : null;

        var docCount = _context.Documents
            .Count(d => d.IssueDate.Year == DateTime.UtcNow.Year && d.Type == model.DocumentType);

        string prefix = model.DocumentType switch
        {
            DocumentType.Invoice => "FV",
            DocumentType.Receipt => "PAR",
            DocumentType.InvoiceForeign => "FV-F",
            DocumentType.InvoicePersonal => "FV-I",
            _ => "DOC"
        };

        string number = $"{prefix}/{docCount + 1:D4}/{DateTime.UtcNow:yyyy}";

        var document = new Document
        {
            ReservationId = reservation.Id,
            Type = model.DocumentType,
            IssueDate = DateTime.Now, // 🕒 lokalny czas
            TotalAmount = total,
            BuyerName = buyerName,
            BuyerAddress = buyerAddress,
            BuyerNip = buyerNip,
            Number = number
        };

        _context.Documents.Add(document);

        // 🔒 oznacz rezerwację jako zamkniętą
        reservation.IsClosed = true;

        await _context.SaveChangesAsync();

        try
        {
            await _pdfGen.GenerateAsync(document.Id);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Błąd PDF: " + ex.Message;
        }

        return RedirectToAction("Settle", new { reservationId = reservation.Id });
    }
}
