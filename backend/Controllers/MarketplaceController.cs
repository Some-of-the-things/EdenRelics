using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MarketplaceController(IMarketplaceService marketplace) : ControllerBase
{
    // --- Listing management ---

    [HttpGet("listings/{productId:guid}")]
    public async Task<ActionResult<List<ProductListingDto>>> GetListings(Guid productId)
    {
        return Ok(await marketplace.GetListingsAsync(productId));
    }

    [HttpPost("listings")]
    public async Task<ActionResult<ProductListingDto>> AddListing([FromBody] CreateListingDto dto)
    {
        ProductListingDto? listing = await marketplace.AddListingAsync(dto);
        return listing is null ? NotFound(new { message = "Product not found." }) : Ok(listing);
    }

    [HttpPut("listings/{id:guid}/status")]
    public async Task<ActionResult<ProductListingDto>> UpdateListingStatus(Guid id, [FromBody] UpdateListingStatusDto dto)
    {
        ProductListingDto? listing = await marketplace.UpdateListingStatusAsync(id, dto);
        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpDelete("listings/{id:guid}")]
    public async Task<IActionResult> RemoveListing(Guid id)
    {
        return await marketplace.RemoveListingAsync(id) ? NoContent() : NotFound();
    }

    [HttpPost("mark-sold/{productId:guid}")]
    public async Task<ActionResult> MarkSold(Guid productId, [FromBody] MarkSoldDto dto)
    {
        bool found = await marketplace.MarkSoldAsync(productId, dto);
        return found
            ? Ok(new { message = $"Product marked as sold on {dto.SoldOn}. Other listings flagged for removal." })
            : NotFound();
    }

    [HttpGet("pending-removals")]
    public async Task<ActionResult<List<PendingRemovalDto>>> GetPendingRemovals()
    {
        return Ok(await marketplace.GetPendingRemovalsAsync());
    }

    [HttpPost("acknowledge-removal/{id:guid}")]
    public async Task<IActionResult> AcknowledgeRemoval(Guid id)
    {
        return await marketplace.AcknowledgeRemovalAsync(id) ? Ok() : NotFound();
    }

    // --- Etsy OAuth ---

    [HttpGet("etsy/connect")]
    public ActionResult EtsyConnect()
    {
        EtsyConnectDto? connect = marketplace.BuildEtsyConnect();
        return connect is null
            ? BadRequest(new { message = "Etsy API key not configured. Set Etsy:ApiKey." })
            : Ok(connect);
    }

    [HttpPost("etsy/callback")]
    public async Task<ActionResult> EtsyCallback([FromBody] EtsyCallbackDto dto)
    {
        EtsyCallbackResult result = await marketplace.CompleteEtsyCallbackAsync(dto);
        return result.Ok
            ? Ok(new { message = result.Message, shopId = result.ShopId })
            : BadRequest(new { message = result.Message });
    }

    [HttpPost("etsy/disconnect")]
    public async Task<ActionResult> EtsyDisconnect()
    {
        await marketplace.DisconnectEtsyAsync();
        return Ok(new { message = "Etsy disconnected." });
    }

    [HttpGet("etsy/status")]
    public async Task<ActionResult<object>> EtsyStatus()
    {
        EtsyStatusDto status = await marketplace.GetEtsyStatusAsync();
        return Ok(new { apiKeyConfigured = status.ApiKeyConfigured, connected = status.Connected, shopId = status.ShopId });
    }

    // --- Etsy listing operations ---

    [HttpPost("etsy/create-listing")]
    public async Task<ActionResult<ProductListingDto>> CreateEtsyListing([FromBody] EtsyListingRequest dto)
    {
        CreateEtsyListingResult result = await marketplace.CreateEtsyListingAsync(dto);
        return result.Outcome switch
        {
            CreateEtsyListingOutcome.ProductNotFound => NotFound(new { message = result.Error }),
            CreateEtsyListingOutcome.Success => Ok(result.Listing),
            _ => BadRequest(new { message = result.Error }),
        };
    }

    // --- Listing text generator ---

    [HttpGet("generate-listing/{productId:guid}")]
    public async Task<ActionResult<GeneratedListingDto>> GenerateListingText(Guid productId, [FromQuery] string platform)
    {
        GeneratedListingDto? generated = await marketplace.GenerateListingTextAsync(productId, platform);
        return generated is null ? NotFound() : Ok(generated);
    }
}
