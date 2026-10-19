using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Models;
using BuildingBlocks.Roles;

namespace AuthService.Tests;

public sealed class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Initial users created by AuthDbSeeder (one per role).
    public static TheoryData<string, string, string> UsuariosIniciales => new()
    {
        { "admin@uta.edu.ec", AuthApiFactory.AdminPassword, AppRoles.Administrador },
        { "profesor@uta.edu.ec", AuthApiFactory.ProfessorPassword, AppRoles.Profesor }
    };

    private static string NewEmail() => $"{Guid.NewGuid():N}@pruebas.test";

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/auth/login", new { email, password });

    private async Task<LoginResponse> LoginOkAsync(string email, string password)
    {
        var response = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return Assert.IsType<LoginResponse>(body);
    }

    private Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    // ---------- POST /auth/login ----------

    [Theory]
    [MemberData(nameof(UsuariosIniciales))]
    public async Task Login_ConUsuarioInicial_DevuelveTokenYRolCorrecto(string email, string password, string rol)
    {
        var body = await LoginOkAsync(email, password);

        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Bearer", body.TokenType);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(email, body.User.Email);
        Assert.Contains(rol, body.User.Roles);
        Assert.False(string.IsNullOrWhiteSpace(body.User.NombreCompleto));
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_Devuelve401()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email);

        var response = await LoginAsync(email, "Incorrecta999*");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ConUsuarioInexistente_Devuelve401()
    {
        var response = await LoginAsync(NewEmail(), AuthApiFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ConUsuarioInactivo_Devuelve401AunqueLaContrasenaSeaCorrecta()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email, activo: false);

        var response = await LoginAsync(email, AuthApiFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "dato-no-secreto")]
    [InlineData("no-es-un-correo", "dato-no-secreto")]
    [InlineData("usuario@pruebas.test", "")]
    public async Task Login_ConDatosInvalidos_Devuelve400(string email, string password)
    {
        var response = await LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_TrasCincoIntentosFallidos_BloqueaLaCuenta()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email);

        for (var intento = 0; intento < 5; intento++)
        {
            var fallido = await LoginAsync(email, "Incorrecta999*");
            Assert.Equal(HttpStatusCode.Unauthorized, fallido.StatusCode);
        }

        var conClaveCorrecta = await LoginAsync(email, AuthApiFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, conClaveCorrecta.StatusCode);
    }

    // ---------- GET /auth/me ----------

    [Fact]
    public async Task Me_ConTokenValido_DevuelvePerfilDelUsuario()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email, role: AppRoles.Administrador);
        var login = await LoginOkAsync(email, AuthApiFactory.DefaultPassword);

        var response = await GetMeAsync(login.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var perfil = await response.Content.ReadFromJsonAsync<AuthUserResponse>();
        Assert.NotNull(perfil);
        Assert.Equal(email, perfil.Email);
        Assert.Equal("Usuario de prueba", perfil.NombreCompleto);
        Assert.Contains(AppRoles.Administrador, perfil.Roles);
    }

    [Fact]
    public async Task Me_SinToken_Devuelve401()
    {
        var response = await GetMeAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ConTokenInvalido_Devuelve401()
    {
        var response = await GetMeAsync("este.token.es-falso");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_SiElUsuarioFueDesactivadoDespuesDelLogin_Devuelve401()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email);
        var login = await LoginOkAsync(email, AuthApiFactory.DefaultPassword);

        await factory.DeactivateUserAsync(email);
        var response = await GetMeAsync(login.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
