using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HotelManagement.Models;
using HotelManagement.Enums;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Data;
using QuestPdfDocument = QuestPDF.Fluent.Document;
using QuestPDF.Fluent;
using System.IO;
using System.Globalization;

public class PdfDocumentGenerator
{
    private readonly HotelManagementContext _context;
    private readonly string issuerName = "Hotel Management";
    private readonly string issuerAddress = "ul. Prosta 5, 15-222 Białystok";
    private readonly string issuerNip = "NIP: 4565678907";

    public PdfDocumentGenerator(HotelManagementContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(int documentId)
    {
        Console.WriteLine("✔ Start generowania PDF dla dokumentu ID: " + documentId);

        var doc = await _context.Documents
            .Include(d => d.Reservation).ThenInclude(r => r.Guest)
            .Include(d => d.Reservation).ThenInclude(r => r.Room)
            .Include(d => d.Reservation).ThenInclude(r => r.ServicesUsed).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (doc == null) throw new Exception("Nie znaleziono dokumentu");

        var payments = await _context.Payments
            .Where(p => p.ReservationId == doc.ReservationId)
            .ToListAsync();

        var fileName = doc.Number.Replace("/", "-") + ".pdf";
        var folder = doc.Type == DocumentType.Receipt ? "receipts" : "invoices";
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        Console.WriteLine("🔧 Ścieżka zapisu PDF: " + outputPath);

        try
        {
            var vatRate = 0.23m;

            // Wyliczenia
            var nights = (doc.Reservation.CheckOut - doc.Reservation.CheckIn).Days;
            var unitPrice = nights > 0 ? doc.Reservation.TotalPrice / nights : doc.Reservation.TotalPrice;

            var items = new List<(string Name, int Qty, decimal UnitPrice)>
            {
                ("Nocleg", nights, unitPrice)
            };

            if (doc.Reservation.Breakfast)
                items.Add(("Śniadanie", nights * doc.Reservation.PersonCount, 40));

            if (doc.Reservation.Parking)
                items.Add(("Parking", nights, 20));

            if (doc.Reservation.ExtraBed)
                items.Add(("Dodatkowe łóżko", nights, 30));

            foreach (var s in doc.Reservation.ServicesUsed)
                items.Add((s.Service.Name, s.Quantity, s.Service.Price));

            var paid = payments.Sum(p => p.Amount);
            var totalGross = items.Sum(i => i.UnitPrice * i.Qty);
            var totalNet = Math.Round(totalGross / (1 + vatRate), 2);
            var totalVat = totalGross - totalNet;

            var docTitle = doc.Type switch
            {
                DocumentType.Invoice => "Faktura VAT",
                DocumentType.Receipt => "Paragon fiskalny",
                DocumentType.InvoiceForeign => "Faktura zagraniczna",
                DocumentType.InvoicePersonal => "Faktura imienna",
                _ => "Dokument sprzedaży"
            };

            string buyer;
            string address;
            string nip;

            if (doc.Type == DocumentType.Receipt)
            {
                buyer = doc.Reservation.Guest.FirstName + " " + doc.Reservation.Guest.LastName;
                address = "-";
                nip = "";
            }
            else
            {
                buyer = doc.BuyerName ?? "-";
                address = doc.BuyerAddress ?? "-";
                nip = string.IsNullOrWhiteSpace(doc.BuyerNip) ? "" : $"NIP: {doc.BuyerNip}";
            }

            var docDate = DateTime.Now.ToString("dd.MM.yyyy");
            var roomNumber = doc.Reservation.Room?.Number ?? "-";

            QuestPdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);

                    page.Header().Text(docTitle)
                        .FontSize(22).Bold().AlignCenter();

                    page.Content().Column(col =>
                    {
                        col.Spacing(15);

                        // Dane sprzedawcy i nabywcy
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Spacing(2);
                                c.Item().Text("Sprzedawca:").Bold();
                                c.Item().Text(issuerName);
                                c.Item().Text(issuerAddress);
                                c.Item().Text(issuerNip);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Spacing(2);
                                c.Item().Text("Nabywca:").Bold();
                                c.Item().Text(buyer);
                                c.Item().Text(address);
                                if (!string.IsNullOrWhiteSpace(nip))
                                    c.Item().Text(nip);
                            });
                        });

                        // Dane rezerwacji
                        col.Item().Text($"Data wystawienia: {docDate}  |  Nr: {doc.Number}");
                        col.Item().Text($"Pokój: {roomNumber} | Termin: {doc.Reservation.CheckIn:dd.MM.yyyy} - {doc.Reservation.CheckOut:dd.MM.yyyy}");
                        col.Item().Text($"Forma płatności: {payments.LastOrDefault()?.Method.ToString() ?? "-"}");

                        // Tabela pozycji
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(x =>
                            {
                                x.RelativeColumn(4); // Nazwa
                                x.RelativeColumn(1); // Ilość
                                x.RelativeColumn(2); // Cena jedn.
                                x.RelativeColumn(2); // Netto
                                x.RelativeColumn(2); // VAT
                                x.RelativeColumn(2); // Brutto
                            });

                            table.Header(h =>
                            {
                                h.Cell().Text("Nazwa").Bold();
                                h.Cell().Text("Ilość").Bold();
                                h.Cell().Text("Cena jedn.").Bold();
                                h.Cell().Text("Netto").Bold();
                                h.Cell().Text("VAT").Bold();
                                h.Cell().Text("Brutto").Bold();
                            });

                            foreach (var i in items)
                            {
                                var grossUnit = i.UnitPrice;
                                var grossTotal = i.Qty * grossUnit;
                                var netUnit = Math.Round(grossUnit / (1 + vatRate), 2);
                                var vatUnit = grossUnit - netUnit;
                                var netTotal = netUnit * i.Qty;
                                var vatTotal = vatUnit * i.Qty;

                                table.Cell().Text(i.Name);
                                table.Cell().Text(i.Qty.ToString());
                                table.Cell().Text($"{grossUnit:0.00} zł");
                                table.Cell().Text($"{netTotal:0.00} zł");
                                table.Cell().Text($"{vatTotal:0.00} zł");
                                table.Cell().Text($"{grossTotal:0.00} zł");
                            }
                        });

                        // Podsumowanie
                        col.Item().AlignRight().Text($"Suma netto: {totalNet:0.00} zł").FontSize(12);
                        col.Item().AlignRight().Text($"Suma VAT: {totalVat:0.00} zł").FontSize(12);
                        col.Item().AlignRight().Text($"Razem brutto: {totalGross:0.00} zł").FontSize(13).Bold();

                        col.Item().AlignRight().Text($"Zapłacono: {paid:0.00} zł").FontSize(12);
                        col.Item().AlignRight().Text($"Pozostało: {Math.Max(doc.TotalAmount - paid, 0):0.00} zł")
                            .FontSize(12).Bold().FontColor(Colors.Red.Medium);
                    });

                    page.Footer().AlignCenter()
                        .Text("Wygenerowano przez system Hotel Management")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf(outputPath);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Błąd PDF: " + ex.Message);
            throw;
        }
    }
}
