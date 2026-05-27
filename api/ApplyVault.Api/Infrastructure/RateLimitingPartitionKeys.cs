namespace ApplyVault.Api.Infrastructure;

internal static class RateLimitingPartitionKeys
{
    private static readonly PathString[] ExemptPaths =
    [
        "/health",
        "/health/live",
        "/api/health"
    ];

    public static bool IsExemptPath(PathString path)
    {
        foreach (var exempt in ExemptPaths)
        {
            if (path.StartsWithSegments(exempt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static string GetUserOrClientIp(HttpContext context)
    {
        var userId = SupabaseClaimTypes.GetSupabaseUserId(context.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{GetClientIp(context)}";
    }
}
