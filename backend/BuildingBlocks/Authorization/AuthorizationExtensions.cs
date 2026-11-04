using BuildingBlocks.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddCustomAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireAdminRole, policy =>
                policy.RequireRole(AppRoles.Administrador));

            options.AddPolicy(AppPolicies.RequireProfesorRole, policy =>
                policy.RequireRole(AppRoles.Profesor));

            options.AddPolicy(AppPolicies.RequireAnyRole, policy =>
                policy.RequireRole(AppRoles.Administrador, AppRoles.Profesor));
        });

        return services;
    }
}