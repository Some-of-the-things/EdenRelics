using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

public partial class SeoController
{
    /// <summary>
    /// Whether submission is switched on, and where the ownership key file has to be reachable —
    /// the key file lives in the frontend's static assets, so it only goes live on a frontend
    /// deploy and this is the quickest way to confirm the two halves are in step.
    /// </summary>
    [HttpGet("indexnow/status")]
    public ActionResult<object> GetIndexNowStatus([FromServices] IIndexNowService indexNow)
    {
        return Ok(new
        {
            configured = indexNow.IsConfigured,
            keyLocation = indexNow.KeyLocation,
        });
    }

    /// <summary>Submits every URL the sitemap advertises. Safe to re-run.</summary>
    [HttpPost("indexnow/submit-all")]
    public async Task<ActionResult<IndexNowResult>> SubmitAllToIndexNow(
        [FromServices] IIndexNowService indexNow,
        CancellationToken ct)
    {
        return Ok(await indexNow.SubmitAllAsync(ct));
    }

    /// <summary>Submits named URLs — for re-pinging a single page after an edit.</summary>
    [HttpPost("indexnow/submit")]
    public async Task<ActionResult<IndexNowResult>> SubmitToIndexNow(
        [FromBody] IndexNowSubmitRequest request,
        [FromServices] IIndexNowService indexNow,
        CancellationToken ct)
    {
        if (request.Urls is null || request.Urls.Count == 0)
        {
            return BadRequest(new { message = "At least one URL is required." });
        }

        return Ok(await indexNow.SubmitAsync(request.Urls, ct));
    }
}

public class IndexNowSubmitRequest
{
    public List<string>? Urls { get; set; }
}
