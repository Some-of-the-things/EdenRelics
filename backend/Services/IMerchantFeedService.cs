namespace Eden_Relics_BE.Services;

public interface IMerchantFeedService
{
    /// <summary>Builds the Google Merchant Center product feed (RSS 2.0 + g: namespace)
    /// from live products, for free Shopping-tab listings.</summary>
    Task<string> BuildFeedXmlAsync();
}
