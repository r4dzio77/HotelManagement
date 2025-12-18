using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QDoc = QuestPDF.Fluent.Document;

namespace HotelManagement.Services
{
    /// <summary>
    /// Generator Raportu Dobowego (nocny audyt).
    /// Pliki trafiają do: wwwroot/reports/daily/DailyReport_yyyyMMdd.pdf
    /// </summary>
    public class DailyReportGenerator : IDailyReportGenerator
    {
        private readonly HotelManagementContext _context;

        public DailyReportGenerator(HotelManagementContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateAsync(DateTime businessDate)
        {
            var day = businessDate.Date;
            var nextDay = day.AddDays(1);

            // 1) Podstawowe informacje o hotelu
            var company = await _context.Companies.FirstOrDefaultAsync();
            var hotelName = company?.Name ?? "HotelManagement";
            var hotelAddress = company?.Address;
            var hotelCity = string.IsNullOrWhiteSpace(company?.City)
                ? null
                : $"{company.City} {company.PostalCode}";
            var hotelPhone = company?.Phone;
            var hotelEmail = company?.Email;

            // 2) Statystyki pokoi i gości
            var totalRooms = await _context.Rooms.CountAsync();

            // Rezerwacje „w domu” (in-house) – pobyty obejmujące daną dobę
            var inHouseReservations = await _context.Reservations
                .Include(r => r.Guest)
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed ||
                     r.Status == ReservationStatus.CheckedIn) &&
                    r.CheckIn < nextDay &&
                    r.CheckOut > day)
                .ToListAsync();

            var occupiedRooms = inHouseReservations
                .Select(r => r.RoomId)
                .Distinct()
                .Count();

            var guestsInHouse = inHouseReservations.Sum(r => r.PersonCount);

