using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QDoc = QuestPDF.Fluent.Document;
using HotelManagement.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Kierownik,Recepcjonista")]
    public class ReportsController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IBusinessDateProvider _businessDate;

        public ReportsController(HotelManagementContext context, IBusinessDateProvider businessDate)
        {
            _context = context;
            _businessDate = businessDate;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ======= HELPER DO NAGŁÓWKA RAPORTU =======
        private async Task<(string HotelName, string FooterNote)> GetReportBrandingAsync()
        {
            var company = await _context.Companies.FirstOrDefaultAsync();
            var name = company?.Name ?? "HotelManagement";
            var footer = $"Wygenerowano przez system {name}";
            return (name, footer);
        }

        // ================================
        //   OCZEKIWANE PRZYJAZDY
        // ================================
        public async Task<IActionResult> ExpectedArrivals(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            // tylko po dacie przyjazdu – widać też zameldowanych
            var arrivals = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckIn.Date == targetDate)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            ViewBag.SelectedDate = targetDate;
            return View(arrivals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExpectedArrivals(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var arrivals = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckIn.Date == targetDate)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var totalReservations = arrivals.Count;
            var totalPersons = arrivals.Sum(r => r.PersonCount);

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    // HEADER
                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Oczekiwane przyjazdy – {targetDate:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text($"Rezerwacje z datą Check-in równą {targetDate:dd.MM.yyyy}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    // CONTENT
                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        // małe podsumowanie
                        col.Item().Element(c => c
                            .Background(Colors.Grey.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2))
                        .Column(box =>
                        {
                            box.Spacing(3);
                            box.Item().Text("Podsumowanie dnia").SemiBold().FontSize(10);
                            box.Item().Text($"Rezerwacje: {totalReservations} | Osób łącznie: {totalPersons}")
                                .FontSize(9);
                        });

                        // tabelka
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25); // #
                                columns.RelativeColumn(3);  // Gość
                                columns.RelativeColumn(1);  // Pokój
                                columns.RelativeColumn(1);  // Osób
                                columns.RelativeColumn(2);  // Termin
                            });

                            // nagłówek
                            table.Header(header =>
                            {
                                void HeaderCell(string text)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(text).SemiBold().FontSize(9);
                                }

                                HeaderCell("#");
                                HeaderCell("Gość");
                                HeaderCell("Pokój");
                                HeaderCell("Osób");
                                HeaderCell("Termin");
                            });

                            int index = 1;
                            foreach (var res in arrivals)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(index++.ToString()).FontSize(9));

                                var guest = $"{res.Guest.FirstName} {res.Guest.LastName}";
                                Cell(c => c.Text(guest).FontSize(9));

                                var room = res.Room?.Number ?? "—";
                                Cell(c => c.Text(room).FontSize(9));

                                Cell(c => c.Text(res.PersonCount.ToString()).FontSize(9));

                                var term = $"{res.CheckIn:dd.MM.yyyy} – {res.CheckOut:dd.MM.yyyy}";
                                Cell(c => c.Text(term).FontSize(9));
                            }

                            if (!arrivals.Any())
                            {
                                table.Cell().ColumnSpan(5).Element(c => c.Padding(6))
                                    .Text("Brak przyjazdów w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Arrivals_{targetDate:yyyyMMdd}.pdf");
        }

        // ================================
        //   OCZEKIWANE WYJAZDY
        // ================================
        public async Task<IActionResult> ExpectedDepartures(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            var departures = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckOut.Date == targetDate)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            ViewBag.SelectedDate = targetDate;
            return View(departures);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExpectedDepartures(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var departures = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckOut.Date == targetDate)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var totalReservations = departures.Count;
            var totalPersons = departures.Sum(r => r.PersonCount);

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Oczekiwane wyjazdy – {targetDate:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text($"Rezerwacje z datą Check-out równą {targetDate:dd.MM.yyyy}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Element(c => c
                            .Background(Colors.Grey.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2))
                        .Column(box =>
                        {
                            box.Spacing(3);
                            box.Item().Text("Podsumowanie dnia").SemiBold().FontSize(10);
                            box.Item().Text($"Rezerwacje: {totalReservations} | Osób łącznie: {totalPersons}")
                                .FontSize(9);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(text).SemiBold().FontSize(9);
                                }

                                HeaderCell("#");
                                HeaderCell("Gość");
                                HeaderCell("Pokój");
                                HeaderCell("Osób");
                                HeaderCell("Termin");
                            });

                            int index = 1;
                            foreach (var res in departures)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(index++.ToString()).FontSize(9));
                                Cell(c => c.Text($"{res.Guest.FirstName} {res.Guest.LastName}").FontSize(9));
                                Cell(c => c.Text(res.Room?.Number ?? "—").FontSize(9));
                                Cell(c => c.Text(res.PersonCount.ToString()).FontSize(9));
                                Cell(c => c.Text($"{res.CheckIn:dd.MM.yyyy} – {res.CheckOut:dd.MM.yyyy}").FontSize(9));
                            }

                            if (!departures.Any())
                            {
                                table.Cell().ColumnSpan(5).Element(c => c.Padding(6))
                                    .Text("Brak wyjazdów w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Departures_{targetDate:yyyyMMdd}.pdf");
        }

        // ================================
        //   STATUS POKOI
        // ================================
        public async Task<IActionResult> RoomStatus(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            var rooms = await _context.Rooms
                .Include(r => r.RoomType)
                .Include(r => r.Reservations)
                    .ThenInclude(res => res.Guest)
                .ToListAsync();

            var report = rooms.Select(room =>
            {
                var res = room.Reservations.FirstOrDefault(r =>
                    r.CheckIn.Date <= targetDate && r.CheckOut.Date >= targetDate &&
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn));

                string guest = res != null ? $"{res.Guest.FirstName} {res.Guest.LastName}" : "—";

                string occupancyStatus;
                if (res != null && res.CheckOut.Date == targetDate)
                    occupancyStatus = "Wyjazd";
                else if (res != null)
                    occupancyStatus = "Pobyt";
                else
                    occupancyStatus = "Wolny";

                string roomStatus = room.IsBlocked ? "Zablokowany"
                    : room.IsDirty ? "Brudny"
                    : room.IsClean ? "Czysty"
                    : "Nieznany";

                var code = room.RoomType?.Code != null
                    ? room.RoomType.Code.ToUpperInvariant()
                    : "—";

                return new
                {
                    room.Number,
                    RoomCode = code,
                    Status = roomStatus,
                    Occupancy = occupancyStatus,
                    Guest = guest
                };
            })
            .OrderBy(r => r.Number)
            .ToList();

            ViewBag.SelectedDate = targetDate;
            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportRoomStatus(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var rooms = await _context.Rooms
                .Include(r => r.RoomType)
                .Include(r => r.Reservations)
                    .ThenInclude(res => res.Guest)
                .ToListAsync();

            var report = rooms.Select(room =>
            {
                var res = room.Reservations.FirstOrDefault(r =>
                    r.CheckIn.Date <= targetDate && r.CheckOut.Date >= targetDate &&
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn));

                string guest = res != null ? $"{res.Guest.FirstName} {res.Guest.LastName}" : "—";

                string occupancyStatus;
                if (res != null && res.CheckOut.Date == targetDate)
                    occupancyStatus = "Wyjazd";
                else if (res != null)
                    occupancyStatus = "Pobyt";
                else
                    occupancyStatus = "Wolny";

                string roomStatus = room.IsBlocked ? "Zablokowany"
                    : room.IsDirty ? "Brudny"
                    : room.IsClean ? "Czysty"
                    : "Nieznany";

                var code = room.RoomType?.Code != null
                    ? room.RoomType.Code.ToUpperInvariant()
                    : "—";

                return new
                {
                    room.Number,
                    RoomCode = code,
                    Status = roomStatus,
                    Occupancy = occupancyStatus,
                    Guest = guest
                };
            })
            .OrderBy(r => r.Number)
            .ToList();

            var totalRooms = report.Count;
            var occupied = report.Count(r => r.Occupancy == "Pobyt" || r.Occupancy == "Wyjazd");

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Status pokoi – {targetDate:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text("Zestawienie stanu pokoi, zajętości i gości w dniu operacyjnym.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Element(c => c
                            .Background(Colors.Grey.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2))
                        .Column(box =>
                        {
                            box.Spacing(3);
                            box.Item().Text("Podsumowanie pokoi").SemiBold().FontSize(10);
                            box.Item().Text($"Pokoje: {totalRooms} | Zajęte: {occupied} | Wolne: {totalRooms - occupied}")
                                .FontSize(9);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // Pokój
                                columns.RelativeColumn(1); // Kod
                                columns.RelativeColumn(2); // Status pokoju
                                columns.RelativeColumn(2); // Zajętość
                                columns.RelativeColumn(3); // Gość
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("Pokój");
                                H("Kod typu");
                                H("Status pokoju");
                                H("Zajętość");
                                H("Gość");
                            });

                            foreach (var r in report)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(r.Number).FontSize(9));
                                Cell(c => c.Text(r.RoomCode).FontSize(9));
                                Cell(c => c.Text(r.Status).FontSize(9));
                                Cell(c => c.Text(r.Occupancy).FontSize(9));
                                Cell(c => c.Text(r.Guest).FontSize(9));
                            }

                            if (!report.Any())
                            {
                                table.Cell().ColumnSpan(5).Element(c => c.Padding(6))
                                    .Text("Brak pokoi w zestawieniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Room_Status_{targetDate:yyyyMMdd}.pdf");
        }

        // ================================
        //   RAPORT ŚNIADAŃ
        // ================================
        public async Task<IActionResult> BreakfastReport(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            var rows = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Guest)
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn) &&
                    r.Breakfast == true &&
                    r.CheckIn.Date < day && day <= r.CheckOut.Date
                )
                .Select(r => new BreakfastRow
                {
                    RoomNumber = r.Room != null ? r.Room.Number : "—",
                    GuestName = r.Guest != null ? (r.Guest.FirstName + " " + r.Guest.LastName) : "—",
                    Quantity = r.PersonCount
                })
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();

            var vm = new BreakfastReportViewModel
            {
                Date = day,
                TotalBreakfasts = rows.Sum(x => x.Quantity),
                Rows = rows
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportBreakfastReport(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var rows = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Guest)
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn) &&
                    r.Breakfast == true &&
                    r.CheckIn.Date < day && day <= r.CheckOut.Date
                )
                .Select(r => new BreakfastRow
                {
                    RoomNumber = r.Room != null ? r.Room.Number : "—",
                    GuestName = r.Guest != null ? (r.Guest.FirstName + " " + r.Guest.LastName) : "—",
                    Quantity = r.PersonCount
                })
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();

            var total = rows.Sum(x => x.Quantity);

            var doc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Raport śniadań – {day:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text("Lista pokoi z wykupionymi śniadaniami – wsparcie dla gastronomii.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Element(c => c
                            .Background(Colors.Grey.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2))
                        .Column(box =>
                        {
                            box.Item().Text("Podsumowanie").SemiBold().FontSize(10);
                            box.Item().Text($"Łącznie śniadań: {total}").FontSize(9);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("#");
                                H("Pokój");
                                H("Gość");
                                H("Ilość");
                            });

                            int i = 1;
                            foreach (var r in rows)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text((i++).ToString()).FontSize(9));
                                Cell(c => c.Text(r.RoomNumber).FontSize(9));
                                Cell(c => c.Text(r.GuestName).FontSize(9));
                                Cell(c => c.Text(r.Quantity.ToString()).FontSize(9));
                            }

                            if (!rows.Any())
                            {
                                table.Cell().ColumnSpan(4).Element(c => c.Padding(6))
                                    .Text("Brak śniadań w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;

            var fileName = $"Raport_sniadan_{day:yyyyMMdd}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }

        public class BreakfastRow
        {
            public string RoomNumber { get; set; } = string.Empty;
            public string GuestName { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        public class BreakfastReportViewModel
        {
            public DateTime Date { get; set; }
            public int TotalBreakfasts { get; set; }
            public List<BreakfastRow> Rows { get; set; } = new();
        }

        // ================================
        //   RAPORT DOBOWY (DASHBOARD)
        // ================================
        public class PaymentSummaryRow
        {
            public string Method { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Amount { get; set; }
        }

        public class DailySummaryViewModel
        {
            public DateTime Date { get; set; }

            public int TotalRooms { get; set; }
            public int OccupiedRooms { get; set; }
            public int GuestsInHouse { get; set; }
            public int BreakfastCount { get; set; }

            public decimal TotalPayments { get; set; }
            public List<PaymentSummaryRow> PaymentSummaries { get; set; } = new();
        }

        public async Task<IActionResult> DailySummary(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);

            var totalRooms = await _context.Rooms.CountAsync();

            var inHouseReservations = await _context.Reservations
                .Where(r =>
                    r.CheckIn.Date <= day &&
                    r.CheckOut.Date >= day &&
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
                .ToListAsync();

            var occupiedRooms = inHouseReservations
                .Where(r => r.RoomId != null)
                .Select(r => r.RoomId)
                .Distinct()
                .Count();

            var guestsInHouse = inHouseReservations.Sum(r => r.PersonCount);

            var breakfastCount = await _context.Reservations
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn) &&
                    r.Breakfast == true &&
                    r.CheckIn.Date < day && day <= r.CheckOut.Date)
                .SumAsync(r => (int?)r.PersonCount) ?? 0;

            var payments = await _context.Payments
                .Where(p => p.PaidAt >= day && p.PaidAt < nextDay)
                .ToListAsync();

            var paymentSummaries = payments
                .GroupBy(p => p.Method)
                .Select(g => new PaymentSummaryRow
                {
                    Method = g.Key.ToString(),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var vm = new DailySummaryViewModel
            {
                Date = day,
                TotalRooms = totalRooms,
                OccupiedRooms = occupiedRooms,
                GuestsInHouse = guestsInHouse,
                BreakfastCount = breakfastCount,
                TotalPayments = payments.Sum(p => p.Amount),
                PaymentSummaries = paymentSummaries
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportDailySummary(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var totalRooms = await _context.Rooms.CountAsync();

            var inHouseReservations = await _context.Reservations
                .Where(r =>
                    r.CheckIn.Date <= day &&
                    r.CheckOut.Date >= day &&
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
                .ToListAsync();

            var occupiedRooms = inHouseReservations
                .Where(r => r.RoomId != null)
                .Select(r => r.RoomId)
                .Distinct()
                .Count();

            var guestsInHouse = inHouseReservations.Sum(r => r.PersonCount);

            var breakfastCount = await _context.Reservations
                .Where(r =>
                    (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn) &&
                    r.Breakfast == true &&
                    r.CheckIn.Date < day && day <= r.CheckOut.Date)
                .SumAsync(r => (int?)r.PersonCount) ?? 0;

            var payments = await _context.Payments
                .Where(p => p.PaidAt >= day && p.PaidAt < nextDay)
                .ToListAsync();

            var paymentSummaries = payments
                .GroupBy(p => p.Method)
                .Select(g => new PaymentSummaryRow
                {
                    Method = g.Key.ToString(),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var doc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Raport dobowy – {day:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text("Podsumowanie obłożenia, gości, śniadań i płatności.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        // dwa "kafelki" jak w apce: pobyt / finanse
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => c
                                .Background(Colors.Blue.Lighten4)
                                .Padding(10)
                                .Border(1)
                                .BorderColor(Colors.Blue.Lighten2))
                            .Column(box =>
                            {
                                box.Spacing(3);
                                box.Item().Text("Pobyt").SemiBold().FontSize(10);
                                box.Item().Text($"Liczba pokoi: {totalRooms}").FontSize(9);
                                box.Item().Text($"Zajęte pokoje: {occupiedRooms}").FontSize(9);
                                box.Item().Text($"Gości w hotelu: {guestsInHouse}").FontSize(9);
                                box.Item().Text($"Śniadania: {breakfastCount}").FontSize(9);
                            });

                            row.RelativeItem().Element(c => c
                                .Background(Colors.Green.Lighten4)
                                .Padding(10)
                                .Border(1)
                                .BorderColor(Colors.Green.Lighten2))
                            .Column(box =>
                            {
                                box.Spacing(3);
                                box.Item().Text("Płatności").SemiBold().FontSize(10);
                                box.Item().Text($"Łączna kwota: {payments.Sum(p => p.Amount):0.00} zł")
                                    .FontSize(9);
                                box.Item().Text($"Liczba transakcji: {payments.Count}")
                                    .FontSize(9);
                            });
                        });

                        col.Item().PaddingTop(6).Text("Płatności wg metody").SemiBold().FontSize(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("Metoda");
                                H("Liczba");
                                H("Kwota");
                            });

                            foreach (var s in paymentSummaries)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(s.Method).FontSize(9));
                                Cell(c => c.AlignRight().Text(s.Count.ToString()).FontSize(9));
                                Cell(c => c.AlignRight().Text($"{s.Amount:0.00} zł").FontSize(9));
                            }

                            if (!paymentSummaries.Any())
                            {
                                table.Cell().ColumnSpan(3).Element(c => c.Padding(6))
                                    .Text("Brak płatności w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;

            var fileName = $"Daily_Summary_{day:yyyyMMdd}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }

        // ================================
        //   PŁATNOŚCI DNIA
        // ================================
        public async Task<IActionResult> Payments(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);

            var payments = await _context.Payments
                .Include(p => p.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Room)
                .Where(p => p.PaidAt >= day && p.PaidAt < nextDay)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            ViewBag.SelectedDate = day;
            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPayments(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var payments = await _context.Payments
                .Include(p => p.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Room)
                .Where(p => p.PaidAt >= day && p.PaidAt < nextDay)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            var byMethod = payments
                .GroupBy(p => p.Method)
                .Select(g => new
                {
                    Method = g.Key.ToString(),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            var totalAmount = payments.Sum(p => p.Amount);

            var doc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Płatności dnia – {day:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text("Zestawienie wszystkich płatności danego dnia.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Element(c => c
                            .Background(Colors.Grey.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2))
                        .Column(box =>
                        {
                            box.Spacing(3);
                            box.Item().Text("Podsumowanie dnia").SemiBold().FontSize(10);
                            box.Item().Text($"Płatności: {payments.Count} | Kwota: {totalAmount:0.00} zł")
                                .FontSize(9);
                        });

                        col.Item().PaddingTop(4).Text("Podział wg metody").SemiBold().FontSize(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("Metoda");
                                H("Liczba");
                                H("Kwota");
                            });

                            foreach (var m in byMethod)
                            {
                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(m.Method).FontSize(9));
                                Cell(c => c.AlignRight().Text(m.Count.ToString()).FontSize(9));
                                Cell(c => c.AlignRight().Text($"{m.Amount:0.00} zł").FontSize(9));
                            }

                            if (!byMethod.Any())
                            {
                                table.Cell().ColumnSpan(3).Element(c => c.Padding(6))
                                    .Text("Brak płatności w tym dniu.").FontSize(9);
                            }
                        });

                        col.Item().PaddingTop(6).Text("Lista płatności").SemiBold().FontSize(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // Godzina
                                columns.RelativeColumn(2); // Gość
                                columns.RelativeColumn(1); // Pokój
                                columns.RelativeColumn(1); // Metoda
                                columns.RelativeColumn(1); // Kwota
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("Godzina");
                                H("Gość");
                                H("Pokój");
                                H("Metoda");
                                H("Kwota");
                            });

                            foreach (var p in payments)
                            {
                                var guestName = p.Guest != null
                                    ? $"{p.Guest.FirstName} {p.Guest.LastName}"
                                    : (p.Reservation?.Guest != null
                                        ? $"{p.Reservation.Guest.FirstName} {p.Reservation.Guest.LastName}"
                                        : "—");

                                var roomNumber = p.Reservation?.Room?.Number ?? "—";

                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(p.PaidAt.ToString("HH:mm")).FontSize(9));
                                Cell(c => c.Text(guestName).FontSize(9));
                                Cell(c => c.Text(roomNumber).FontSize(9));
                                Cell(c => c.Text(p.Method.ToString()).FontSize(9));
                                Cell(c => c.AlignRight().Text($"{p.Amount:0.00} zł").FontSize(9));
                            }

                            if (!payments.Any())
                            {
                                table.Cell().ColumnSpan(5).Element(c => c.Padding(6))
                                    .Text("Brak płatności w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;

            var fileName = $"Payments_{day:yyyyMMdd}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }

        // ================================
        //   CASH RAPORT (GOTÓWKA)
        // ================================
        public async Task<IActionResult> CashReport(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);

            var cashPayments = await _context.Payments
                .Include(p => p.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Room)
                .Where(p =>
                    p.PaidAt >= day && p.PaidAt < nextDay &&
                    p.Method == PaymentMethod.Cash)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            ViewBag.SelectedDate = day;
            return View(cashPayments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCashReport(DateTime? date)
        {
            var day = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();
            var nextDay = day.AddDays(1);
            var (hotelName, footerNote) = await GetReportBrandingAsync();

            var cashPayments = await _context.Payments
                .Include(p => p.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Guest)
                .Include(p => p.Reservation).ThenInclude(r => r.Room)
                .Where(p =>
                    p.PaidAt >= day && p.PaidAt < nextDay &&
                    p.Method == PaymentMethod.Cash)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            var totalAmount = cashPayments.Sum(p => p.Amount);

            var doc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);

                    page.Header().Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(hotelName).Bold().FontSize(16);
                        col.Item().Text($"Cash raport (gotówka) – {day:dd.MM.yyyy}")
                            .FontSize(12).SemiBold();
                        col.Item().Text("Wszystkie płatności gotówką z wybranego dnia.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Element(c => c
                            .Background(Colors.Green.Lighten4)
                            .Padding(8)
                            .Border(1)
                            .BorderColor(Colors.Green.Lighten2))
                        .Column(box =>
                        {
                            box.Spacing(3);
                            box.Item().Text("Podsumowanie gotówki").SemiBold().FontSize(10);
                            box.Item().Text($"Płatności gotówką: {cashPayments.Count}")
                                .FontSize(9);
                            box.Item().Text($"Łączna kwota: {totalAmount:0.00} zł")
                                .FontSize(9);
                        });

                        col.Item().PaddingTop(6).Text("Lista płatności gotówką").SemiBold().FontSize(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // Godzina
                                columns.RelativeColumn(2); // Gość
                                columns.RelativeColumn(1); // Pokój
                                columns.RelativeColumn(1); // Kwota
                            });

                            table.Header(header =>
                            {
                                void H(string t)
                                {
                                    header.Cell().Element(c => c
                                        .Background(Colors.Grey.Lighten3)
                                        .Padding(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2))
                                        .Text(t).SemiBold().FontSize(9);
                                }

                                H("Godzina");
                                H("Gość");
                                H("Pokój");
                                H("Kwota");
                            });

                            foreach (var p in cashPayments)
                            {
                                var guestName = p.Guest != null
                                    ? $"{p.Guest.FirstName} {p.Guest.LastName}"
                                    : (p.Reservation?.Guest != null
                                        ? $"{p.Reservation.Guest.FirstName} {p.Reservation.Guest.LastName}"
                                        : "—");

                                var roomNumber = p.Reservation?.Room?.Number ?? "—";

                                void Cell(Action<IContainer> config)
                                {
                                    table.Cell().Element(c => c.PaddingVertical(3).BorderBottom(0.5f)
                                        .BorderColor(Colors.Grey.Lighten3)).Element(config);
                                }

                                Cell(c => c.Text(p.PaidAt.ToString("HH:mm")).FontSize(9));
                                Cell(c => c.Text(guestName).FontSize(9));
                                Cell(c => c.Text(roomNumber).FontSize(9));
                                Cell(c => c.AlignRight().Text($"{p.Amount:0.00} zł").FontSize(9));
                            }

                            if (!cashPayments.Any())
                            {
                                table.Cell().ColumnSpan(4).Element(c => c.Padding(6))
                                    .Text("Brak płatności gotówką w tym dniu.").FontSize(9);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text($"{footerNote} · {DateTime.Now:dd.MM.yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            ms.Position = 0;

            var fileName = $"Cash_Report_{day:yyyyMMdd}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }
    }
}
