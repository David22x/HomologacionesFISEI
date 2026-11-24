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
    public async Task Contenidos_externos_se_persisten_y_se_solicitan_los_faltantes()
    {
        var cedula = "EX" + Guid.NewGuid().ToString("N")[..12];
        object Payload(bool completo) => new { cedula, tipoIdentificacion = 1, nombres = "Ana", apellidos = "Externa", tipo = 1,
            historial = new { carreraOrigenNombre = "Informatica", institucionOrigen = "Universidad externa", nivelActual = 3,
                asignaturas = new[] { new { codigo = "EXT1", nombre = "Programacion", creditos = 4, horas = 64, nota = 9m, periodo = "2026-A",
                    temas = completo ? new object[] { new { nombre = "Variables", subtemas = new[] { "Declaracion" } } } : Array.Empty<object>() } } } };
        using var first = await Send(HttpMethod.Put, "/internal/estudiantes/importaciones", Payload(false));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var initial = JsonSerializer.Deserialize<JsonElement>(await first.Content.ReadAsStringAsync());
        Assert.False(initial.GetProperty("listoParaHomologar").GetBoolean());
        Assert.NotEmpty(initial.GetProperty("informacionFaltante").EnumerateArray());
        var studentId = initial.GetProperty("id").GetGuid();
        using var complete = await Send(HttpMethod.Put, "/internal/estudiantes/importaciones", Payload(true));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var updated = JsonSerializer.Deserialize<JsonElement>(await complete.Content.ReadAsStringAsync());
        Assert.True(updated.GetProperty("listoParaHomologar").GetBoolean());
        Assert.Empty(updated.GetProperty("informacionFaltante").EnumerateArray());
        using var history = await Get($"/api/v1/estudiantes/{studentId}/historial", "Profesor");
        var stored = JsonSerializer.Deserialize<JsonElement>(await history.Content.ReadAsStringAsync());
        Assert.True(stored.GetProperty("esExterno").GetBoolean());
        Assert.Equal("Declaracion", stored.GetProperty("asignaturas")[0].GetProperty("temas")[0].GetProperty("subtemas")[0].GetString());
    }

    [Fact]
    public async Task Editar_agrega_elimina_y_conserva_ids_de_materias_y_permite_baja_logica()
    {
        var cedula = NuevaCedula(); var carrera = Guid.NewGuid(); var malla = Guid.NewGuid();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        object Course(Guid id, string nombre) => new { asignaturaId = id, codigo = nombre, nombre, creditos = 3m, horas = 48, nota = 8m, periodo = "2026-A" };
        object Payload(params object[] courses) => new { cedula, tipoIdentificacion = 0, nombres = "Regresion", apellidos = "Historial", tipo = 0,
            historial = new { carreraOrigenId = carrera, mallaOrigenId = malla, nivelActual = 3, asignaturas = courses } };
        using var created = await Send(HttpMethod.Post, "/api/v1/estudiantes", Payload(Course(a, "Matematica")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        var id = original.GetProperty("id").GetGuid();
        var courseId = original.GetProperty("historial").GetProperty("asignaturas")[0].GetProperty("id").GetGuid();
        using var updated = await Send(HttpMethod.Put, $"/api/v1/estudiantes/{id}", Payload(Course(a, "Matematica"), Course(b, "Fisica")));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var history = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement.GetProperty("historial");
        Assert.Equal(2, history.GetProperty("asignaturas").GetArrayLength());
        Assert.Equal(malla, history.GetProperty("mallaOrigenId").GetGuid());
        Assert.Contains(history.GetProperty("asignaturas").EnumerateArray(), x => x.GetProperty("id").GetGuid() == courseId);
        using var removed = await Send(HttpMethod.Put, $"/api/v1/estudiantes/{id}", Payload(Course(b, "Fisica")));
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Single(JsonDocument.Parse(await removed.Content.ReadAsStringAsync()).RootElement.GetProperty("historial").GetProperty("asignaturas").EnumerateArray());
        using var denied = await _client.DeleteAsync($"/api/v1/estudiantes/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var deleted = await Send(HttpMethod.Delete, $"/api/v1/estudiantes/{id}", new { });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var search = await Get($"/api/v1/estudiantes/cedula/{cedula}", "Profesor");
        Assert.Equal(HttpStatusCode.NotFound, search.StatusCode);
        using var list = await Get("/api/v1/estudiantes", "Profesor");
        Assert.DoesNotContain(JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        // Una referencia de un expediente existente conserva su historial.
        using var archived = await Get($"/api/v1/estudiantes/{id}", "Profesor");
        Assert.True(JsonDocument.Parse(await archived.Content.ReadAsStringAsync()).RootElement.GetProperty("eliminado").GetBoolean());
        using var mutation = await Send(HttpMethod.Put, $"/api/v1/estudiantes/{id}", Payload(Course(a, "Matematica")));
        Assert.Equal(HttpStatusCode.NotFound, mutation.StatusCode);
    }

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

    [Fact]
    public async Task ImportacionInterna_RepetidaEsIdempotente_YReemplazaHistorialSiCambia()
    {
        const string identification = "AB12345";
        var body = ExternalStudentImport(identification, "MAT1", "Matemática");

        using var created = await Send(HttpMethod.Put, "/internal/estudiantes/importaciones", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var first = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var studentId = first.RootElement.GetProperty("id").GetGuid();
        var historyId = first.RootElement.GetProperty("historial").GetProperty("id").GetGuid();
        var courseId = first.RootElement.GetProperty("historial").GetProperty("asignaturas")[0]
            .GetProperty("id").GetGuid();
        var updatedAt = first.RootElement.GetProperty("historial").GetProperty("actualizadoEn").GetDateTime();

        using var repeated = await Send(HttpMethod.Put, "/internal/estudiantes/importaciones", body);
        Assert.True(repeated.StatusCode == HttpStatusCode.OK,
            await repeated.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
        Assert.Equal(studentId, second.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(historyId, second.RootElement.GetProperty("historial").GetProperty("id").GetGuid());
        Assert.Equal(courseId, second.RootElement.GetProperty("historial").GetProperty("asignaturas")[0]
            .GetProperty("id").GetGuid());
        Assert.Equal(updatedAt, second.RootElement.GetProperty("historial").GetProperty("actualizadoEn").GetDateTime());
        Assert.Equal(2, second.RootElement.GetProperty("historial").GetProperty("asignaturas").GetArrayLength());

        using var changed = await Send(HttpMethod.Put, "/internal/estudiantes/importaciones",
            ExternalStudentImport(identification, "BIO1", "Biología", includeSecond: false));
        Assert.True(changed.StatusCode == HttpStatusCode.OK,
            await changed.Content.ReadAsStringAsync());
        using var third = JsonDocument.Parse(await changed.Content.ReadAsStringAsync());
        var courses = third.RootElement.GetProperty("historial").GetProperty("asignaturas");
        Assert.Single(courses.EnumerateArray());
        Assert.Equal("BIO1", courses[0].GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task ImportacionInterna_RequiereJwt()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/internal/estudiantes/importaciones")
        {
            Content = new StringContent("{}")
        };
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Profesor")]
    public async Task Busca_por_cedula_permite_roles_autorizados(string role)
    {
        var cedula = NuevaCedula();
        await CrearEstudiante(cedula);

        using var found = await Get($"/api/v1/estudiantes/cedula/{cedula}", role);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        using var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync());
        Assert.Equal(cedula, json.RootElement.GetProperty("cedula").GetString());

        using var partial = await Get($"/api/v1/estudiantes/cedula/{cedula[..9]}", role);
        Assert.Equal(HttpStatusCode.NotFound, partial.StatusCode);
    }

    [Fact]
    public async Task Cedula_inexistente_y_rol_no_autorizado_devuelven_la_misma_respuesta()
    {
        var existingCedula = NuevaCedula();
        await CrearEstudiante(existingCedula);

        using var existingWithUnauthorizedRole = await Get(
            $"/api/v1/estudiantes/cedula/{existingCedula}", "Estudiante");
        using var missingWithUnauthorizedRole = await Get(
            $"/api/v1/estudiantes/cedula/{NuevaCedula()}", "Estudiante");
        Assert.Equal(HttpStatusCode.NotFound, existingWithUnauthorizedRole.StatusCode);
        Assert.Equal(existingWithUnauthorizedRole.StatusCode, missingWithUnauthorizedRole.StatusCode);
        Assert.Equal(
            WithoutTraceId(await existingWithUnauthorizedRole.Content.ReadAsStringAsync()),
            WithoutTraceId(await missingWithUnauthorizedRole.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Busca_por_cedula_sin_token_devuelve401()
    {
        using var response = await Get($"/api/v1/estudiantes/cedula/{NuevaCedula()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task CrearEstudiante(string cedula)
    {
        using var response = await Send(HttpMethod.Post, "/api/v1/estudiantes", new
        {
            cedula, tipoIdentificacion = 0, nombres = "Ana", apellidos = "Paz", tipo = 1
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object ExternalStudentImport(
        string identification, string code, string subject, bool includeSecond = true)
    {
        var subjects = new List<object>
        {
            new
            {
                asignaturaId = (Guid?)null,
                codigo = code,
                nombre = subject,
                creditos = 3m,
                horas = 48,
                nota = 9m,
                periodo = "2026-A"
            }
        };
        if (includeSecond)
        {
            subjects.Add(new
            {
                asignaturaId = (Guid?)null,
                codigo = "ENG1",
                nombre = "Inglés",
                creditos = 2m,
                horas = 32,
                nota = 8m,
                periodo = "2026-A"
            });
        }

        return new
        {
            cedula = identification,
            tipoIdentificacion = 1,
            nombres = "Ana",
            apellidos = "Paz",
            tipo = 1,
            historial = new
            {
                carreraOrigenId = (Guid?)null,
                carreraOrigenNombre = "Ingeniería",
                institucionOrigen = "Universidad de origen",
                nivelActual = 3,
                asignaturas = subjects
            }
        };
    }

    private static string WithoutTraceId(string response)
    {
        using var json = JsonDocument.Parse(response);
        var properties = json.RootElement.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        return JsonSerializer.Serialize(properties);
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

    private async Task<HttpResponseMessage> Get(string path, string? role = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (role is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", StudentsApiFactory.Token(role));
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
