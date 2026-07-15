namespace Eden_Relics_BE.Auth;

/// <summary>
/// Canonical role names carried in the JWT `role` claim (see JwtTokenService) and used by
/// <c>[Authorize(Roles = ...)]</c>. Roles are a single free-text string on <c>User.Role</c>;
/// these constants avoid magic strings. "Seller" is introduced for the multi-seller hub — a
/// seller account can still buy (buying requires no role), so no multi-role model is needed.
/// </summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string Seller = "Seller";
}
