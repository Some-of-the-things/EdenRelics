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

public record ShippingCountry(string Code, string Name);

public record ShippingZone(string Zone, string Label, string Method, string DeliveryEstimate, decimal Price, List<ShippingCountry> Countries);

public static class ShippingZones
{
    public static readonly List<ShippingZone> All =
    [
        new("uk-standard", "Standard UK Delivery", "standard", "3\u20135 working days", 3.95m,
        [
            new("GB", "United Kingdom"),
        ]),

        new("uk-express", "Express UK Delivery", "express", "1\u20132 working days", 6.95m,
        [
            new("GB", "United Kingdom"),
        ]),

        new("europe", "Europe", "europe", "5\u201310 working days", 9.95m,
        [
            new("IE", "Ireland"),
            new("FR", "France"),
            new("DE", "Germany"),
            new("NL", "Netherlands"),
            new("BE", "Belgium"),
            new("ES", "Spain"),
            new("PT", "Portugal"),
            new("IT", "Italy"),
            new("AT", "Austria"),
            new("CH", "Switzerland"),
            new("SE", "Sweden"),
            new("DK", "Denmark"),
            new("NO", "Norway"),
            new("FI", "Finland"),
            new("PL", "Poland"),
            new("CZ", "Czech Republic"),
            new("GR", "Greece"),
            new("HR", "Croatia"),
            new("HU", "Hungary"),
            new("RO", "Romania"),
            new("LU", "Luxembourg"),
        ]),

        new("north-america", "North America", "international", "7\u201314 working days", 14.95m,
        [
            new("US", "United States"),
            new("CA", "Canada"),
        ]),

        new("australasia", "Australia & New Zealand", "international", "10\u201318 working days", 16.95m,
        [
            new("AU", "Australia"),
            new("NZ", "New Zealand"),
        ]),

        new("asia", "Asia", "international", "7\u201314 working days", 14.95m,
        [
            new("JP", "Japan"),
            new("KR", "South Korea"),
            new("SG", "Singapore"),
            new("HK", "Hong Kong"),
            new("TW", "Taiwan"),
            new("MY", "Malaysia"),
            new("TH", "Thailand"),
        ]),

        new("middle-east", "Middle East", "international", "7\u201314 working days", 14.95m,
        [
            new("AE", "United Arab Emirates"),
            new("SA", "Saudi Arabia"),
            new("QA", "Qatar"),
            new("IL", "Israel"),
        ]),

        new("rest-of-world", "Rest of World", "international", "10\u201321 working days", 18.95m,
        [
            new("ZA", "South Africa"),
            new("MX", "Mexico"),
        ]),
    ];

    public static decimal GetShippingCost(string? shippingMethod, string? country)
    {
        if (shippingMethod is "standard") { return 3.95m; }
        if (shippingMethod is "express") { return 6.95m; }

        if (!string.IsNullOrWhiteSpace(country))
        {
            string normalised = country.Trim();
            ShippingZone? zone = All.FirstOrDefault(z =>
                z.Zone is not "uk-standard" and not "uk-express" &&
                z.Countries.Any(c =>
                    c.Code.Equals(normalised, StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Equals(normalised, StringComparison.OrdinalIgnoreCase)));

            if (zone is not null)
            {
                return zone.Price;
            }
        }

        return 12.95m; // fallback
    }
}
