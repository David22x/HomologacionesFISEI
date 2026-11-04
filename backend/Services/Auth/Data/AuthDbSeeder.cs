using AuthService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Data;

public static class AuthDbSeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        bool seedDevelopmentUsers)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        string[] roles = [AppRoles.Administrador, AppRoles.Profesor];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new ApplicationRole(role)
                {
                    Descripcion = $"Rol de {role} del sistema"
                }));
            }
        }

        if (!seedDevelopmentUsers)
        {
            return;
        }

        await EnsureDevelopmentUserAsync(
            userManager,
            "admin@uta.edu.ec",
            "Administrador del Sistema",
            AppRoles.Administrador,
            GetRequiredSetting(configuration, "SeedUsers:AdministradorPassword"));
        await EnsureDevelopmentUserAsync(
            userManager,
            "profesor@uta.edu.ec",
            "Ing. Hernan Naranjo",
            AppRoles.Profesor,
            GetRequiredSetting(configuration, "SeedUsers:ProfesorPassword"));
    }

    private static async Task EnsureDevelopmentUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = fullName,
                Activo = true
            };

            EnsureSucceeded(await userManager.CreateAsync(user, password));
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        }
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException(
                $"Configura {key} mediante User Secrets o una variable de entorno para sembrar los usuarios de desarrollo.");

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}