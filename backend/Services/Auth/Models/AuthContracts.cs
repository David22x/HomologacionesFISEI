using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Roles;

namespace AuthService.Models;

/// <summary>Credentials used to authenticate an existing user.</summary>
public sealed record LoginRequest
{
    /// <summary>Institutional email of the user.</summary>
    /// <example>profesor@uta.edu.ec</example>
    [Required, EmailAddress]
    public required string Email { get; init; }

    /// <summary>Account password.</summary>
    [Required]
    public required string Password { get; init; }
}

/// <summary>Public profile information for the authenticated user.</summary>
/// <param name="Id">Unique identifier of the user.</param>
/// <param name="Email">Email of the user.</param>
/// <param name="NombreCompleto">Full name of the user.</param>
/// <param name="Roles">Roles assigned to the user.</param>
public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles);

/// <summary>JWT access token and the authenticated user's profile.</summary>
/// <param name="AccessToken">JWT to send as <c>Authorization: Bearer {token}</c>.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresAt">Moment (UTC) when the token expires.</param>
/// <param name="User">Profile of the authenticated user.</param>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    AuthUserResponse User);

/// <summary>User data managed by an administrator.</summary>
public sealed record ManagedUserResponse(
    Guid Id,
    string Email,
    string NombreCompleto,
    bool Activo,
    IReadOnlyList<string> Roles);

/// <summary>Data required to create a managed account.</summary>
public sealed record CreateManagedUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public required string NombreCompleto { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }

    [Required]
    public required string Rol { get; init; }
}

/// <summary>Data required to update a managed account and its assigned role.</summary>
public sealed record UpdateManagedUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public required string NombreCompleto { get; init; }

    [Required]
    public required string Rol { get; init; }

    public bool Activo { get; init; } = true;
}
