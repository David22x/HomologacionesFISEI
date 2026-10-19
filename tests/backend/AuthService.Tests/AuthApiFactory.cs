using System.Security.Cryptography;
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
    public static readonly string DefaultPassword = CreateDevelopmentPassword("Test");
    public static readonly string AdminPassword = CreateDevelopmentPassword("Admin");
    public static readonly string ProfessorPassword = CreateDevelopmentPassword("Professor");
    private static readonly string JwtSecretKey = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32));

    private readonly string _databaseName = $"AuthDB-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("Jwt:SecretKey", JwtSecretKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(local);Database=AuthDB-tests");
        builder.UseSetting("SeedUsers:AdministradorPassword", AdminPassword);
        builder.UseSetting("SeedUsers:ProfesorPassword", ProfessorPassword);

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
        string? password = null,
        bool activo = true,
        string? role = null)
    {
        password ??= DefaultPassword;

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

    private static string CreateDevelopmentPassword(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}a1";
}
