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
    public async Task<ActionResult<IEnumerable<Guid>>> GetMyFavourites()
    {
        Guid? userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IEnumerable<Favourite> favourites = await _repository.FindAsync(f => f.UserId == userId.Value);
        return Ok(favourites.Select(f => f.ProductId));
    }

    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> AddFavourite(Guid productId)
    {
        Guid? userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IEnumerable<Favourite> existing = await _repository.FindAsync(
            f => f.UserId == userId.Value && f.ProductId == productId);
        if (existing.Any())
        {
            return Ok();
        }

        await _repository.AddAsync(new Favourite { UserId = userId.Value, ProductId = productId });
        return Created();
    }

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
