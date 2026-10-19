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
    /// <remarks>
    /// Send the returned token as <c>Authorization: Bearer {token}</c> to the protected endpoints.
    /// The account is locked for a few minutes after 5 consecutive failed attempts.
    /// </remarks>
    /// <response code="200">Valid credentials. Returns the token and the user profile.</response>
    /// <response code="400">The email or the password is missing or malformed.</response>
    /// <response code="401">Wrong credentials, inactive user or locked account.</response>
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
    /// <response code="200">Profile of the authenticated user.</response>
    /// <response code="401">Missing, invalid or expired token, or the user is inactive.</response>
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
