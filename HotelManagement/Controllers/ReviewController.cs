using HotelManagement.Data;
using HotelManagement.Enums;
using HotelManagement.Models;
using HotelManagement.Services;
using HotelManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly ReviewService _reviewService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewController(
            HotelManagementContext context,
            ReviewService reviewService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _reviewService = reviewService;
            _userManager = userManager;
        }

        // ===================== CREATE (GET) =====================
        public async Task<IActionResult> Create(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Review)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return NotFound();

            if (!_reviewService.CanAddReview(reservation))
                return BadRequest("Nie można dodać opinii.");

            return View(new CreateReviewViewModel
            {
                ReservationId = reservationId
            });
        }

        // ===================== CREATE (POST) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReviewViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var reservation = await _context.Reservations
                .Include(r => r.Review)
                .Include(r => r.Guest)
                .FirstOrDefaultAsync(r => r.Id == model.ReservationId);

            if (reservation == null || !_reviewService.CanAddReview(reservation))
                return BadRequest();

            var review = new Review
            {
                ReservationId = reservation.Id,
                GuestId = reservation.GuestId,
                Comment = model.Comment
            };

            review.Ratings = new List<ReviewRating>
            {
                new ReviewRating { Category = RatingCategory.Cleanliness, Score = model.Cleanliness },
                new ReviewRating { Category = RatingCategory.Comfort, Score = model.Comfort },
                new ReviewRating { Category = RatingCategory.Staff, Score = model.Staff },
                new ReviewRating { Category = RatingCategory.Location, Score = model.Location },
                new ReviewRating { Category = RatingCategory.ValueForMoney, Score = model.ValueForMoney }
            };

            review.AverageRating = _reviewService.CalculateAverage(review.Ratings);

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== LIST =====================
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.Reviews
                .Include(r => r.Guest)
                .Include(r => r.Ratings)
                .OrderByDescending(r => r.AverageRating)
                .ToListAsync();

            return View(reviews);
        }

        // ===================== DETAILS =====================
        public async Task<IActionResult> Details(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.Guest)
                .Include(r => r.Ratings)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
                return NotFound();

            return View(review);
        }
    }
}
