using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShippingController : ControllerBase
{
    [HttpGet("countries")]
    public ActionResult GetCountries()
    {
        return Ok(ShippingZones.All.Select(z => new
        {
            z.Zone,
            z.Label,
            z.DeliveryEstimate,
            z.Price,
            countries = z.Countries.Select(c => new { c.Code, c.Name })
        }));
    }

    [HttpGet("rate")]
    public ActionResult GetRate([FromQuery] string country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return BadRequest(new { message = "Country is required." });
        }

        string normalised = country.Trim();

        ShippingZone? zone = ShippingZones.All.FirstOrDefault(z =>
            z.Countries.Any(c =>
                c.Code.Equals(normalised, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(normalised, StringComparison.OrdinalIgnoreCase)));

        if (zone is null)
        {
            return BadRequest(new { message = "We do not currently ship to this country." });
        }

        return Ok(new
        {
            zone = zone.Zone,
            label = zone.Label,
            deliveryEstimate = zone.DeliveryEstimate,
            price = zone.Price,
            method = zone.Method
        });
    }
}
