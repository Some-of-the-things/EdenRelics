namespace Eden_Relics_BE.Services;

public record ShippingCountry(string Code, string Name);

public record ShippingZone(string Zone, string Label, string Method, string DeliveryEstimate, decimal Price, List<ShippingCountry> Countries);

/// <summary>
/// Shipping zones and rate calculation. A domain concern (used by checkout/order pricing
/// as well as the shipping API), not a controller-level one.
/// </summary>
public static class ShippingZones
{
    public static readonly List<ShippingZone> All =
    [
        new("uk-standard", "Standard UK Delivery", "standard", "3–5 working days", 3.95m,
        [
            new("GB", "United Kingdom"),
        ]),

        new("uk-express", "Express UK Delivery", "express", "1–2 working days", 6.95m,
        [
            new("GB", "United Kingdom"),
        ]),

        new("europe", "Europe", "europe", "5–10 working days", 9.95m,
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

        new("north-america", "North America", "international", "7–14 working days", 14.95m,
        [
            new("US", "United States"),
            new("CA", "Canada"),
        ]),

        new("australasia", "Australia & New Zealand", "international", "10–18 working days", 16.95m,
        [
            new("AU", "Australia"),
            new("NZ", "New Zealand"),
        ]),

        new("asia", "Asia", "international", "7–14 working days", 14.95m,
        [
            new("JP", "Japan"),
            new("KR", "South Korea"),
            new("SG", "Singapore"),
            new("HK", "Hong Kong"),
            new("TW", "Taiwan"),
            new("MY", "Malaysia"),
            new("TH", "Thailand"),
        ]),

        new("middle-east", "Middle East", "international", "7–14 working days", 14.95m,
        [
            new("AE", "United Arab Emirates"),
            new("SA", "Saudi Arabia"),
            new("QA", "Qatar"),
            new("IL", "Israel"),
        ]),

        new("rest-of-world", "Rest of World", "international", "10–21 working days", 18.95m,
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
