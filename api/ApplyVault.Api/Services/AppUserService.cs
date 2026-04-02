using System.Security.Claims;
using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class AppUserService(
    ApplyVaultDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IAppUserService
{
    public async Task<AppUserEntity> GetRequiredUserAsync(CancellationToken cancellationToken = default)
    {
        return await TryGetCurrentUserAsync(cancellationToken)
            ?? throw new InvalidOperationException("The current request does not have an authenticated user.");
    }

    public async Task<AppUserEntity?> TryGetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var supabaseUserId = principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(supabaseUserId))
        {
            return null;
        }

        var email = principal.FindFirstValue("email");
        var displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("user_metadata.full_name")
            ?? email;

        var user = await dbContext.Users.SingleOrDefaultAsync(
            (candidate) => candidate.SupabaseUserId == supabaseUserId,
            cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new AppUserEntity
            {
                Id = Guid.NewGuid(),
                SupabaseUserId = supabaseUserId,
                Email = email,
                DisplayName = displayName,
                CreatedAt = utcNow,
                LastSeenAt = utcNow
            };

            await dbContext.Users.AddAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return user;
        }

        var hasChanges = false;

        if (!string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            user.Email = email;
            hasChanges = true;
        }

        if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            user.DisplayName = displayName;
            hasChanges = true;
        }

        user.LastSeenAt = utcNow;
        hasChanges = true;

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }
}
