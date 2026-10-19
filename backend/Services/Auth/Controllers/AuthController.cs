using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Authenticates credentials and issues a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }

    /// <summary>Returns the profile associated with the supplied bearer token.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var response = await authService.GetCurrentUserAsync(User, cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }
}
