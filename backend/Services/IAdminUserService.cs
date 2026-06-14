namespace Eden_Relics_BE.Services;

public interface IAdminUserService
{
    Task<List<AdminUserDto>> GetAllAsync();
}

public record AdminUserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    bool EmailVerified,
    bool MfaEnabled,
    string? ExternalProvider,
    DateTime CreatedAtUtc,
    int OrderCount,
    bool MailingList,
    List<string> Favourites);
