using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

public partial class SeoController
{
    [HttpGet("traffic/status")]
    public async Task<ActionResult<TrafficStatusDto>> GetTrafficStatus()
    {
        return Ok(await seo.GetTrafficStatusAsync());
    }

    [HttpGet("traffic/overview")]
    public async Task<ActionResult<TrafficOverviewDto>> GetTrafficOverview([FromQuery] int days = 30)
    {
        return Ok(await seo.GetTrafficOverviewAsync(days));
    }

    [HttpGet("traffic/queries")]
    public async Task<ActionResult<List<QueryRollupDto>>> GetTopQueries(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100,
        [FromQuery] string sort = "clicks")
    {
        return Ok(await seo.GetTopQueriesAsync(days, limit, sort));
    }

    [HttpGet("traffic/pages")]
    public async Task<ActionResult<List<PageRollupDto>>> GetTopPages(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100,
        [FromQuery] string sort = "clicks")
    {
        return Ok(await seo.GetTopPagesAsync(days, limit, sort));
    }

    [HttpGet("traffic/sources")]
    public async Task<ActionResult<List<SourceRollupDto>>> GetTopSources(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100)
    {
        return Ok(await seo.GetTopSourcesAsync(days, limit));
    }

    [HttpGet("traffic/landing-pages")]
    public async Task<ActionResult<List<LandingPageRollupDto>>> GetTopLandingPages(
        [FromQuery] int days = 28,
        [FromQuery] int limit = 100)
    {
        return Ok(await seo.GetTopLandingPagesAsync(days, limit));
    }

    /// <summary>
    /// Queries with meaningful impressions but stuck on pages 2-3 — these are
    /// often the cheapest wins. Improving title/meta/internal-link/content for
    /// these can push them onto page 1.
    /// </summary>
    [HttpGet("traffic/opportunities")]
    public async Task<ActionResult<List<QueryRollupDto>>> GetOpportunities(
        [FromQuery] int days = 28,
        [FromQuery] int minImpressions = 10,
        [FromQuery] double minPosition = 5,
        [FromQuery] double maxPosition = 30)
    {
        return Ok(await seo.GetOpportunitiesAsync(days, minImpressions, minPosition, maxPosition));
    }

    [HttpPost("traffic/run")]
    public async Task<ActionResult<TrafficRunResultDto>> RunTrafficIngest([FromQuery] int? days = null)
    {
        return Ok(await seo.RunTrafficIngestAsync(days));
    }

    /// <summary>
    /// Our own first-party, cookieless page-view counter (bot-filtered). Sees 100% of SSR
    /// renders, unlike GA4's consented cohort — a GDPR-clean source of truth for real humans.
    /// </summary>
    [HttpGet("traffic/page-views")]
    public async Task<ActionResult<PageViewStatsDto>> GetPageViews(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 50)
    {
        return Ok(await seo.GetPageViewStatsAsync(days, limit));
    }
}
