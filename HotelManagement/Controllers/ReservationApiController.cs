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

        [HttpPost("CalculatePrice")]
        public async Task<IActionResult> CalculatePrice([FromBody] ReservationPriceRequest request)
        {
            try
            {
                var price = await _priceCalculator.CalculateTotalPriceAsync(
                    request.RoomTypeId,
                    request.CheckIn,
                    request.CheckOut,
                    request.Breakfast,
                    request.Parking,
                    request.Pet,
                    request.ExtraBed,
                     request.PersonCount,
                    request.SelectedServiceIds);

                return Ok(new { TotalPrice = price });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
