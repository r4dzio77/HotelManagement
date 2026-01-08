using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize(Roles = "Pracownik,Kierownik")]
    public class SearchController : Controller
    {
        private readonly HotelManagementContext _context;

        public SearchController(HotelManagementContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string searchType = "reservation",

            // REZERWACJE
            string? reservationNumber = null,
            string? firstName = null,
            string? lastName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,

            // GOŚCIE
            string? guestQuery = null,

            // DOKUMENTY
            string? documentNumber = null,
            DateTime? documentFromDate = null,
            DateTime? documentToDate = null
        )
        {
            searchType = searchType.ToLower();

            // =========================
            // REZERWACJE
            // =========================
            IEnumerable<Reservation> reservations = Enumerable.Empty<Reservation>();

            if (searchType == "reservation")
            {
                var q = _context.Reservations
                    .Include(r => r.Guest)
                    .Include(r => r.Room)
                    .Include(r => r.RoomType)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(reservationNumber) &&
                    int.TryParse(reservationNumber, out int id))
                    q = q.Where(r => r.Id == id);

                if (!string.IsNullOrWhiteSpace(firstName))
                    q = q.Where(r => r.Guest.FirstName.Contains(firstName));

                if (!string.IsNullOrWhiteSpace(lastName))
                    q = q.Where(r => r.Guest.LastName.Contains(lastName));

                if (fromDate.HasValue)
                    q = q.Where(r => r.CheckIn.Date >= fromDate.Value.Date);

                if (toDate.HasValue)
                    q = q.Where(r => r.CheckOut.Date <= toDate.Value.Date);

                reservations = await q
                    .OrderByDescending(r => r.CheckIn)
                    .ToListAsync();
            }

            // =========================
            // GOŚCIE
            // =========================
            IEnumerable<Guest> guestResults = Enumerable.Empty<Guest>();

            if (searchType == "guest" && !string.IsNullOrWhiteSpace(guestQuery))
            {
                guestResults = await _context.Guests
                    .Where(g =>
                        g.FirstName.Contains(guestQuery) ||
                        g.LastName.Contains(guestQuery) ||
                        (g.Email != null && g.Email.Contains(guestQuery)) ||
                        (g.PhoneNumber != null && g.PhoneNumber.Contains(guestQuery)))
                    .OrderBy(g => g.LastName)
                    .ThenBy(g => g.FirstName)
                    .ToListAsync();
            }

            // =========================
            // DOKUMENTY
            // =========================
            IEnumerable<Document> documentResults = Enumerable.Empty<Document>();

            if (searchType == "document")
            {
                var q = _context.Documents
                    .Include(d => d.Reservation)
                        .ThenInclude(r => r.Guest)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(documentNumber))
                    q = q.Where(d => d.Number.Contains(documentNumber));

                if (documentFromDate.HasValue)
                    q = q.Where(d => d.IssueDate.Date >= documentFromDate.Value.Date);

                if (documentToDate.HasValue)
                    q = q.Where(d => d.IssueDate.Date <= documentToDate.Value.Date);

                documentResults = await q
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();
            }

            // =========================
            // VIEWBAG (DLA WIDOKU)
            // =========================
            ViewBag.SearchType = searchType;

            ViewBag.ReservationNumber = reservationNumber;
            ViewBag.FirstName = firstName;
            ViewBag.LastName = lastName;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            ViewBag.GuestQuery = guestQuery;
            ViewBag.GuestResults = guestResults;

            ViewBag.DocumentNumber = documentNumber;
            ViewBag.DocumentFromDate = documentFromDate?.ToString("yyyy-MM-dd");
            ViewBag.DocumentToDate = documentToDate?.ToString("yyyy-MM-dd");
            ViewBag.DocumentResults = documentResults;

            return View(reservations);
        }
    }
}
