using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using QDoc = QuestPDF.Fluent.Document;
using HotelManagement.Services;

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

        public async Task<IActionResult> ExpectedArrivals(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            var arrivals = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckIn.Date == targetDate && r.Status == ReservationStatus.Confirmed)
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

            var arrivals = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckIn.Date == targetDate && r.Status == ReservationStatus.Confirmed)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().AlignCenter().Text(t =>
                    {
                        t.Span($"Raport: Oczekiwane przyjazdy - {targetDate:dd.MM.yyyy}").Bold().FontSize(18);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(t => t.Span("#").Bold());
                            header.Cell().Text(t => t.Span("Gość").Bold());
                            header.Cell().Text(t => t.Span("Pokój").Bold());
                            header.Cell().Text(t => t.Span("Osób").Bold());
                            header.Cell().Text(t => t.Span("Uwagi").Bold());
                        });

                        int index = 1;
                        foreach (var res in arrivals)
                        {
                            table.Cell().Text(index++.ToString());
                            table.Cell().Text($"{res.Guest.FirstName} {res.Guest.LastName}");
                            table.Cell().Text(res.Room?.Number?.ToString() ?? "—");
                            table.Cell().Text(res.PersonCount.ToString());
                            table.Cell().Text("");
                        }
                    });

                    var genAt = targetDate.Add(DateTime.Now.TimeOfDay);
                    page.Footer().AlignCenter().Text($"Wygenerowano: {genAt:dd.MM.yyyy HH:mm}");
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Arrivals_{targetDate:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> ExpectedDepartures(DateTime? date)
        {
            var targetDate = (date?.Date) ?? await _businessDate.GetCurrentBusinessDateAsync();

            var departures = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckOut.Date == targetDate && r.Status == ReservationStatus.Confirmed)
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

            var departures = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckOut.Date == targetDate && r.Status == ReservationStatus.Confirmed)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().AlignCenter().Text(t =>
                    {
                        t.Span($"Raport: Oczekiwane wyjazdy - {targetDate:dd.MM.yyyy}").Bold().FontSize(18);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(t => t.Span("#").Bold());
                            header.Cell().Text(t => t.Span("Gość").Bold());
                            header.Cell().Text(t => t.Span("Pokój").Bold());
                            header.Cell().Text(t => t.Span("Osób").Bold());
                            header.Cell().Text(t => t.Span("Uwagi").Bold());
                        });

                        int index = 1;
                        foreach (var res in departures)
                        {
                            table.Cell().Text(index++.ToString());
                            table.Cell().Text($"{res.Guest.FirstName} {res.Guest.LastName}");
                            table.Cell().Text(res.Room?.Number?.ToString() ?? "—");
                            table.Cell().Text(res.PersonCount.ToString());
                            table.Cell().Text("");
                        }
                    });

                    var genAt = targetDate.Add(DateTime.Now.TimeOfDay);
                    page.Footer().AlignCenter().Text($"Wygenerowano: {genAt:dd.MM.yyyy HH:mm}");
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Departures_{targetDate:yyyyMMdd}.pdf");
        }

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
                    r.Status == ReservationStatus.Confirmed &&
                    r.CheckIn.Date <= targetDate && r.CheckOut.Date >= targetDate);

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

                return new
                {
                    room.Number,
                    RoomType = room.RoomType?.Name ?? "—",
                    Status = roomStatus,
                    Occupancy = occupancyStatus,
                    Guest = guest
                };
            }).OrderBy(r => r.Number).ToList();

            ViewBag.SelectedDate = targetDate;
            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportRoomStatus(DateTime? date)
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
                    r.Status == ReservationStatus.Confirmed &&
                    r.CheckIn.Date <= targetDate && r.CheckOut.Date >= targetDate);

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

                return new
                {
                    room.Number,
                    RoomType = room.RoomType?.Name ?? "—",
                    Status = roomStatus,
                    Occupancy = occupancyStatus,
                    Guest = guest
                };
            }).OrderBy(r => r.Number).ToList();

            var pdfDoc = QDoc.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().AlignCenter().Text(t =>
                    {
                        t.Span($"Raport: Status pokoi – {targetDate:dd.MM.yyyy}").Bold().FontSize(18);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(t => t.Span("#").Bold());
                            header.Cell().Text(t => t.Span("Pokój").Bold());
                            header.Cell().Text(t => t.Span("Typ pokoju").Bold());
                            header.Cell().Text(t => t.Span("Status pokoju").Bold());
                            header.Cell().Text(t => t.Span("Zajętość").Bold());
                            header.Cell().Text(t => t.Span("Gość").Bold());
                        });

                        int index = 1;
                        foreach (var r in report)
                        {
                            table.Cell().Text(index++.ToString());
                            table.Cell().Text(r.Number);
                            table.Cell().Text(r.RoomType);
                            table.Cell().Text(r.Status);
                            table.Cell().Text(r.Occupancy);
                            table.Cell().Text(r.Guest);
                        }
                    });

                    var genAt = targetDate.Add(DateTime.Now.TimeOfDay);
                    page.Footer().AlignCenter().Text($"Wygenerowano: {genAt:dd.MM.yyyy HH:mm}");
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Room_Status_{targetDate:yyyyMMdd}.pdf");
        }

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
                        col.Item().AlignCenter().Text(t => t.Span($"Raport śniadań – {day:dd.MM.yyyy}").Bold().FontSize(18));
                        col.Item().AlignCenter().Text($"Łącznie śniadań: {total}");
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(t => t.Span("#").Bold());
                            header.Cell().Text(t => t.Span("Pokój").Bold());
                            header.Cell().Text(t => t.Span("Gość").Bold());
                            header.Cell().Text(t => t.Span("Ilość").Bold());
                        });

                        int i = 1;
                        foreach (var r in rows)
                        {
                            table.Cell().Text((i++).ToString());
                            table.Cell().Text(r.RoomNumber);
                            table.Cell().Text(r.GuestName);
                            table.Cell().Text(r.Quantity.ToString());
                        }

                        if (!rows.Any())
                        {
                            table.Cell().ColumnSpan(4).Text("Brak śniadań w tym dniu.");
                        }
                    });

                    var genAt = day.Add(DateTime.Now.TimeOfDay);
                    page.Footer().AlignCenter().Text($"Wygenerowano: {genAt:dd.MM.yyyy HH:mm}");
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
    }
}
