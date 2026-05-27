using System.Security.Claims;

namespace ApplyVault.Api.IntegrationTests;

internal static class TestUserTokens
{
    public const string UserA = "test-user-a";
    public const string UserB = "test-user-b";

    public const string UserASub = "11111111-1111-1111-1111-111111111111";
    public const string UserBSub = "22222222-2222-2222-2222-222222222222";

    public static bool TryGetClaims(string token, out Claim[] claims)
    {
        claims = token switch
        {
            UserA =>
            [
                new Claim("sub", UserASub),
                new Claim("email", "user-a@test.local"),
                new Claim("name", "User A")
            ],
            UserB =>
            [
                new Claim("sub", UserBSub),
                new Claim("email", "user-b@test.local"),
                new Claim("name", "User B")
            ],
            _ => []
        };

        return claims.Length > 0;
    }
}
