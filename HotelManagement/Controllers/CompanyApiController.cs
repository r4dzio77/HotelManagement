using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyApiController : ControllerBase
    {
        private readonly HotelManagementContext _context;

        public CompanyApiController(HotelManagementContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Wyszukiwanie firm w Twojej bazie po nazwie lub NIP.
        /// GET /api/CompanyApi/Search?term=...
        /// </summary>
        [HttpGet("Search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Ok(new { results = new object[0] });
            }

            term = term.Trim();
            var normalizedDigits = new string(term.Where(char.IsDigit).ToArray());

            var query = _context.Companies
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(normalizedDigits) && normalizedDigits.Length >= 4)
            {
                // Szukamy po NIP (cyfry)
                query = query.Where(c =>
                    c.VatNumber != null &&
                    new string(c.VatNumber.Where(char.IsDigit).ToArray()).Contains(normalizedDigits));
            }
            else
            {
                // Szukamy po nazwie firmy (case-insensitive)
                var lower = term.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(lower));
            }

            var companies = await query
                .OrderBy(c => c.Name)
                .Take(20)
                .ToListAsync();

            var results = companies.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                vatNumber = c.VatNumber,
                address = c.Address,
                postalCode = c.PostalCode,
                city = c.City,
                country = c.Country
            });

            return Ok(new { results });
        }
    }
}
