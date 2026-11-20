using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Category hubs (/designers, /style, /dresses) and their sub-categories, plus which pieces sit in
/// them. Edited on the admin Categories tab and the product edit screen.
/// </summary>
[ApiController]
[Route("api/categories")]
[Authorize(Roles = "Admin")]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    /// <summary>Public: visible sub-categories, redirects for old URLs, and hidden slugs.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<CategoriesDto>> Get()
    {
        return Ok(await categories.GetPublicAsync());
    }

    /// <summary>Admin: every sub-category, hidden ones included.</summary>
    [HttpGet("admin")]
    public async Task<ActionResult<CategoriesDto>> GetForAdmin()
    {
        return Ok(await categories.GetAdminAsync());
    }

    [HttpPost("admin/sub-categories")]
    public async Task<ActionResult<SubCategoryDto>> Create([FromBody] SaveSubCategoryRequest request)
    {
        try
        {
            return Ok(await categories.CreateAsync(request));
        }
        catch (CategoryValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("admin/sub-categories/{id:guid}")]
    public async Task<ActionResult<SubCategoryDto>> Update(Guid id, [FromBody] SaveSubCategoryRequest request)
    {
        try
        {
            SubCategoryDto? updated = await categories.UpdateAsync(id, request);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (CategoryValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Deletes a sub-category; its URL then 301s to the hub page.</summary>
    [HttpDelete("admin/sub-categories/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await categories.DeleteAsync(id) ? NoContent() : NotFound();
    }

    [HttpPut("admin/hubs/{hubKey}/order")]
    public async Task<IActionResult> Reorder(string hubKey, [FromBody] ReorderSubCategoriesRequest request)
    {
        await categories.ReorderAsync(hubKey, request.Ids ?? []);
        return NoContent();
    }

    /// <summary>Bulk add/remove pieces from one sub-category.</summary>
    [HttpPut("admin/sub-categories/{id:guid}/products")]
    public async Task<ActionResult<SubCategoryDto>> SetProducts(Guid id, [FromBody] SetSubCategoryProductsRequest request)
    {
        SubCategoryDto? updated = await categories.SetProductsAsync(id, request.Add ?? [], request.Remove ?? []);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Sets every sub-category a piece belongs to (the product edit screen's ticks).</summary>
    [HttpPut("admin/products/{productId:guid}")]
    public async Task<IActionResult> SetProductCategories(Guid productId, [FromBody] SetProductCategoriesRequest request)
    {
        return await categories.SetProductCategoriesAsync(productId, request.SubCategoryIds ?? [])
            ? NoContent()
            : NotFound();
    }
}
