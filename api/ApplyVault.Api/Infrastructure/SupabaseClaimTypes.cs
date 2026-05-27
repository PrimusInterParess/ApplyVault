using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ApplyVault.Api.Infrastructure;

internal static class SupabaseClaimTypes
{
    public static string? GetSupabaseUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        return principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
