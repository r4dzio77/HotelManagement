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
using System;
using System.Collections.Generic;
using System.Linq;

public class PdfDocumentGenerator
{
    private readonly HotelManagementContext _context;

    // Fallback, gdy w bazie nie ma firmy lub nie ma danych adresowych
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

        if (doc == null)
            throw new Exception("Nie znaleziono dokumentu");

        var payments = await _context.Payments
            .Where(p => p.ReservationId == doc.ReservationId)
            .OrderBy(p => p.PaidAt)
            .ToListAsync();

        // Dane firmy (sprzedawcy)
        var company = await _context.Companies.FirstOrDefaultAsync();

        var sellerName = company?.Name ?? issuerName;

        var sellerAddressLines = new List<string>();

        // Budujemy adres firmy tylko z pól, które istnieją w modelu Company
        if (company != null)
        {
            // np. "15-222 Białystok"
            if (!string.IsNullOrWhiteSpace(company.PostalCode) || !string.IsNullOrWhiteSpace(company.City))
            {
                sellerAddressLines.Add($"{company.PostalCode} {company.City}".Trim());
            }

            // np. "Polska"
            if (!string.IsNullOrWhiteSpace(company.Country))
            {
                sellerAddressLines.Add(company.Country);
            }
        }

        // Jeśli nie udało się zbudować adresu firmy z bazy – użyj fallbacku
        if (!sellerAddressLines.Any())
        {
            sellerAddressLines.Add(issuerAddress);
        }

        var sellerNip = !string.IsNullOrWhiteSpace(company?.VatNumber)
            ? $"NIP: {company.VatNumber}"
            : issuerNip;

