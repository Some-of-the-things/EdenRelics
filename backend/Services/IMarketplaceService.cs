namespace Eden_Relics_BE.Services;

public interface IMarketplaceService
{
    Task<List<ProductListingDto>> GetListingsAsync(Guid productId);
    Task<ProductListingDto?> AddListingAsync(CreateListingDto dto);
    Task<ProductListingDto?> UpdateListingStatusAsync(Guid id, UpdateListingStatusDto dto);
    Task<bool> RemoveListingAsync(Guid id);
    Task<bool> MarkSoldAsync(Guid productId, MarkSoldDto dto);
    Task<List<PendingRemovalDto>> GetPendingRemovalsAsync();
    Task<bool> AcknowledgeRemovalAsync(Guid id);

    EtsyConnectDto? BuildEtsyConnect();
    Task<EtsyCallbackResult> CompleteEtsyCallbackAsync(EtsyCallbackDto dto);
    Task DisconnectEtsyAsync();
    Task<EtsyStatusDto> GetEtsyStatusAsync();
    Task<CreateEtsyListingResult> CreateEtsyListingAsync(EtsyListingRequest dto);
    Task<GeneratedListingDto?> GenerateListingTextAsync(Guid productId, string platform);
}

public enum CreateEtsyListingOutcome { Success, NotConnected, ProductNotFound, EtsyError }

public record EtsyConnectDto(string Url, string State, string CodeVerifier);
public record EtsyCallbackResult(bool Ok, string Message, string? ShopId);
public record EtsyStatusDto(bool ApiKeyConfigured, bool Connected, string? ShopId);
public record CreateEtsyListingResult(CreateEtsyListingOutcome Outcome, ProductListingDto? Listing, string? Error);

public record ProductListingDto(Guid Id, Guid ProductId, string Platform, string? ExternalListingId, string? ExternalUrl, string Status);
public record CreateListingDto(Guid ProductId, string Platform, string? ExternalListingId, string? ExternalUrl);
public record UpdateListingStatusDto(string Status);
public record MarkSoldDto(string SoldOn);
public record PendingRemovalDto(Guid ListingId, Guid ProductId, string ProductName, string Platform, string? ExternalUrl);
public record EtsyListingRequest(Guid ProductId, string? Title, string? Description);
public record EtsyCallbackDto(string Code, string CodeVerifier);
public record GeneratedListingDto(string Title, string Description, decimal Price, string ImageUrl);
