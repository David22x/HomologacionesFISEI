using System.Security.Claims;
using AuthService.Models;

namespace AuthService.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthUserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