            // Śniadania – logika jak w raporcie śniadań
            var breakfastReservations = await _context.Reservations
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed ||
                     r.Status == ReservationStatus.CheckedIn) &&
                    r.Breakfast &&
                    r.CheckIn < day &&
                    day <= r.CheckOut)
                .ToListAsync();

            var totalBreakfasts = breakfastReservations.Sum(r => r.PersonCount);

            // 3) Ruch dobowy rezerwacji
            var arrivalsCount = await _context.Reservations
                .CountAsync(r => r.CheckIn >= day && r.CheckIn < nextDay);

            var departuresCount = await _context.Reservations
                .CountAsync(r => r.CheckOut >= day && r.CheckOut < nextDay);

            var noShowCount = await _context.Reservations
                .CountAsync(r => r.Status == ReservationStatus.NoShow &&
                                 r.CheckIn >= day && r.CheckIn < nextDay);

            // 4) Płatności z danej doby
            var paymentsOfDay = await _context.Payments
                .Include(p => p.Reservation)
                    .ThenInclude(r => r.Guest)
                .Where(p => p.PaidAt >= day && p.PaidAt < nextDay)
                .AsNoTracking()
                .ToListAsync();

            var totalPayments = paymentsOfDay.Sum(p => p.Amount);

            var byMethod = paymentsOfDay
                .GroupBy(p => p.Method)
                .Select(g => new
                {
                    Method = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            // 5) Mini-zestawienie przykładowych rezerwacji in-house (pierwsze 15)
            var inHouseSample = inHouseReservations
                .OrderBy(r => r.RoomId)
                .Take(15)
                .ToList();

            // 6) Ścieżki pliku
            var fileName = $"DailyReport_{businessDate:yyyyMMdd}.pdf";
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "daily");
            Directory.CreateDirectory(folder);
            var fullPath = Path.Combine(folder, fileName);
            var publicUrl = $"/reports/daily/{fileName}";

            // 7) Generowanie PDF
            var culture = new System.Globalization.CultureInfo("pl-PL");

            var doc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.PageColor(Colors.White);

                    // HEADER
                    page.Header().Column(col =>
                    {
                        col.Spacing(2);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(hotelName)
                                    .FontSize(16).SemiBold();

                                if (!string.IsNullOrWhiteSpace(hotelAddress))
                                    c.Item().Text(hotelAddress).FontSize(10);

                                if (!string.IsNullOrWhiteSpace(hotelCity))
                                    c.Item().Text(hotelCity).FontSize(10);

                                if (!string.IsNullOrWhiteSpace(hotelPhone) ||
                                    !string.IsNullOrWhiteSpace(hotelEmail))
                                {
                                    c.Item().Text(text =>
                                    {
                                        text.Span(hotelPhone ?? "").FontSize(9);
                                        if (!string.IsNullOrWhiteSpace(hotelPhone) &&
                                            !string.IsNullOrWhiteSpace(hotelEmail))
                                            text.Span(" · ").FontSize(9);
                                        text.Span(hotelEmail ?? "").FontSize(9);
                                    });
                                }
                            });

                            row.ConstantItem(140).Column(c =>
                            {
                                c.Item().AlignRight().Text("RAPORT DOBOWY")
                                    .FontSize(13).Bold();

                                c.Item().AlignRight().Text(t =>
                                {
                                    t.Span("Data operacyjna: ").FontSize(9);
                                    t.Span(businessDate.ToString("dd.MM.yyyy", culture))
                                        .FontSize(9).SemiBold();
                                });

                                c.Item().AlignRight().Text(t =>
                                {
                                    t.Span("Wygenerowano: ").FontSize(9);
                                    t.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm", culture))
                                        .FontSize(9);
                                });
                            });
                        });

                        col.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten2);
                    });

                    // CONTENT
                    page.Content().Column(col =>
                    {
                        col.Spacing(15);

                        // SEKCJA 1: PODSUMOWANIE DOBY
                        col.Item().Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().Text("1. Podstawowe podsumowanie doby")
                                .FontSize(11).SemiBold();

                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3)
                                    .Padding(6).Column(c =>
                                    {
                                        c.Spacing(2);
                                        c.Item().Text("Pokoje").FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{totalRooms}").FontSize(12).SemiBold();
                                            t.Span(" pokoi ogółem").FontSize(10);
                                        });
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{occupiedRooms}").FontSize(11).SemiBold();
                                            t.Span(" zajętych / ").FontSize(10);
                                            t.Span($"{totalRooms - occupiedRooms}").FontSize(11).SemiBold();
                                            t.Span(" wolnych").FontSize(10);
                                        });
                                    });

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3)
                                    .Padding(6).Column(c =>
                                    {
                                        c.Spacing(2);
                                        c.Item().Text("Goście").FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{guestsInHouse}").FontSize(12).SemiBold();
                                            t.Span(" osób w hotelu").FontSize(10);
                                        });
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{inHouseReservations.Count}").FontSize(11).SemiBold();
                                            t.Span(" aktywnych rezerwacji").FontSize(10);
                                        });
                                    });

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten3)
                                    .Padding(6).Column(c =>
                                    {
                                        c.Spacing(2);
                                        c.Item().Text("Śniadania").FontSize(9).FontColor(Colors.Grey.Darken2);
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{totalBreakfasts}").FontSize(12).SemiBold();
                                            t.Span(" porcji śniadaniowych").FontSize(10);
                                        });
                                        c.Item().Text(t =>
                                        {
                                            t.Span($"{breakfastReservations.Count}").FontSize(11).SemiBold();
                                            t.Span(" rezerwacji ze śniadaniami").FontSize(10);
                                        });
                                    });
                            });
                        });

                        // SEKCJA 2: RUCH DOBOWY REZERWACJI
                        col.Item().Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().Text("2. Ruch dobowy rezerwacji")
                                .FontSize(11).SemiBold();

                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t =>
                                    {
                                        t.Span("Przyjazdy: ").FontSize(10);
                                        t.Span(arrivalsCount.ToString()).FontSize(11).SemiBold();
                                    });
                                    c.Item().Text(t =>
                                    {
                                        t.Span("Wyjazdy: ").FontSize(10);
                                        t.Span(departuresCount.ToString()).FontSize(11).SemiBold();
                                    });
                                    c.Item().Text(t =>
                                    {
                                        t.Span("No-show (dla tej doby): ").FontSize(10);
                                        t.Span(noShowCount.ToString()).FontSize(11).SemiBold();
                                    });
                                });
                            });
                        });

                        // SEKCJA 3: PŁATNOŚCI DOBOWE
                        col.Item().Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().Text("3. Płatności w danej dobie")
                                .FontSize(11).SemiBold();

                            section.Item().Text(t =>
                            {
                                t.Span("Łączna wartość płatności: ").FontSize(10);
                                t.Span($"{totalPayments:0.00} zł").FontSize(11).SemiBold();
                            });

                            if (!byMethod.Any())
                            {
                                section.Item().Text("Brak zarejestrowanych płatności w tej dobie.")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                section.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3); // metoda
                                        cols.RelativeColumn(1); // liczba
                                        cols.RelativeColumn(2); // kwota
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().Text("Metoda płatności")
                                            .FontSize(9).SemiBold();
                                        h.Cell().AlignRight().Text("Liczba transakcji")
                                            .FontSize(9).SemiBold();
                                        h.Cell().AlignRight().Text("Kwota łączna")
                                            .FontSize(9).SemiBold();
                                    });

                                    foreach (var m in byMethod)
                                    {
                                        table.Cell().Text(m.Method.ToString()).FontSize(9);
                                        table.Cell().AlignRight().Text(m.Count.ToString()).FontSize(9);
                                        table.Cell().AlignRight().Text($"{m.Amount:0.00} zł").FontSize(9);
                                    }
                                });
                            }
                        });

                        // SEKCJA 4: LISTA WYBRANYCH REZERWACJI IN-HOUSE
                        col.Item().Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().Text("4. Goście w hotelu (podgląd)")
                                .FontSize(11).SemiBold();

                            if (!inHouseSample.Any())
                            {
                                section.Item().Text("Brak aktywnych rezerwacji w tej dobie.")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                                return;
                            }

                            section.Item().Text("Poniżej lista przykładowych rezerwacji aktualnie w hotelu (maks. 15).")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);

                            section.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1); // pokój
                                    cols.RelativeColumn(3); // gość
                                    cols.RelativeColumn(2); // daty
                                    cols.RelativeColumn(1); // osób
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Text("Pokój").FontSize(9).SemiBold();
                                    h.Cell().Text("Gość").FontSize(9).SemiBold();
                                    h.Cell().Text("Pobyt").FontSize(9).SemiBold();
                                    h.Cell().AlignRight().Text("Osób").FontSize(9).SemiBold();
                                });

                                foreach (var r in inHouseSample)
                                {
                                    table.Cell().Text(r.Room?.Number ?? "-").FontSize(9);
                                    table.Cell().Text($"{r.Guest?.FirstName} {r.Guest?.LastName}".Trim())
                                        .FontSize(9);
                                    table.Cell().Text(
                                            $"{r.CheckIn:dd.MM.yyyy} – {r.CheckOut:dd.MM.yyyy}")
                                        .FontSize(9);
                                    table.Cell().AlignRight().Text(r.PersonCount.ToString()).FontSize(9);
                                }
                            });
                        });
                    });

                    // FOOTER
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("System HotelManagement – Raport dobowy").FontSize(8);
                    });
                });
            });

            doc.GeneratePdf(fullPath);
            return publicUrl;
        }
    }
}
