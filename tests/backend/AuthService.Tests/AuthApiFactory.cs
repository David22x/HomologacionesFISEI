using AuthService.Data;
using AuthService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Tests;

/// <summary>Starts the Auth Service in memory, using an isolated InMemory database.</summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultPassword = "Prueba123*";

    private readonly string _databaseName = $"AuthDB-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Fallback only: the real configuration wins if it already defines the key (min. 32 chars).
        builder.UseSetting("Jwt:SecretKey", "clave-solo-para-pruebas-automatizadas-0123456789");

        builder.ConfigureTestServices(services =>
        {
            var contextDescriptors = services
                .Where(d => d.ServiceType == typeof(AuthDbContext)
                    || d.ServiceType.GenericTypeArguments.Contains(typeof(AuthDbContext)))
                .ToList();

            foreach (var descriptor in contextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task CreateUserAsync(
        string email,
        string password = DefaultPassword,
        bool activo = true,
        string? role = null)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "Usuario de prueba",
            Activo = activo
        };

        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role ?? AppRoles.Profesor));
    }

    public async Task DeactivateUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");
        user.Activo = false;
        EnsureSucceeded(await userManager.UpdateAsync(user));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