        var sellerContactLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(company?.Phone))
            sellerContactLines.Add($"Tel.: {company.Phone}");
        if (!string.IsNullOrWhiteSpace(company?.Email))
            sellerContactLines.Add($"E-mail: {company.Email}");

        var fileName = doc.Number.Replace("/", "-") + ".pdf";
        var folder = doc.Type == DocumentType.Receipt ? "receipts" : "invoices";
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        Console.WriteLine("🔧 Ścieżka zapisu PDF: " + outputPath);

        try
        {
            var vatRate = 0.23m;
            var culture = new CultureInfo("pl-PL");

            // Wyliczenia
            var nights = (doc.Reservation.CheckOut - doc.Reservation.CheckIn).Days;
            if (nights <= 0) nights = 1; // bezpieczeństwo

            var unitPrice = doc.Reservation.TotalPrice / nights;

            var items = new List<(string Name, int Qty, decimal UnitPrice)>
            {
                ("Nocleg", nights, unitPrice)
            };

            if (doc.Reservation.Breakfast)
                items.Add(("Śniadanie", nights * doc.Reservation.PersonCount, 40m));

            if (doc.Reservation.Parking)
                items.Add(("Parking", nights, 20m));

            if (doc.Reservation.ExtraBed)
                items.Add(("Dodatkowe łóżko", nights, 30m));

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
                buyer = $"{doc.Reservation.Guest.FirstName} {doc.Reservation.Guest.LastName}";
                address = "-";
                nip = "";
            }
            else
            {
                buyer = doc.BuyerName ?? $"{doc.Reservation.Guest.FirstName} {doc.Reservation.Guest.LastName}";
                address = doc.BuyerAddress ?? "-";
                nip = string.IsNullOrWhiteSpace(doc.BuyerNip) ? "" : $"NIP: {doc.BuyerNip}";
            }

            var issueDate = doc.IssueDate == default ? DateTime.Now : doc.IssueDate;
            var saleDate = doc.Reservation.CheckOut;
            var roomNumber = doc.Reservation.Room?.Number ?? "-";

            QuestPdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);

                    // GŁÓWNY NAGŁÓWEK
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(2);
                            col.Item().Text(sellerName)
                                .FontSize(20).SemiBold();

                            foreach (var line in sellerAddressLines)
                                col.Item().Text(line);

                            col.Item().Text(sellerNip);

                            foreach (var line in sellerContactLines)
                                col.Item().Text(line)
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                        });

                        row.ConstantItem(220).Column(col =>
                        {
                            col.Spacing(3);
                            col.Item().AlignRight().Text(docTitle)
                                .FontSize(18).SemiBold();

                            col.Item().AlignRight().Text($"Nr: {doc.Number}")
                                .FontSize(11);

                            col.Item().AlignRight().Text($"Data wystawienia: {issueDate:dd.MM.yyyy}")
                                .FontSize(10);

                            col.Item().AlignRight().Text($"Data sprzedaży: {saleDate:dd.MM.yyyy}")
                                .FontSize(10);

                            col.Item().AlignRight().Text($"Pokój: {roomNumber}")
                                .FontSize(10);
                        });
                    });

                    // TREŚĆ
                    page.Content().Column(col =>
                    {
                        col.Spacing(15);

                        // BLOK SPRZEDAWCA / NABYWCA
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                            {
                                c.Spacing(2);
                                c.Item().Text("Sprzedawca").Bold().FontSize(11);
                                c.Item().Text(sellerName);
                                foreach (var line in sellerAddressLines)
                                    c.Item().Text(line);
                                c.Item().Text(sellerNip);
                            });

                            row.ConstantItem(15); // odstęp

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                            {
                                c.Spacing(2);
                                c.Item().Text("Nabywca").Bold().FontSize(11);
                                c.Item().Text(buyer);
                                c.Item().Text(address);
                                if (!string.IsNullOrWhiteSpace(nip))
                                    c.Item().Text(nip);
                            });
                        });

                        // DANE REZERWACJI POD BLOKIEM
                        col.Item().Text(txt =>
                        {
                            txt.Span("Termin pobytu: ").Bold();
                            txt.Span($"{doc.Reservation.CheckIn:dd.MM.yyyy} - {doc.Reservation.CheckOut:dd.MM.yyyy}");
                        });

                        var lastPaymentMethod = payments.LastOrDefault()?.Method.ToString() ?? "-";
                        col.Item().Text(txt =>
                        {
                            txt.Span("Forma płatności: ").Bold();
                            txt.Span(lastPaymentMethod);
                        });

                        // TABELA POZYCJI
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
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nazwa").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Ilość").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Cena jedn.").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Netto").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("VAT").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Brutto").Bold();
                            });

                            foreach (var i in items)
                            {
                                var grossUnit = i.UnitPrice;
                                var grossTotal = i.Qty * grossUnit;
                                var netUnit = Math.Round(grossUnit / (1 + vatRate), 2);
                                var vatUnit = grossUnit - netUnit;
                                var netTotal = netUnit * i.Qty;
                                var vatTotal = vatUnit * i.Qty;

                                table.Cell().PaddingVertical(2).Text(i.Name);
                                table.Cell().PaddingVertical(2).AlignRight().Text(i.Qty.ToString());
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{grossUnit.ToString("N2", culture)} zł");
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{netTotal.ToString("N2", culture)} zł");
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{vatTotal.ToString("N2", culture)} zł");
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{grossTotal.ToString("N2", culture)} zł");
                            }
                        });

                        // PODSUMOWANIE
                        col.Item().AlignRight().Text($"Suma netto: {totalNet.ToString("N2", culture)} zł").FontSize(11);
                        col.Item().AlignRight().Text($"Suma VAT: {totalVat.ToString("N2", culture)} zł").FontSize(11);
                        col.Item().AlignRight().Text($"Razem brutto: {totalGross.ToString("N2", culture)} zł")
                            .FontSize(12).Bold();

                        col.Item().AlignRight().Text($"Zapłacono: {paid.ToString("N2", culture)} zł").FontSize(11);
                        var remaining = Math.Max(doc.TotalAmount - paid, 0);
                        col.Item().AlignRight().Text($"Pozostało do zapłaty: {remaining.ToString("N2", culture)} zł")
                            .FontSize(11).Bold()
                            .FontColor(remaining > 0 ? Colors.Red.Medium : Colors.Green.Darken2);

                        // PŁATNOŚCI – szczegóły
                        col.Item().PaddingTop(10).Text("Historia płatności").Bold().FontSize(12);
                        if (!payments.Any())
                        {
                            col.Item().Text("Brak zarejestrowanych płatności.").FontSize(10);
                        }
                        else
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(x =>
                                {
                                    x.ConstantColumn(120); // Data
                                    x.RelativeColumn();    // Metoda
                                    x.ConstantColumn(120); // Kwota
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Data").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Metoda").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Kwota").Bold();
                                });

                                foreach (var p in payments)
                                {
                                    table.Cell().PaddingVertical(2).Text(p.PaidAt.ToString("dd.MM.yyyy HH:mm"));
                                    table.Cell().PaddingVertical(2).Text(p.Method.ToString());
                                    table.Cell().PaddingVertical(2).AlignRight()
                                         .Text($"{p.Amount.ToString("N2", culture)} zł");
                                }
                            });
                        }

                        // MIEJSCE NA PODPIS
                        col.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(".................................................")
                                    .AlignLeft();
                                c.Item().Text("Podpis osoby upoważnionej")
                                    .FontSize(9).AlignLeft().FontColor(Colors.Grey.Darken1);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(".................................................")
                                    .AlignRight();
                                c.Item().Text("Podpis odbiorcy")
                                    .FontSize(9).AlignRight().FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    // STOPKA
                    page.Footer().AlignCenter()
                        .Text("Dokument wygenerowany elektronicznie w systemie Hotel Management")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
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
