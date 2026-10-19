using System.ComponentModel.DataAnnotations;

namespace AuthService.Models;

/// <summary>Credentials used to authenticate an existing user.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Public profile information for the authenticated user.</summary>
public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles);

/// <summary>JWT access token and the authenticated user's profile.</summary>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    AuthUserResponse User);
