using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApplyVault.Api.Infrastructure;

public sealed class ConfigureSupabaseJwtBearerOptions(
    IOptions<SupabaseOptions> supabaseOptions,
    SupabaseJwtSigningKeyProvider signingKeyProvider,
    IHostEnvironment environment,
    ILoggerFactory loggerFactory) :
    IConfigureNamedOptions<JwtBearerOptions>,
    IPostConfigureOptions<JwtBearerOptions>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("ApplyVault.Auth.JwtBearer");

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.Events = CreateJwtBearerEvents();

        var supabase = supabaseOptions.Value;
        if (string.IsNullOrWhiteSpace(supabase.Url))
        {
            _logger.LogWarning(
                "Supabase:Url is not configured. JWT Bearer validation is disabled and protected endpoints will return 401.");
            return;
        }

        var authority = BuildAuthority(supabase.Url);

        _logger.LogInformation(
            "JWT Bearer authentication enabled. Authority={Authority}, Audience={Audience}, SigningKeys=SupabaseJWKS",
            authority,
            ResolveAudience(supabase));
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var supabase = supabaseOptions.Value;
        if (string.IsNullOrWhiteSpace(supabase.Url))
        {
            return;
        }

        var authority = BuildAuthority(supabase.Url);
        var audience = ResolveAudience(supabase);

        // Supabase access tokens are ES256 and must be verified against JWKS directly.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = authority;
        options.TokenValidationParameters.ValidIssuers = [authority];
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = audience;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Sub;
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.IssuerSigningKeyResolver =
            (_, _, kid, _) => signingKeyProvider.Resolve(kid);
    }

    public void Configure(JwtBearerOptions options) =>
        Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    private static string BuildAuthority(string supabaseUrl) =>
        $"{supabaseUrl.TrimEnd('/')}/auth/v1";

    private static string ResolveAudience(SupabaseOptions supabase) =>
        string.IsNullOrWhiteSpace(supabase.Audience) ? "authenticated" : supabase.Audience;

    private JwtBearerEvents CreateJwtBearerEvents()
    {
        return new JwtBearerEvents
        {
            OnAuthenticationFailed = (context) =>
            {
                _logger.LogWarning(
                    context.Exception,
                    "JWT authentication failed for {Method} {Path}. Reason: {Reason}",
                    context.Request.Method,
                    context.Request.Path,
                    DescribeAuthenticationFailure(context.Exception));

                return Task.CompletedTask;
            },
            OnChallenge = (context) =>
            {
                if (context.AuthenticateFailure is not null)
                {
                    return Task.CompletedTask;
                }

                if (string.IsNullOrWhiteSpace(context.Request.Headers.Authorization))
                {
                    _logger.LogWarning(
                        "JWT challenge for {Method} {Path}: no Authorization header was sent.",
                        context.Request.Method,
                        context.Request.Path);
                }
                else
                {
                    _logger.LogWarning(
                        "JWT challenge for {Method} {Path}: {Error} {Description}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Error ?? "invalid_token",
                        context.ErrorDescription ?? "The access token could not be validated.");
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = (context) =>
            {
                var subject = SupabaseClaimTypes.GetSupabaseUserId(context.Principal) ?? "(unknown)";

                if (string.Equals(subject, "(unknown)", StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "JWT validated for {Method} {Path} but no Supabase user id claim was found. Claims={Claims}",
                        context.Request.Method,
                        context.Request.Path,
                        string.Join(", ", context.Principal?.Claims.Select((claim) => claim.Type) ?? []));
                }
                else if (environment.IsDevelopment())
                {
                    _logger.LogDebug(
                        "JWT validated for subject {Subject} on {Method} {Path}",
                        subject,
                        context.Request.Method,
                        context.Request.Path);
                }

                return Task.CompletedTask;
            }
        };
    }

    private static string DescribeAuthenticationFailure(Exception? exception)
    {
        return exception switch
        {
            SecurityTokenExpiredException expiredException =>
                $"Token expired at {expiredException.Expires:O}.",
            SecurityTokenInvalidAudienceException invalidAudienceException =>
                $"Invalid audience: {invalidAudienceException.InvalidAudience ?? "(missing)"}.",
            SecurityTokenInvalidIssuerException invalidIssuerException =>
                $"Invalid issuer. Token issuer: {invalidIssuerException.InvalidIssuer}.",
            SecurityTokenSignatureKeyNotFoundException =>
                "Signing key not found in Supabase JWKS. Verify Supabase:Url and network access to /.well-known/jwks.json.",
            SecurityTokenException securityTokenException => securityTokenException.Message,
            _ => exception?.Message ?? "Unknown authentication failure."
        };
    }
}
