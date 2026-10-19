using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication;

public sealed class JwtSettings
{
    public const string DefaultSecretKey = "ClaveSecretaSuperSeguraParaDesarrolloLocalSprint1_2026!";
    public const string DefaultIssuer = "HomologacionesFISEI";
    public const string DefaultAudience = "HomologacionesFISEIClient";

    public string SecretKey { get; init; } = DefaultSecretKey;
    public string Issuer { get; init; } = DefaultIssuer;
    public string Audience { get; init; } = DefaultAudience;
    public int ExpirationMinutes { get; init; } = 60;
}

public static class JwtExtensions
{
    public static IServiceCollection AddCustomJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var expirationMinutes = GetExpirationMinutes(configuration);
        var jwtSettings = new JwtSettings
        {
            SecretKey = configuration["Jwt:SecretKey"] ?? JwtSettings.DefaultSecretKey,
            Issuer = configuration["Jwt:Issuer"] ?? JwtSettings.DefaultIssuer,
            Audience = configuration["Jwt:Audience"] ?? JwtSettings.DefaultAudience,
            ExpirationMinutes = expirationMinutes
        };

        if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
        {
            throw new InvalidOperationException("La clave JWT debe tener al menos 32 bytes.");
        }

        services.AddSingleton(jwtSettings);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    private static int GetExpirationMinutes(IConfiguration configuration)
    {
        var configuredExpiration = configuration["Jwt:ExpirationMinutes"];
        if (configuredExpiration is null)
        {
            return 60;
        }

        if (!int.TryParse(configuredExpiration, out var expirationMinutes) || expirationMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:ExpirationMinutes debe ser un entero positivo.");
        }

        return expirationMinutes;
    }
}