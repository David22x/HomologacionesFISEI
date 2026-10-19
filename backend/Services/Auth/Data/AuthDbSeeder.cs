using AuthService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Data;

public static class AuthDbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Crear roles si no existen
        string[] roles = [AppRoles.Administrador, AppRoles.Profesor];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role)
                {
                    Descripcion = $"Rol de {role} del sistema"
                });
            }
        }

        // Crear usuario inicial Administrador (ej. admin@uta.edu.ec)
        var adminEmail = "admin@uta.edu.ec";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                NombreCompleto = "Administrador del Sistema",
                Activo = true
            };
            await userManager.CreateAsync(adminUser, "Admin123*");
            await userManager.AddToRoleAsync(adminUser, AppRoles.Administrador);
        }

        // Crear usuario inicial Profesor Evaluador (ej. profesor@uta.edu.ec)
        var profEmail = "profesor@uta.edu.ec";
        var profUser = await userManager.FindByEmailAsync(profEmail);
        if (profUser == null)
        {
            profUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = profEmail,
                Email = profEmail,
                EmailConfirmed = true,
                NombreCompleto = "Ing. Hernan Naranjo",
                Activo = true
            };
            await userManager.CreateAsync(profUser, "Profesor123*");
            await userManager.AddToRoleAsync(profUser, AppRoles.Profesor);
        }
    }
}