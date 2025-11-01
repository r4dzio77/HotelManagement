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
    private readonly IBusinessDateProvider _businessDate; // ⬅️ data operacyjna

    public SettlementController(HotelManagementContext context, PdfDocumentGenerator pdfGen, IBusinessDateProvider businessDate)
    {
        _context = context;
        _pdfGen = pdfGen;
        _businessDate = businessDate; // ⬅️ zapamiętane
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
                        (reservation.Breakfast ? 40m : 0m) +
                        (reservation.Parking ? 20m : 0m) +
                        (reservation.ExtraBed ? 30m : 0m) +
                        additional;

        decimal paid = payments.Sum(p => p.Amount);
        decimal toPay = total - paid;

        // ⬅️ zaokrąglenie do 2 miejsc
        total = Math.Round(total, 2);
        paid = Math.Round(paid, 2);
        toPay = Math.Round(toPay, 2);

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
    public async Task<IActionResult> AddService(SettlementViewModel model)
    {
        var reservation = await _context.Reservations.FindAsync(model.Reservation.Id);
        if (reservation == null) return NotFound();

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
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddPayment(SettlementViewModel model)
    {
        var reservation = await _context.Reservations.FindAsync(model.Reservation.Id);
        if (reservation == null) return NotFound();

        if (reservation.IsClosed)
        {
            TempData["Error"] = "Rachunek jest zamknięty – nie można dodać nowych płatności.";
            return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
        }

        if (model.NewPaymentAmount > 0 && model.NewPaymentMethod != null)
        {
            var businessToday = await _businessDate.GetCurrentBusinessDateAsync();
            var paidAt = businessToday.Add(DateTime.Now.TimeOfDay);

            var payment = new Payment
            {
                ReservationId = reservation.Id,
                PaidAt = paidAt,
                Amount = Math.Round(model.NewPaymentAmount, 2), // ⬅️ zaokrąglenie
                Method = model.NewPaymentMethod.Value,
                GuestId = reservation.GuestId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
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
            return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
        }

        decimal servicesTotal = reservation.ServicesUsed.Sum(su => su.Quantity * su.Service.Price);
        decimal total = reservation.TotalPrice +
                        (reservation.Breakfast ? 40m : 0m) +
                        (reservation.Parking ? 20m : 0m) +
                        (reservation.ExtraBed ? 30m : 0m) +
                        servicesTotal;

        total = Math.Round(total, 2); // ⬅️ zaokrąglenie

        string buyerName = model.IsCompany ? model.CompanyName : model.PersonalName;
        string buyerAddress = model.IsCompany ? model.CompanyAddress : model.PersonalAddress;
        string? buyerNip = model.IsCompany ? model.CompanyNip : null;

        var businessToday = await _businessDate.GetCurrentBusinessDateAsync();
        int bizYear = businessToday.Year;

        var docCount = _context.Documents
            .Count(d => d.IssueDate.Year == bizYear && d.Type == model.DocumentType);

        string prefix = model.DocumentType switch
        {
            DocumentType.Invoice => "FV",
            DocumentType.Receipt => "PAR",
            DocumentType.InvoiceForeign => "FV-F",
            DocumentType.InvoicePersonal => "FV-I",
            _ => "DOC"
        };

        string number = $"{prefix}/{docCount + 1:D4}/{bizYear}";
        var issueDate = businessToday.Add(DateTime.Now.TimeOfDay);

        var document = new Document
        {
            ReservationId = reservation.Id,
            Type = model.DocumentType,
            IssueDate = issueDate,
            TotalAmount = total,
            BuyerName = buyerName,
            BuyerAddress = buyerAddress,
            BuyerNip = buyerNip,
            Number = number
        };

        _context.Documents.Add(document);
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

        return RedirectToAction(nameof(Settle), new { reservationId = reservation.Id });
    }
}
