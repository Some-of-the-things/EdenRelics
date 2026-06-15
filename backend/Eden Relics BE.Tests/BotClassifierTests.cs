using Eden_Relics_BE.Services;

namespace Eden_Relics_BE.Tests;

public class BotClassifierTests
{
    [Theory]
    // Missing UA → treated as a bot.
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData("   ", null, true)]
    // Self-identifying crawlers / tooling.
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)", null, true)]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)", null, true)]
    [InlineData("facebookexternalhit/1.1", null, true)]
    [InlineData("python-requests/2.31.0", null, true)]
    [InlineData("curl/8.4.0", null, true)]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) HeadlessChrome/120.0", null, true)]
    // Genuine browsers from consumer networks → human.
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", null, false)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "Vodafone Ltd", false)]
    // Browser UA but datacenter network → spoofed bot.
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", "Amazon.com, Inc.", true)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120 Safari/537.36", "Hetzner Online GmbH", true)]
    public void IsBot_ClassifiesAsExpected(string? userAgent, string? asOrganization, bool expected)
    {
        Assert.Equal(expected, BotClassifier.IsBot(userAgent, asOrganization));
    }
}
