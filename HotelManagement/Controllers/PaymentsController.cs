using System;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

// alias, żeby nie myliło się z Stripe.PaymentMethod
using PaymentMethodEnum = HotelManagement.Enums.PaymentMethod;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly IConfiguration _configuration;

        public PaymentsController(HotelManagementContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // alias: /Payments/Pay -> Start
        [HttpGet]
        public async Task<IActionResult> Pay(int reservationId)
        {
            return await Start(reservationId);
        }

        // ======= WYBÓR METODY PŁATNOŚCI =======
        [HttpGet]
        public async Task<IActionResult> Start(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int reservationId, PaymentMethodEnum method)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return NotFound();

            if (method == PaymentMethodEnum.OnlineStripe)
            {
                return await CheckoutSession(reservation);
            }

            // płatność na miejscu – traktujemy jako opłacone
            reservation.IsPaidOnline = false;
            reservation.PaymentStatus = "pay_on_arrival";

            _context.Reservations.Update(reservation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Success), new { reservationId = reservation.Id });
        }

        // ====== KOMPATYBILNOŚĆ ======
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return NotFound();

            return await CheckoutSession(reservation);
        }

        // ====== TWORZENIE SESJI STRIPE ======
        private async Task<IActionResult> CheckoutSession(Reservation reservation)
        {
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            var domain = $"{Request.Scheme}://{Request.Host}";

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl =
                    $"{domain}/Payments/Success?reservationId={reservation.Id}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl =
                    $"{domain}/Payments/Cancel?reservationId={reservation.Id}",
                CustomerEmail = reservation.Guest?.Email,
                PaymentMethodTypes = new System.Collections.Generic.List<string>
                {
                    "card", "blik", "p24"
                },
                LineItems = new System.Collections.Generic.List<SessionLineItemOptions>()
            };

            options.LineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "pln",
                    UnitAmount = (long)(reservation.TotalPrice * 100m),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Rezerwacja: {reservation.RoomType?.Name}",
                        Description = $"Pobyt {reservation.CheckIn:dd.MM.yyyy} – {reservation.CheckOut:dd.MM.yyyy}"
                    }
                }
            });

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // 🔥 Zapisanie ID sesji w rezerwacji
            reservation.CheckoutSessionId = session.Id;
            _context.Reservations.Update(reservation);
            await _context.SaveChangesAsync();

            return Redirect(session.Url);
        }

        // ====== SUKCES PŁATNOŚCI ======
        [HttpGet]
        public async Task<IActionResult> Success(int reservationId, string? session_id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return View("SuccessNoReservation");

            // pobranie danych Stripe – jeśli wróciło session_id
            if (!string.IsNullOrEmpty(session_id))
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                var intentService = new PaymentIntentService();
                var paymentIntent = await intentService.GetAsync(session.PaymentIntentId);

                // 🔥 Zapis do rezerwacji
                reservation.IsPaidOnline = true;
                reservation.PaymentMethod = paymentIntent.PaymentMethodTypes?.FirstOrDefault();
                reservation.PaymentIntentId = paymentIntent.Id;
                reservation.PaymentStatus = paymentIntent.Status;
                reservation.CheckoutSessionId = session.Id;

                _context.Reservations.Update(reservation);
                await _context.SaveChangesAsync();
            }

            // stary system zapisujący PaymentRecord
            bool alreadyPaid = await _context.Payments
                .AnyAsync(p => p.ReservationId == reservation.Id &&
                               p.Method == PaymentMethodEnum.OnlineStripe);

            if (!alreadyPaid)
            {
                var payment = new Payment
                {
                    ReservationId = reservation.Id,
                    GuestId = reservation.GuestId,
                    Method = PaymentMethodEnum.OnlineStripe,
                    Amount = reservation.TotalPrice,
                    PaidAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
            }

            return View(reservation);
        }

        // ====== ANULOWANIE ======
        [HttpGet]
        public async Task<IActionResult> Cancel(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }
    }
}
