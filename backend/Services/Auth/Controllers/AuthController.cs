using AuthService.Models;
using AuthService.Services;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IAuthService authService,
    UserManager<ApplicationUser> userManager) : ControllerBase
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
        return response is null
            ? Unauthorized(new { message = "Credenciales inválidas o cuenta inactiva." })
            : Ok(response);
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

    /// <summary>Lists user accounts for administrators.</summary>
    [HttpGet("usuarios")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagedUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ManagedUserResponse>>> ListUsers(CancellationToken ct)
    {
        var users = await userManager.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(ct);
        var responses = new List<ManagedUserResponse>(users.Count);
        foreach (var user in users)
            responses.Add(await ToManagedResponseAsync(user));
        return Ok(responses);
    }

    /// <summary>Creates a user and assigns either the Administrador or Profesor role.</summary>
    [HttpPost("usuarios")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(ManagedUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagedUserResponse>> CreateUser(
        CreateManagedUserRequest request, CancellationToken ct)
    {
        if (!ValidRole(request.Rol)) return BadRequest(new { message = "El rol debe ser Administrador o Profesor." });
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "Ya existe un usuario con ese correo." });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
            NombreCompleto = request.NombreCompleto.Trim(), Activo = true
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) return BadRequest(new { errors = created.Errors.Select(error => error.Description) });

        var assigned = await userManager.AddToRoleAsync(user, request.Rol);
        if (!assigned.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return BadRequest(new { errors = assigned.Errors.Select(error => error.Description) });
        }

        var response = await ToManagedResponseAsync(user);
        return Created($"/auth/usuarios/{user.Id}", response);
    }

    /// <summary>Updates a user's profile, role and active status.</summary>
    [HttpPut("usuarios/{userId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(ManagedUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagedUserResponse>> UpdateUser(
        Guid userId, UpdateManagedUserRequest request, CancellationToken ct)
    {
        if (!ValidRole(request.Rol)) return BadRequest(new { message = "El rol debe ser Administrador o Profesor." });
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        var email = request.Email.Trim();
        var duplicate = await userManager.FindByEmailAsync(email);
        if (duplicate is not null && duplicate.Id != user.Id)
            return Conflict(new { message = "Ya existe un usuario con ese correo." });

        var currentRoles = await userManager.GetRolesAsync(user);
        var isSelf = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.Id.ToString();
        var isRemovingAdmin = currentRoles.Contains(AppRoles.Administrador)
            && (!request.Activo || request.Rol != AppRoles.Administrador);
        if (isRemovingAdmin && await IsLastActiveAdministratorAsync(user, ct))
            return Conflict(new { message = "Debe permanecer al menos un administrador activo." });
        if (isSelf && isRemovingAdmin)
            return Conflict(new { message = "No puedes desactivar ni retirar el rol de administrador de tu propia cuenta." });

        user.Email = email;
        user.UserName = email;
        user.NombreCompleto = request.NombreCompleto.Trim();
        user.Activo = request.Activo;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return BadRequest(new { errors = updated.Errors.Select(error => error.Description) });

        var remove = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!remove.Succeeded) return BadRequest(new { errors = remove.Errors.Select(error => error.Description) });
        var assigned = await userManager.AddToRoleAsync(user, request.Rol);
        if (!assigned.Succeeded) return BadRequest(new { errors = assigned.Errors.Select(error => error.Description) });
        return Ok(await ToManagedResponseAsync(user));
    }

    private static bool ValidRole(string role) => role is AppRoles.Administrador or AppRoles.Profesor;

    private async Task<bool> IsLastActiveAdministratorAsync(ApplicationUser user, CancellationToken ct)
    {
        var activeUsers = await userManager.Users.Where(candidate => candidate.Activo && candidate.Id != user.Id).ToListAsync(ct);
        foreach (var candidate in activeUsers)
            if (await userManager.IsInRoleAsync(candidate, AppRoles.Administrador)) return false;
        return true;
    }

    private async Task<ManagedUserResponse> ToManagedResponseAsync(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.NombreCompleto, user.Activo,
            (await userManager.GetRolesAsync(user)).ToArray());
}
