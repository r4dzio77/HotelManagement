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

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Kierownik,Recepcjonista")]
    public class ReportsController : Controller
    {
        private readonly HotelManagementContext _context;

        public ReportsController(HotelManagementContext context)
        {
            _context = context;
        }

        // 📊 Strona główna raportów
        public IActionResult Index()
        {
            return View();
        }

        // ✅ RAPORT: Oczekiwane przyjazdy
        public async Task<IActionResult> ExpectedArrivals(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

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

        // ✅ PDF: Oczekiwane przyjazdy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExpectedArrivals(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            var arrivals = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckIn.Date == targetDate && r.Status == ReservationStatus.Confirmed)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var pdfDoc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text($"Raport: Oczekiwane przyjazdy - {targetDate:dd.MM.yyyy}")
                        .FontSize(18).Bold().AlignCenter();

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
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Gość").Bold();
                            header.Cell().Text("Pokój").Bold();
                            header.Cell().Text("Osób").Bold();
                            header.Cell().Text("Uwagi").Bold();
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
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Arrivals_{targetDate:yyyyMMdd}.pdf");
        }

        // ✅ RAPORT: Oczekiwane wyjazdy
        public async Task<IActionResult> ExpectedDepartures(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

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

        // ✅ PDF: Oczekiwane wyjazdy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExpectedDepartures(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            var departures = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Include(r => r.RoomType)
                .Where(r => r.CheckOut.Date == targetDate && r.Status == ReservationStatus.Confirmed)
                .OrderBy(r => r.Room != null ? r.Room.Number : "")
                .ToListAsync();

            var pdfDoc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text($"Raport: Oczekiwane wyjazdy - {targetDate:dd.MM.yyyy}")
                        .FontSize(18).Bold().AlignCenter();

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
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Gość").Bold();
                            header.Cell().Text("Pokój").Bold();
                            header.Cell().Text("Osób").Bold();
                            header.Cell().Text("Uwagi").Bold();
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
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Expected_Departures_{targetDate:yyyyMMdd}.pdf");
        }

        // ✅ RAPORT: Status pokoi
        public async Task<IActionResult> RoomStatus(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

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

        // ✅ PDF: Status pokoi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportRoomStatus(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

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

            var pdfDoc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text($"Raport: Status pokoi – {targetDate:dd.MM.yyyy}")
                        .FontSize(18).Bold().AlignCenter();

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
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Pokój").Bold();
                            header.Cell().Text("Typ pokoju").Bold();
                            header.Cell().Text("Status pokoju").Bold();
                            header.Cell().Text("Zajętość").Bold();
                            header.Cell().Text("Gość").Bold();
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
                });
            });

            using var stream = new MemoryStream();
            pdfDoc.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", $"Room_Status_{targetDate:yyyyMMdd}.pdf");
        }
    }
}
