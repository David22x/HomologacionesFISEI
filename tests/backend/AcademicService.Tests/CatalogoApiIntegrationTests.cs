using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using AcademicService.Contracts;
using AcademicService.Data;
using AcademicService.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AcademicService.Tests;

public sealed class CatalogoApiIntegrationTests : IClassFixture<CatalogApiFactory>
{
    private readonly CatalogApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogoApiIntegrationTests(CatalogApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Profesor")]
    public async Task Carreras_solo_se_consultan_con_un_rol_autorizado_y_exponen_el_contrato(
        string role)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/catalogo/carreras", role);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carreras = await response.Content.ReadFromJsonAsync<List<CarreraCatalogoResponse>>();
        Assert.NotNull(carreras);
        Assert.Contains(carreras, carrera => carrera.Codigo == "TI");
        Assert.Contains(carreras, carrera => carrera.Codigo == "SW");
        Assert.Contains(carreras, carrera => carrera.Codigo == "IND" && carrera.EsDemo);
        Assert.Contains(carreras, carrera => carrera.Codigo == "ROB" && carrera.EsDemo);
        Assert.Contains(carreras, carrera => carrera.Codigo == "TEL" && carrera.EsDemo);
    }

    [Fact]
    public async Task Flujo_de_consulta_recupera_mallas_niveles_y_asignaturas_con_sus_campos()
    {
        var carrera = await ObtenerCarreraAsync("TI");

        using var mallasResponse = await GetAsync(
            $"/api/v1/catalogo/carreras/{carrera.Id}/mallas", "Profesor");
        Assert.Equal(HttpStatusCode.OK, mallasResponse.StatusCode);
        var mallas = await mallasResponse.Content.ReadFromJsonAsync<List<MallaCatalogoResponse>>();
        Assert.NotNull(mallas);
        var malla = Assert.Single(mallas);
        Assert.Equal(carrera.Id, malla.CarreraId);

        using var nivelesResponse = await GetAsync(
            $"/api/v1/catalogo/mallas/{malla.Id}/niveles", "Profesor");
        Assert.Equal(HttpStatusCode.OK, nivelesResponse.StatusCode);
        var niveles = await nivelesResponse.Content.ReadFromJsonAsync<List<NivelCatalogoResponse>>();
        Assert.NotNull(niveles);
        Assert.NotEmpty(niveles);

        using var asignaturasResponse = await GetAsync(
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas?nivelId={niveles[0].Id}", "Profesor");
        Assert.Equal(HttpStatusCode.OK, asignaturasResponse.StatusCode);
        var asignaturas = await asignaturasResponse.Content.ReadFromJsonAsync<List<AsignaturaCatalogoResponse>>();
        Assert.NotNull(asignaturas);
        Assert.NotEmpty(asignaturas);

        var asignatura = Assert.Single(asignaturas,
            item => item.Codigo == "UTA-FISEI-TI-MP-UB-01-01");
        Assert.Equal("ALGEBRA LINEAL", asignatura.Nombre);
        Assert.True(asignatura.Creditos > 0);
        Assert.True(asignatura.Horas > 0);
        Assert.False(string.IsNullOrWhiteSpace(asignatura.UnidadOrganizacional));
        Assert.Equal(niveles[0].Numero, asignatura.NivelNumero);
    }

    [Fact]
    public async Task Filtros_de_asignaturas_combinan_nivel_y_busqueda_textual()
    {
        var carrera = await ObtenerCarreraAsync("TI");
        var malla = await ObtenerMallaAsync(carrera.Id);
        var nivel = await ObtenerNivelQueContieneAsync(malla.Id, "UTA-FISEI-TI-MP-UB-01-01");

        using var response = await GetAsync(
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas?nivelId={nivel.Id}&search=algebra",
            "Administrador");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var asignaturas = await response.Content.ReadFromJsonAsync<List<AsignaturaCatalogoResponse>>();
        Assert.NotNull(asignaturas);
        Assert.NotEmpty(asignaturas);
        Assert.All(asignaturas, asignatura =>
        {
            Assert.Equal(nivel.Id, asignatura.NivelId);
            Assert.Contains("algebra", asignatura.Nombre, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Rutas_del_catalogo_responden_401_sin_token_y_403_con_rol_no_autorizado()
    {
        using var sinToken = await _client.GetAsync("/api/v1/catalogo/carreras");
        using var rolNoAutorizado = await _client.SendAsync(
            CreateRequest(HttpMethod.Get, "/api/v1/catalogo/carreras", "SinPermiso"));

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rolNoAutorizado.StatusCode);
    }

    [Fact]
    public async Task Recursos_inexistentes_responden_404()
    {
        var noExiste = Guid.NewGuid();
        using var carreraResponse = await GetAsync(
            $"/api/v1/catalogo/carreras/{noExiste}/mallas", "Administrador");
        using var mallaResponse = await GetAsync(
            $"/api/v1/catalogo/mallas/{noExiste}/niveles", "Profesor");

        Assert.Equal(HttpStatusCode.NotFound, carreraResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mallaResponse.StatusCode);
    }

    [Fact]
    public async Task Filtros_con_formato_invalido_responden_400()
    {
        var carrera = await ObtenerCarreraAsync("TI");
        var malla = await ObtenerMallaAsync(carrera.Id);

        using var response = await GetAsync(
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas?nivelId=no-es-guid", "Profesor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_documenta_rutas_parametros_respuestas_y_esquemas_del_catalogo()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _));

        AssertGetResponses(paths.GetProperty("/api/v1/catalogo/carreras").GetProperty("get"),
            "200", "401", "403");
        AssertGetResponses(paths.GetProperty("/api/v1/catalogo/carreras/{carreraId}/mallas").GetProperty("get"),
            "200", "401", "403", "404");
        AssertGetResponses(paths.GetProperty("/api/v1/catalogo/mallas/{mallaId}/niveles").GetProperty("get"),
            "200", "401", "403", "404");

        var asignaturas = paths
            .GetProperty("/api/v1/catalogo/mallas/{mallaId}/asignaturas")
            .GetProperty("get");
        AssertGetResponses(asignaturas, "200", "400", "401", "403", "404");
        var queryParameters = asignaturas.GetProperty("parameters")
            .EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("nivelId", queryParameters);
        Assert.Contains("nivel", queryParameters);
        Assert.Contains("search", queryParameters);

        var schemas = root.GetProperty("components").GetProperty("schemas");
        AssertProperties(schemas, nameof(CarreraCatalogoResponse), "id", "codigo", "nombre", "esDemo");
        AssertProperties(schemas, nameof(MallaCatalogoResponse), "id", "carreraId", "version", "vigente");
        AssertProperties(schemas, nameof(NivelCatalogoResponse), "id", "mallaVersionId", "numero");
        AssertProperties(schemas, nameof(AsignaturaCatalogoResponse), "id", "mallaVersionId", "nivelId",
            "nivelNumero", "codigo", "nombre", "creditos", "horas", "unidadOrganizacional");

        var responseSchema = asignaturas.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", responseSchema.GetProperty("type").GetString());
        Assert.EndsWith(nameof(AsignaturaCatalogoResponse),
            responseSchema.GetProperty("items").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    private async Task<CarreraCatalogoResponse> ObtenerCarreraAsync(string codigo)
    {
        using var response = await GetAsync("/api/v1/catalogo/carreras", "Administrador");
        response.EnsureSuccessStatusCode();
        var carreras = await response.Content.ReadFromJsonAsync<List<CarreraCatalogoResponse>>();
        return Assert.Single(carreras!, item => item.Codigo == codigo);
    }

    private async Task<MallaCatalogoResponse> ObtenerMallaAsync(Guid carreraId)
    {
        using var response = await GetAsync(
            $"/api/v1/catalogo/carreras/{carreraId}/mallas", "Administrador");
        response.EnsureSuccessStatusCode();
        var mallas = await response.Content.ReadFromJsonAsync<List<MallaCatalogoResponse>>();
        return Assert.Single(mallas!);
    }

    private async Task<NivelCatalogoResponse> ObtenerNivelQueContieneAsync(Guid mallaId, string codigo)
    {
        using var response = await GetAsync(
            $"/api/v1/catalogo/mallas/{mallaId}/asignaturas?search={Uri.EscapeDataString(codigo)}",
            "Profesor");
        response.EnsureSuccessStatusCode();
        var asignaturas = await response.Content.ReadFromJsonAsync<List<AsignaturaCatalogoResponse>>();
        var asignatura = Assert.Single(asignaturas!);

        using var nivelesResponse = await GetAsync(
            $"/api/v1/catalogo/mallas/{mallaId}/niveles", "Profesor");
        nivelesResponse.EnsureSuccessStatusCode();
        var niveles = await nivelesResponse.Content.ReadFromJsonAsync<List<NivelCatalogoResponse>>();
        return Assert.Single(niveles!, item => item.Id == asignatura.NivelId);
    }

    private static void AssertGetResponses(JsonElement operation, params string[] expectedCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (var code in expectedCodes)
        {
            Assert.True(responses.TryGetProperty(code, out _), $"Swagger no documenta la respuesta {code}.");
        }
    }

    private static void AssertProperties(JsonElement schemas, string schemaName, params string[] expectedProperties)
    {
        var properties = schemas.GetProperty(schemaName).GetProperty("properties");
        foreach (var name in expectedProperties)
        {
            Assert.True(properties.TryGetProperty(name, out _),
                $"El esquema {schemaName} no documenta la propiedad {name}.");
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.CreateToken(role));
        return request;
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string role)
    {
        using var request = CreateRequest(HttpMethod.Get, path, role);
        return await _client.SendAsync(request);
    }
}

public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Issuer = "AcademicService.Tests";
    private const string Audience = "AcademicService.Tests.Client";
    private static readonly string SecretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var client = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seedPath = Path.Combine(AppContext.BaseDirectory, "Seed", "seed_academic_sprint1.json");
        var seedJson = await File.ReadAllTextAsync(seedPath);
        await AcademicDbSeeder.SeedFromJsonAsync(db, seedJson);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Academic:SeedOnStartup", "false");
        builder.UseSetting("ConnectionStrings:AcademicConnection", "Server=localhost;Database=AcademicTests");
        builder.UseSetting("Jwt:SecretKey", SecretKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.ConfigureTestServices(services =>
        {
            var contextDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(AcademicDbContext)
                    || descriptor.ServiceType.GenericTypeArguments.Contains(typeof(AcademicDbContext)))
                .ToList();
            foreach (var descriptor in contextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AcademicDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<AcademicDbContext>();
            services.AddScoped<AcademicDbContext>(provider =>
                new SqliteAcademicDbContext(
                    provider.GetRequiredService<DbContextOptions<AcademicDbContext>>()));
        });
    }

    public static string CreateToken(string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"test-{role}"),
            new Claim(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class SqliteAcademicDbContext(DbContextOptions<AcademicDbContext> options)
        : AcademicDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Asignatura>().Property(asignatura => asignatura.Creditos)
                .HasConversion<double>();
        }
    }
}
