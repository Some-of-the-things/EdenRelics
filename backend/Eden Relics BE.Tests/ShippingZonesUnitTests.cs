using Eden_Relics_BE.Controllers;

namespace Eden_Relics_BE.Tests;

public class ShippingZonesUnitTests
{
    [Fact]
    public void GetShippingCost_Standard_Returns395()
    {
        Assert.Equal(3.95m, ShippingZones.GetShippingCost("standard", null));
    }

    [Fact]
    public void GetShippingCost_Express_Returns695()
    {
        Assert.Equal(6.95m, ShippingZones.GetShippingCost("express", null));
    }

    [Fact]
    public void GetShippingCost_France_ReturnsEuropeRate()
    {
        Assert.Equal(9.95m, ShippingZones.GetShippingCost("international", "France"));
    }

    [Fact]
    public void GetShippingCost_ByCode_FR_ReturnsEuropeRate()
    {
        Assert.Equal(9.95m, ShippingZones.GetShippingCost("international", "FR"));
    }

    [Fact]
    public void GetShippingCost_US_ReturnsNorthAmericaRate()
    {
        Assert.Equal(14.95m, ShippingZones.GetShippingCost("international", "US"));
    }

    [Fact]
    public void GetShippingCost_Australia_ReturnsAustralasiaRate()
    {
        Assert.Equal(16.95m, ShippingZones.GetShippingCost("international", "AU"));
    }

    [Fact]
    public void GetShippingCost_Japan_ReturnsAsiaRate()
    {
        Assert.Equal(14.95m, ShippingZones.GetShippingCost("international", "JP"));
    }

    [Fact]
    public void GetShippingCost_UAE_ReturnsMiddleEastRate()
    {
        Assert.Equal(14.95m, ShippingZones.GetShippingCost("international", "AE"));
    }

    [Fact]
    public void GetShippingCost_SouthAfrica_ReturnsRestOfWorldRate()
    {
        Assert.Equal(18.95m, ShippingZones.GetShippingCost("international", "ZA"));
    }

    [Fact]
    public void GetShippingCost_UnknownCountry_ReturnsFallback()
    {
        Assert.Equal(12.95m, ShippingZones.GetShippingCost("international", "XX"));
    }

    [Fact]
    public void GetShippingCost_NullCountry_ReturnsFallback()
    {
        Assert.Equal(12.95m, ShippingZones.GetShippingCost("international", null));
    }

    [Fact]
    public void GetShippingCost_CaseInsensitive()
    {
        Assert.Equal(9.95m, ShippingZones.GetShippingCost("international", "fr"));
        Assert.Equal(14.95m, ShippingZones.GetShippingCost("international", "united states"));
    }

    [Fact]
    public void AllZones_HaveAtLeastOneCountry()
    {
        foreach (var zone in ShippingZones.All)
        {
            Assert.NotEmpty(zone.Countries);
        }
    }

    [Fact]
    public void AllZones_HavePositivePrice()
    {
        foreach (var zone in ShippingZones.All)
        {
            Assert.True(zone.Price > 0, $"Zone {zone.Zone} has non-positive price");
        }
    }

    [Fact]
    public void AllZones_HaveDeliveryEstimate()
    {
        foreach (var zone in ShippingZones.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(zone.DeliveryEstimate), $"Zone {zone.Zone} has no delivery estimate");
        }
    }
}
