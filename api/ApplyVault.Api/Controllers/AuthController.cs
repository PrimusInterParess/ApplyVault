using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(IAppUserService appUserService) : ControllerBase
{
    [HttpGet("session")]
    public async Task<ActionResult<CurrentUserDto>> GetSession(CancellationToken cancellationToken)
    {
        var user = await appUserService.TryGetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserDto(user.Id, user.SupabaseUserId, user.Email, user.DisplayName));
    }
}
