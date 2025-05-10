using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    public class ReservationController : Controller
    {
        private readonly HotelManagementContext _context;

        public ReservationController(HotelManagementContext context)
        {
            _context = context;
        }

        // GET: Reservation/Index
        [HttpGet]
        [Authorize(Roles = "Kierownik,Admin,Pracownik")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Now;

            // Pobierz wszystkie rezerwacje z RoomType
            var reservations = await _context.Reservations
                .Include(r => r.Room) // Możesz załączyć inne powiązania, np. pokój
                .ToListAsync();

            // Podziel rezerwacje na trzy grupy: przyjazdy, pobyty, wyjazdy
            var arrivals = reservations.Where(r => r.CheckIn > today).ToList(); // Przyjazdy
            var inStay = reservations.Where(r => r.CheckIn <= today && r.CheckOut >= today).ToList(); // Pobyty
            var departures = reservations.Where(r => r.CheckOut < today).ToList(); // Wyjazdy

            // Przekazujemy dane do widoku
            ViewBag.Arrivals = arrivals;
            ViewBag.InStay = inStay;
            ViewBag.Departures = departures;

            return View(); // Przekazujemy widok główny
        }
    }
}
