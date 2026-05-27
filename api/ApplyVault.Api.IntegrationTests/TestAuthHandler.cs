using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.IntegrationTests;

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorization) ||
            authorization.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Expected Bearer authorization."));
        }

        var token = header["Bearer ".Length..].Trim();

        if (!TestUserTokens.TryGetClaims(token, out var claims))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown test token."));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
