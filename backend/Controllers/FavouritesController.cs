using System.Security.Claims;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavouritesController : ControllerBase
{
    private readonly IRepository<Favourite> _repository;

    public FavouritesController(IRepository<Favourite> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult> GetMyFavourites()
    {
        Guid? userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IEnumerable<Favourite> favourites = await _repository.FindAsync(f => f.UserId == userId.Value);
        return Ok(favourites.Select(f => new { f.ProductId, f.NotifyOnSale }));
    }

    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> AddFavourite(Guid productId, [FromBody] AddFavouriteDto? dto = null)
    {
        Guid? userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Include soft-deleted rows: DeleteAsync soft-deletes, but the
        // (UserId, ProductId) unique index ignores IsDeleted — so a previously
        // removed favourite still occupies the slot. Resurrect it rather than
        // inserting a duplicate (which would hit the unique constraint).
        IEnumerable<Favourite> existing = await _repository.FindAsync(
            f => f.UserId == userId.Value && f.ProductId == productId, includeDeleted: true);
        Favourite? existingFav = existing.FirstOrDefault();
        if (existingFav is not null)
        {
            bool wasDeleted = existingFav.IsDeleted;
            bool notifyChanged = dto is not null && existingFav.NotifyOnSale != dto.NotifyOnSale;
            if (wasDeleted || notifyChanged)
            {
                existingFav.IsDeleted = false;
                if (dto is not null)
                {
                    existingFav.NotifyOnSale = dto.NotifyOnSale;
                }
                await _repository.UpdateAsync(existingFav);
            }
            return Ok();
        }

        await _repository.AddAsync(new Favourite
        {
            UserId = userId.Value,
            ProductId = productId,
            NotifyOnSale = dto?.NotifyOnSale ?? false,
        });
        return Created();
    }

    public record AddFavouriteDto(bool NotifyOnSale = false);

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> RemoveFavourite(Guid productId)
    {
        Guid? userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IEnumerable<Favourite> existing = await _repository.FindAsync(
            f => f.UserId == userId.Value && f.ProductId == productId);
        Favourite? favourite = existing.FirstOrDefault();
        if (favourite is null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(favourite.Id);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return null;
        }
        return userId;
    }
}
