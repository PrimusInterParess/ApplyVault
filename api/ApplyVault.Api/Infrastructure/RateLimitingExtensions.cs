using System.Threading.RateLimiting;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyVault.Api.Infrastructure;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApplyVaultRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .Validate(
                (options) => !options.Enabled || options.GlobalApi.PermitLimit > 0,
                "RateLimiting:GlobalApi:PermitLimit must be greater than 0 when rate limiting is enabled.")
            .Validate(
                (options) => !options.Enabled || options.GlobalApi.WindowSeconds > 0,
                "RateLimiting:GlobalApi:WindowSeconds must be greater than 0 when rate limiting is enabled.")
            .ValidateOnStart();

        services.AddRateLimiter((rateLimiterOptions) =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                var logger = httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ApplyVault.RateLimiting");

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                logger.LogWarning(
                    "Rate limit exceeded for {Method} {Path} Partition={PartitionKey} TraceId={TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path.Value ?? "/",
                    context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                        ?? "global",
                    httpContext.TraceIdentifier);

                await Task.CompletedTask;
            };

            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>((httpContext) =>
            {
                var options = httpContext.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
                    .Value;

                if (!options.Enabled ||
                    RateLimitingPartitionKeys.IsExemptPath(httpContext.Request.Path) ||
                    !httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                var ip = RateLimitingPartitionKeys.GetClientIp(httpContext);
                var policy = options.GlobalApi;

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"global:{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = 0
                    });
            });

            rateLimiterOptions.AddPolicy(RateLimitingOptions.PolicyScrapeIngest, CreateFixedWindowPolicy);
            rateLimiterOptions.AddPolicy(RateLimitingOptions.PolicyEuresSearch, CreateEuresSlidingWindowPolicy);
            rateLimiterOptions.AddPolicy(RateLimitingOptions.PolicyJobnetSearch, CreateJobnetSlidingWindowPolicy);
            rateLimiterOptions.AddPolicy(RateLimitingOptions.PolicyOAuthCallback, CreateOAuthCallbackPolicy);
        });

        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindowPolicy(HttpContext httpContext)
    {
        var options = httpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var partitionKey = $"{RateLimitingOptions.PolicyScrapeIngest}:{RateLimitingPartitionKeys.GetUserOrClientIp(httpContext)}";
        var policy = options.ScrapeIngest;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = 0
            });
    }

    private static RateLimitPartition<string> CreateEuresSlidingWindowPolicy(HttpContext httpContext)
    {
        var options = httpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var partitionKey = $"{RateLimitingOptions.PolicyEuresSearch}:{RateLimitingPartitionKeys.GetUserOrClientIp(httpContext)}";
        var policy = options.EuresSearch;

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });
    }

    private static RateLimitPartition<string> CreateJobnetSlidingWindowPolicy(HttpContext httpContext)
    {
        var options = httpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var partitionKey = $"{RateLimitingOptions.PolicyJobnetSearch}:{RateLimitingPartitionKeys.GetUserOrClientIp(httpContext)}";
        var policy = options.JobnetSearch;

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });
    }

    private static RateLimitPartition<string> CreateOAuthCallbackPolicy(HttpContext httpContext)
    {
        var options = httpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>()
            .Value;

        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var ip = RateLimitingPartitionKeys.GetClientIp(httpContext);
        var partitionKey = $"{RateLimitingOptions.PolicyOAuthCallback}:{ip}";
        var policy = options.OAuthCallback;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = 0
            });
    }
}
