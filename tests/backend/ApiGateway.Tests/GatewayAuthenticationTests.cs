using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Tests;

public sealed class GatewayAuthenticationTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public GatewayAuthenticationTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RutaProtegida_SinToken_Devuelve401()
    {
        var response = await _fixture.Client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenVencido_Devuelve401()
    {
        var token = GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(-1));

        var response = await GetWithTokenAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenAlterado_Devuelve401()
    {
        var token = GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(5));
        var tokenParts = token.Split('.');
        var signature = tokenParts[2];
        tokenParts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var alteredToken = string.Join('.', tokenParts);

        var response = await GetWithTokenAsync(alteredToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/estudiantes")]
    [InlineData("POST", "/api/v1/estudiantes")]
    [InlineData("GET", "/api/v1/estudiantes/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/v1/estudiantes/00000000-0000-0000-0000-000000000001/historial")]
    public async Task RutaStudents_SinToken_Devuelve401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = new StringContent("{}");

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/estudiantes")]
    [InlineData("POST", "/api/v1/estudiantes")]
    [InlineData("GET", "/api/v1/estudiantes/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/v1/estudiantes/00000000-0000-0000-0000-000000000001/historial")]
    public async Task RutaStudents_ConToken_ReenviaAlServicio(
        string method,
        string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(5)));

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void ConfiguracionStudents_UsaPuertoCorrectoYExigeBearerSinPublicarInternal()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")));
        var routes = document.RootElement.GetProperty("Routes").EnumerateArray()
            .Where(route => route.GetProperty("UpstreamPathTemplate").GetString()!
                .StartsWith("/api/v1/estudiantes", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, routes.Count);
        foreach (var route in routes)
        {
            Assert.Equal(
                5194,
                route.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());
            Assert.Equal(
                "Bearer",
                route.GetProperty("AuthenticationOptions")
                    .GetProperty("AuthenticationProviderKey").GetString());
            Assert.DoesNotContain(
                "internal",
                route.GetProperty("UpstreamPathTemplate").GetString()!,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("POST", "/api/v1/catalogo/carreras")]
    [InlineData("PUT", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001/mallas")]
    [InlineData("PUT", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/niveles")]
    [InlineData("PUT", "/api/v1/catalogo/niveles/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/asignaturas")]
    [InlineData("PUT", "/api/v1/catalogo/asignaturas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/publicar")]
    public async Task EscriturasDelCatalogo_SinToken_Devuelven401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = new StringContent("{}");

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/catalogo/carreras")]
    [InlineData("PUT", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001/mallas")]
    [InlineData("PUT", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/niveles")]
    [InlineData("PUT", "/api/v1/catalogo/niveles/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/asignaturas")]
    [InlineData("PUT", "/api/v1/catalogo/asignaturas/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/publicar")]
    public async Task EscriturasDelCatalogo_Propagan403DelServicio(
        string method,
        string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", GatewayApiFactory.CreateToken(DateTime.UtcNow.AddMinutes(5)));

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/internal/health")]
    [InlineData("/INTERNAL/health")]
    public async Task RutaInternal_NoEstaExpuestaPorElGateway(string path)
    {
        var response = await _fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetWithTokenAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _fixture.Client.SendAsync(request);
    }
}

public sealed class GatewayTestFixture : IAsyncLifetime
{
    private WebApplication? _downstream;
    private GatewayApiFactory? _factory;
    private string? _configurationDirectory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        _downstream = builder.Build();
        _downstream.MapMethods(
            "/api/v1/catalogo/{**path}",
            ["GET", "POST", "PUT"],
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            });
        _downstream.MapMethods(
            "/api/v1/estudiantes/{**path}",
            ["GET", "POST", "PUT"],
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            });
        _downstream.MapGet("/auth/me", () => Results.Ok());
        await _downstream.StartAsync();

        var address = _downstream.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        var downstreamPort = new Uri(address).Port;
        _configurationDirectory = Path.Combine(
            Path.GetTempPath(), $"gateway-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_configurationDirectory);
        var configurationPath = Path.Combine(_configurationDirectory, "ocelot.json");
        await File.WriteAllTextAsync(configurationPath, JsonSerializer.Serialize(new
        {
            Routes = new object[]
            {
                new
                {
                    DownstreamPathTemplate = "/api/v1/estudiantes",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new[]
                    {
                        new { Host = "127.0.0.1", Port = downstreamPort }
                    },
                    UpstreamPathTemplate = "/api/v1/estudiantes",
                    UpstreamHttpMethod = new[] { "GET", "POST" },
                    AuthenticationOptions = new
                    {
                        AuthenticationProviderKey = "Bearer",
                        AllowedScopes = Array.Empty<string>()
                    }
                },
                new
                {
                    DownstreamPathTemplate = "/api/v1/estudiantes/{everything}",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new[]
                    {
                        new { Host = "127.0.0.1", Port = downstreamPort }
                    },
                    UpstreamPathTemplate = "/api/v1/estudiantes/{everything}",
                    UpstreamHttpMethod = new[] { "GET", "PUT" },
                    AuthenticationOptions = new
                    {
                        AuthenticationProviderKey = "Bearer",
                        AllowedScopes = Array.Empty<string>()
                    }
                },
                new
                {
                    DownstreamPathTemplate = "/api/v1/catalogo/{everything}",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new[]
                    {
                        new { Host = "127.0.0.1", Port = downstreamPort }
                    },
                    UpstreamPathTemplate = "/api/v1/catalogo/{everything}",
                    UpstreamHttpMethod = new[] { "GET", "POST", "PUT" },
                    AuthenticationOptions = new
                    {
                        AuthenticationProviderKey = "Bearer",
                        AllowedScopes = Array.Empty<string>()
                    }
                },
                new
                {
                    DownstreamPathTemplate = "/auth/me",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new[]
                    {
                        new { Host = "127.0.0.1", Port = downstreamPort }
                    },
                    UpstreamPathTemplate = "/auth/me",
                    UpstreamHttpMethod = new[] { "GET" },
                    AuthenticationOptions = new
                    {
                        AuthenticationProviderKey = "Bearer",
                        AllowedScopes = Array.Empty<string>()
                    }
                }
            },
            GlobalConfiguration = new { BaseUrl = "http://localhost:5080" }
        }));

        _factory = new GatewayApiFactory(configurationPath);
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (_downstream is not null)
        {
            await _downstream.StopAsync();
            await _downstream.DisposeAsync();
        }
        if (_configurationDirectory is not null)
        {
            File.Delete(Path.Combine(_configurationDirectory, "ocelot.json"));
            Directory.Delete(_configurationDirectory);
        }
    }
}

public sealed class GatewayApiFactory(string ocelotConfigurationPath) : WebApplicationFactory<Program>
{
    private static readonly string SecretKey = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SecretKey", SecretKey);
        builder.UseSetting("Ocelot:ConfigurationFile", ocelotConfigurationPath);
    }

    public static string CreateToken(DateTime expiresAt)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var signingCredentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HomologacionesFISEI",
            audience: "HomologacionesFISEIClient",
            claims: [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
