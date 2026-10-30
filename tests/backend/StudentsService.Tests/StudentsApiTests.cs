using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using StudentsService.Data;
using Xunit;

namespace StudentsService.Tests;

public sealed class StudentsApiTests : IClassFixture<StudentsApiFactory>
{
    private readonly HttpClient _client;
    public StudentsApiTests(StudentsApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Registra_historial_valido_lista_filtra_y_actualiza()
    {
                var cedula = NuevaCedula();
using var response = await Send(HttpMethod.Post, "/api/v1/estudiantes", new
        {
            cedula, tipoIdentificacion = 0, nombres = "Ana", apellidos = "Paz", tipo = 0,
            historial = new { carreraOrigenId = Guid.NewGuid(), carreraOrigenNombre = (string?)null,
                institucionOrigen = (string?)null, nivelActual = 2,
                asignaturas = new[] { new { asignaturaId = Guid.NewGuid(), codigo = "MAT1", nombre = "Matemática",
                    creditos = 3m, horas = 48, nota = 9.5m, periodo = "2026-A" } } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var studentId = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(cedula, json.RootElement.GetProperty("cedula").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("historial").GetProperty("asignaturas").GetArrayLength());

        using var filteredRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/estudiantes?filtro=Ana");
        filteredRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", StudentsApiFactory.Token("Administrador"));
        using var filtered = await _client.SendAsync(filteredRequest);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var update = await Send(HttpMethod.Put, $"/api/v1/estudiantes/{studentId}", new
        {
            cedula, tipoIdentificacion = 0, nombres = "Ana María", apellidos = "Paz", tipo = 0,
            historial = new { carreraOrigenId = Guid.NewGuid(), carreraOrigenNombre = (string?)null,
                institucionOrigen = (string?)null, nivelActual = 3, asignaturas = Array.Empty<object>() }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("0102030401")]
    public async Task Rechaza_cedula_con_formato_o_verificador_invalido(string cedula)
    {
        using var response = await Send(HttpMethod.Post, "/api/v1/estudiantes", new
        {
            cedula, tipoIdentificacion = 0, nombres = "Ana", apellidos = "Paz", tipo = 0,
            historial = new { carreraOrigenId = Guid.NewGuid(), nivelActual = 1,
                asignaturas = Array.Empty<object>() }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cedula_es_unica()
    {
        var body = new { cedula = NuevaCedula(), tipoIdentificacion = 0, nombres = "Ana", apellidos = "Paz", tipo = 1 };
        using var first = await Send(HttpMethod.Post, "/api/v1/estudiantes", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var second = await Send(HttpMethod.Post, "/api/v1/estudiantes", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static string NuevaCedula()
    {
        var primerosNueve = "011" + RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var suma = 0;
        for (var i = 0; i < 9; i++)
        {
            var digito = primerosNueve[i] - '0';
            if (i % 2 == 0) { digito *= 2; if (digito > 9) digito -= 9; }
            suma += digito;
        }
        return primerosNueve + ((10 - suma % 10) % 10).ToString();
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", StudentsApiFactory.Token("Administrador"));
        return await _client.SendAsync(request);
    }
}

public sealed class StudentsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        _connection.CreateFunction<string, int>("LEN", value => value?.Length ?? 0);
        await _connection.OpenAsync();
        using var client = CreateClient();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StudentsDbContext>().Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Students:MigrateOnStartup", "false");
        builder.UseSetting("ConnectionStrings:StudentsConnection", "Server=localhost;Database=StudentsTests");
        builder.UseSetting("Jwt:SecretKey", Secret);
        builder.ConfigureTestServices(services =>
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(StudentsDbContext)
                || d.ServiceType.GenericTypeArguments.Contains(typeof(StudentsDbContext))).ToList();
            foreach (var descriptor in descriptors) services.Remove(descriptor);
            services.AddDbContext<StudentsDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<StudentsDbContext>();
            services.AddScoped<StudentsDbContext>(provider => new SqliteStudentsDbContext(
                provider.GetRequiredService<DbContextOptions<StudentsDbContext>>()));
        });
    }

    private sealed class SqliteStudentsDbContext(DbContextOptions<StudentsDbContext> options)
        : StudentsDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<StudentsService.Models.AsignaturaCursada>()
                .Property(a => a.Nota).HasConversion<double>();
            modelBuilder.Entity<StudentsService.Models.AsignaturaCursada>()
                .Property(a => a.Creditos).HasConversion<double?>();
        }
    }
    public static string Token(string role)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, role) };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var token = new JwtSecurityToken(issuer: "HomologacionesFISEI", audience: "HomologacionesFISEIClient",
            claims: claims, notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
