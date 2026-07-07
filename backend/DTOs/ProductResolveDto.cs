namespace Eden_Relics_BE.DTOs;

/// <summary>
/// Result of resolving a product slug for the edge redirect layer. <see cref="Action"/>
/// is "render", "redirect", or "gone". For "redirect", <see cref="Name"/> and
/// <see cref="Era"/> let the caller map the piece to a designer hub or decade page.
/// </summary>
public class ProductResolveDto
{
    public required string Action { get; set; }
    public string? Name { get; set; }
    public string? Era { get; set; }
}
