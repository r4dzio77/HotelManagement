using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationApiController : ControllerBase
    {
        private readonly ReservationPriceCalculator _priceCalculator;

        public ReservationApiController(ReservationPriceCalculator priceCalculator)
        {
            _priceCalculator = priceCalculator;
        }

        // =====================================================
        // 🔥 LIVE PREVIEW + ROZBICIE CENY (EDIT / CREATE)
        // =====================================================
        [HttpPost("CalculatePrice")]
        public async Task<IActionResult> CalculatePrice([FromBody] ReservationPriceRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Brak danych wejściowych." });

            if (request.CheckOut <= request.CheckIn)
            {
                return Ok(new
                {
                    nights = 0,
                    roomPrice = 0m,
                    servicesPrice = 0m,
                    totalPrice = 0m
                });
            }

            var breakdown = await _priceCalculator.CalculateAsync(
                request.RoomTypeId,
                request.CheckIn,
                request.CheckOut,
                request.Breakfast,
                request.Parking,
                request.ExtraBed,
                request.Pet,
                request.PersonCount,
                request.SelectedServiceIds ?? new List<int>()
            );

            return Ok(new
            {
                nights = breakdown.Nights,
                roomPrice = Math.Round(breakdown.RoomPrice, 2),
                servicesPrice = Math.Round(breakdown.ServicesPrice, 2),
                totalPrice = Math.Round(breakdown.TotalPrice, 2)
            });
        }

        // =====================================================
        // 🔹 TOTAL DO ZAPISU (DB / DETAILS)
        // =====================================================
        [HttpPost("CalculateTotal")]
        public async Task<IActionResult> CalculateTotal([FromBody] ReservationPriceRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Brak danych wejściowych." });

            if (request.CheckOut <= request.CheckIn)
                return Ok(new { totalPrice = 0m });

            var breakdown = await _priceCalculator.CalculateAsync(
                request.RoomTypeId,
                request.CheckIn,
                request.CheckOut,
                request.Breakfast,
                request.Parking,
                request.ExtraBed,
                request.Pet,
                request.PersonCount,
                request.SelectedServiceIds ?? new List<int>()
            );

            return Ok(new
            {
                totalPrice = Math.Round(breakdown.TotalPrice, 2)
            });
        }
    }
}
