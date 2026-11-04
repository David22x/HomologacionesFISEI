using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AuthService.Models;
using BuildingBlocks.Authentication;
using BuildingBlocks.Authorization;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests;

/// <summary>
/// Gets, once per test class, the real tokens issued by the Auth Service login for the
/// initial users, before any other host is created.
/// </summary>
public sealed class RealTokensFixture : IAsyncLifetime
{
    private readonly AuthApiFactory _authFactory = new();

    public string AdminToken { get; private set; } = string.Empty;
    public string ProfesorToken { get; private set; } = string.Empty;
    public JwtSettings Jwt { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Jwt = _authFactory.Services.GetRequiredService<JwtSettings>();
        AdminToken = await LoginAsync("admin@uta.edu.ec", AuthApiFactory.AdminPassword);
        ProfesorToken = await LoginAsync("profesor@uta.edu.ec", AuthApiFactory.ProfessorPassword);
    }

    public Task DisposeAsync()
    {
        _authFactory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        using var client = _authFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Login de {email} devolvio {(int)response.StatusCode}: {await DescribeUserAsync(email, password)}.");
        }

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private async Task<string> DescribeUserAsync(string email, string password)
    {
        using var scope = _authFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return "el usuario NO existe en la base de la fabrica (el sembrado no corrio o fallo)";
        }

        return $"el usuario existe; activo={user.Activo}; " +
               $"contrasena correcta={await users.CheckPasswordAsync(user, password)}; " +
               $"bloqueado={await users.IsLockedOutAsync(user)}";
    }
}

/// <summary>
/// Verifies 401/403 on the shared role policies and attributes from BuildingBlocks.
/// A small in-memory host exposes toy routes protected by each policy. Admin and professor
/// tokens come from the real login of the Auth Service, so the test also proves that the
/// issued token is understood by the shared authorization.
/// </summary>
public sealed class RoleAuthorizationTests(RealTokensFixture tokens)
    : IClassFixture<RealTokensFixture>, IAsyncLifetime
{
    private const string Admin = "admin";
    private const string Profesor = "profesor";
    private const string SinRol = "sin-rol";

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:SecretKey"] = tokens.Jwt.SecretKey,
                        ["Jwt:Issuer"] = tokens.Jwt.Issuer,
                        ["Jwt:Audience"] = tokens.Jwt.Audience
                    }));
                web.ConfigureServices((context, services) =>
                {
                    services.AddRouting();
                    services.AddCustomJwtAuthentication(context.Configuration);
                    services.AddCustomAuthorizationPolicies();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/publica", () => "ok").AllowAnonymous();
                        endpoints.MapGet("/autenticado", () => "ok").RequireAuthorization();
                        endpoints.MapGet("/admin", () => "ok")
                            .RequireAuthorization(AppPolicies.RequireAdminRole);
                        endpoints.MapGet("/profesor", () => "ok")
                            .RequireAuthorization(AppPolicies.RequireProfesorRole);
                        endpoints.MapGet("/cualquiera", () => "ok")
                            .RequireAuthorization(AppPolicies.RequireAnyRole);
                        endpoints.MapGet("/atributo-admin", () => "ok")
                            .RequireAuthorization(new AuthorizeRolesAttribute(AppRoles.Administrador));
                        endpoints.MapGet("/atributo-ambos", () => "ok")
                            .RequireAuthorization(
                                new AuthorizeRolesAttribute(AppRoles.Administrador, AppRoles.Profesor));
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    // ---------- Rol correcto: 200 / rol equivocado: 403 ----------

    public static TheoryData<string, string, HttpStatusCode> RolesPorRuta => new()
    {
        { "/admin", Admin, HttpStatusCode.OK },
        { "/admin", Profesor, HttpStatusCode.Forbidden },
        { "/profesor", Profesor, HttpStatusCode.OK },
        { "/profesor", Admin, HttpStatusCode.Forbidden },
        { "/cualquiera", Admin, HttpStatusCode.OK },
        { "/cualquiera", Profesor, HttpStatusCode.OK },
        { "/atributo-admin", Admin, HttpStatusCode.OK },
        { "/atributo-admin", Profesor, HttpStatusCode.Forbidden },
        { "/atributo-ambos", Admin, HttpStatusCode.OK },
        { "/atributo-ambos", Profesor, HttpStatusCode.OK },
        { "/autenticado", Admin, HttpStatusCode.OK },
        { "/autenticado", Profesor, HttpStatusCode.OK }
    };

    [Theory]
    [MemberData(nameof(RolesPorRuta))]
    public async Task RutaConRol_RespondeSegunElRolDelToken(
        string path, string quien, HttpStatusCode esperado)
    {
        var response = await GetAsync(path, Token(quien));

        Assert.Equal(esperado, response.StatusCode);
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/profesor")]
    [InlineData("/cualquiera")]
    [InlineData("/atributo-admin")]
    [InlineData("/atributo-ambos")]
    [InlineData("/autenticado")]
    public async Task RutaProtegida_SinToken_Devuelve401(string path)
    {
        var response = await GetAsync(path, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaPublica_SinToken_Devuelve200()
    {
        var response = await GetAsync("/publica", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Tokens que no deben aceptarse (401) ----------

    [Fact]
    public async Task RutaProtegida_ConTokenFalso_Devuelve401()
    {
        var response = await GetAsync("/cualquiera", "este.token.es-falso");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenVencido_Devuelve401()
    {
        var token = BuildToken(tokens.Jwt.SecretKey, DateTime.UtcNow.AddMinutes(-1), AppRoles.Administrador);

        var response = await GetAsync("/admin", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RutaProtegida_ConTokenFirmadoConOtraClave_Devuelve401()
    {
        var token = BuildToken(
            "otra-clave-distinta-para-firmar-0123456789",
            DateTime.UtcNow.AddMinutes(5),
            AppRoles.Administrador);

        var response = await GetAsync("/admin", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Token valido pero sin rol ----------

    [Fact]
    public async Task TokenValidoSinRol_EsRechazadoEnRutasConRol_PeroAceptadoSiSoloExigeAutenticacion()
    {
        var token = Token(SinRol);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/admin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/cualquiera", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/autenticado", token)).StatusCode);
    }

    // ---------- Ayudas ----------

    private string Token(string quien) => quien switch
    {
        Admin => tokens.AdminToken,
        Profesor => tokens.ProfesorToken,
        SinRol => BuildToken(tokens.Jwt.SecretKey, DateTime.UtcNow.AddMinutes(5)),
        _ => throw new ArgumentOutOfRangeException(nameof(quien), quien, null)
    };

    private string BuildToken(string secretKey, DateTime expiresAt, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "usuario-de-prueba") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: tokens.Jwt.Issuer,
            audience: tokens.Jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }
}
