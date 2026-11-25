using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using ReportsService.Data;
using UglyToad.PdfPig;

namespace ReportsService.Tests;

public sealed class InformesTests
{
    [Fact]
    public async Task CaidaDeBaseAlGuardar_noSeConfundeConGeneracionConcurrente()
    {
        using var factory = new ReportsFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        factory.SaveFailure = new DbUpdateException("Fallo simulado", new TransientDatabaseFailure());
        using var response = await client.PostAsync($"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}/generar", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("concurrentemente", await response.Content.ReadAsStringAsync());
        factory.SaveFailure = null;
        using var recovered = await client.PostAsync($"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}/generar", null);
        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
    }

    private sealed class TransientDatabaseFailure : System.Data.Common.DbException
    {
        public override bool IsTransient => true;
    }

    [Fact]
    public async Task Generar_descargarYVersionar_conservaBytesYHuella()
    {
        using var factory = new ReportsFactory();
        factory.Fuente.CantidadTemas = 45;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        using var generado = await client.PostAsync(ruta + "/generar", null);
        Assert.Equal(HttpStatusCode.Created, generado.StatusCode);
        var version1 = await generado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, version1.GetProperty("version").GetInt32());
        using var descarga = await client.GetAsync(ruta + "/1/pdf");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType!.MediaType);
        var pdf = await descarga.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        var huella = Convert.ToHexString(SHA256.HashData(pdf));
        Assert.Equal(huella, version1.GetProperty("huellaSha256").GetString());
        Assert.Equal(huella, Assert.Single(descarga.Headers.GetValues("X-Content-SHA256")));
        using var repetido = await client.PostAsync(ruta + "/generar", null);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Equal(version1.GetProperty("id").GetGuid(),
            (await repetido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        factory.Fuente.Nombre = "Ana corregida";
        using var nueva = await client.PostAsync(ruta + "/generar", null);
        Assert.Equal(HttpStatusCode.Created, nueva.StatusCode);
        Assert.Equal(2, (await nueva.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32());
        Assert.Equal(pdf, await client.GetByteArrayAsync(ruta + "/1/pdf"));
        var versiones = await client.GetFromJsonAsync<JsonElement>(ruta);
        Assert.Equal(2, versiones.GetArrayLength());

    }

    private static async Task<string> GenerarTextoAsync(ReportsFactory factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        using var documento = PdfDocument.Open(await client.GetByteArrayAsync(ruta + "/1/pdf"));
        return string.Join(" ", documento.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
    }

    [Fact]
    public async Task Pdf_incluyeLosDatosYLasDecisionesGuardadas()
    {
        using var factory = new ReportsFactory();
        var texto = await GenerarTextoAsync(factory);
        Assert.Contains("Paz, Ana", texto);
        Assert.Contains("DEMO-001", texto);
        Assert.Contains("Carrera de origen: Ingeniería en Sistemas (Universidad Externa)", texto);
        Assert.Contains("Carrera de destino: Ingeniería de Software · Malla 2026", texto);
        Assert.Contains("Programación", texto);
        Assert.Contains("Origen: Fundamentos de programación", texto);
        Assert.Contains("Resultado calculado: Rechazo por créditos", texto);
        Assert.Contains("Decisión docente: Homologable", texto);
        Assert.Contains("Justificación: Se acepta por revisión académica documentada.", texto);
        Assert.Contains("Docente de prueba", texto);
        Assert.Contains("Asignaturas homologadas (1)", texto);
        Assert.Contains("Asignaturas no homologadas por decisión del profesor (0)", texto);
        Assert.Contains("Observaciones del profesor", texto);
        Assert.Contains("Firma del profesor responsable", texto);
        Assert.Contains("Firma del estudiante (acepta el informe)", texto);
        Assert.Contains("Tipo de homologación: Interna", texto);
        Assert.Contains("Total de asignaturas homologadas: 1 de 1", texto);
        Assert.Contains("Observaciones: Sin observaciones.", texto);
        Assert.Contains("70% 80% Profesor", texto);
    }

    [Fact]
    public async Task Pdf_noMuestraCodigosInternosYUsaHoraDeEcuador()
    {
        using var factory = new ReportsFactory();
        var texto = await GenerarTextoAsync(factory);
        var expediente = factory.Fuente.ExpedienteId.ToString();
        Assert.Contains("Expediente " + expediente[..8], texto);
        Assert.DoesNotContain(expediente, texto);
        Assert.DoesNotContain("Evidencia:", texto);
        Assert.DoesNotContain("UTC", texto);
        Assert.Contains("Decisión registrada 22/11/2026 05:00 (hora de Ecuador)", texto);
        Assert.Contains("Temas del destino cubiertos: 1 de 1", texto);
    }

    [Fact]
    public async Task Pdf_resumeHomologadasYRechazadasSegunLaDecision()
    {
        using var homologada = new ReportsFactory();
        var texto = await GenerarTextoAsync(homologada);
        Assert.Contains("Resumen 1 1 0 Decisiones Homologadas Rechazadas", texto);
        using var rechazada = new ReportsFactory();
        rechazada.Fuente.ResultadoFinal = 1;
        texto = await GenerarTextoAsync(rechazada);
        Assert.Contains("Asignaturas no homologadas por decisión del profesor (0)", texto);
        Assert.Contains("Otras 1 asignaturas no cumplieron los requisitos de homologación", texto);
        Assert.DoesNotContain("Motivo del rechazo", texto);
        Assert.DoesNotContain("Observaciones del profesor", texto);
        Assert.Contains("Resumen 1 0 1 Decisiones Homologadas Rechazadas", texto);
    }

    [Fact]
    public async Task Pdf_explicaPorQueElProfesorRechazoLoQueElSistemaRecomendabaHomologar()
    {
        using var factory = new ReportsFactory();
        factory.Fuente.ResultadoCalculado = 0;
        factory.Fuente.ResultadoFinal = 2;
        factory.Fuente.Justificacion = "Faltan temas de bases de datos en el sílabo de origen.";
        var texto = await GenerarTextoAsync(factory);
        Assert.Contains("Asignaturas no homologadas por decisión del profesor (1)", texto);
        Assert.Contains("Motivo del rechazo", texto);
        Assert.Contains("El sistema recomendaba homologar; el profesor la rechazó.", texto);
        Assert.Contains("Rechazo por contenidos", texto);
        Assert.Contains("Créditos: 70% (no cumple, mínimo 80%)", texto);
        Assert.Contains("Justificación del profesor: Faltan temas de bases de datos en el sílabo de origen.", texto);
        Assert.DoesNotContain("Observaciones del profesor", texto);
    }

    [Fact]
    public async Task Pdf_indicaQueLosContenidosNoAplicanEnUnaTransversalDirecta()
    {
        using var factory = new ReportsFactory();
        factory.Fuente.ResultadoCalculado = 4;
        factory.Fuente.ResultadoFinal = 4;
        var texto = await GenerarTextoAsync(factory);
        Assert.Contains("Asignaturas homologadas (1)", texto);
        Assert.Contains("70% No aplica Sistema", texto);
    }

    [Fact]
    public async Task Pdf_indicaSiCumpleLosMinimosYDeclaraLaDecisionDelProfesor()
    {
        using var factory = new ReportsFactory();
        var texto = await GenerarTextoAsync(factory);
        Assert.Contains("Créditos: 70% (no cumple, mínimo 80%)", texto);
        Assert.Contains("Contenidos: 80% (cumple, mínimo 80%)", texto);
        Assert.Contains("Horas: Sin datos", texto);
        Assert.Contains("La decisión final de homologación corresponde al profesor responsable", texto);
    }

    [Fact]
    public async Task Pdf_destacaLaNotaEsRecomendableHomologarSoloCuandoExiste()
    {
        using var sinNota = new ReportsFactory();
        Assert.DoesNotContain("Nota: Es recomendable homologar", await GenerarTextoAsync(sinNota));
        using var conNota = new ReportsFactory();
        conNota.Fuente.Justificacion = "Revisión docente. Es recomendable homologar";
        Assert.Contains("Nota: Es recomendable homologar", await GenerarTextoAsync(conNota));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Generar_noPermiteExpedientesQueNoEstanResueltos(int estado)
    {
        using var factory = new ReportsFactory();
        factory.Fuente.Estado = estado;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Informes.CountAsync());
    }

    [Fact]
    public async Task Generar_noPermiteUnExpedienteResueltoSinDecisiones()
    {
        using var factory = new ReportsFactory();
        factory.Fuente.SinDecisiones = true;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
    }

    [Fact]
    public async Task Generar_incluyeCarreraDeOrigenDelHistorial()
    {
        using var factory = new ReportsFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var informe = await scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Informes.SingleAsync();
        Assert.Contains("Ingeniería en Sistemas (Universidad Externa)",
            JsonDocument.Parse(informe.DatosJson).RootElement.GetProperty("carreraOrigen").GetString());
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(informe.Pdf, 0, 5));
    }

    [Fact]
    public async Task PermisosYEstado_noPermitenGenerarInformesAjenos()
    {
        using var factory = new ReportsFactory();
        using var client = factory.CreateClient();
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ruta)).StatusCode);
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Administrador);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        factory.Fuente.EstadoRespuesta = HttpStatusCode.NotFound;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ruta)).StatusCode);
        factory.Fuente.EstadoRespuesta = HttpStatusCode.Conflict;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Informes.CountAsync());
    }

    [Fact]
    public async Task Descarga_detectaUnPdfAlterado()
    {
        using var factory = new ReportsFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = ReportsFactory.Token(AppRoles.Profesor);
        var ruta = $"/api/v1/informes/expedientes/{factory.Fuente.ExpedienteId}";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(ruta + "/generar", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReportsDbContext>();
            (await db.Informes.SingleAsync()).Pdf = [1, 2, 3];
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync(ruta + "/1/pdf")).StatusCode);
    }
}

public sealed class ReportsFactory : WebApplicationFactory<Program>
{
    private const string Secret = "Reports-tests-secret-with-at-least-32-bytes";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public FuenteFalsa Fuente { get; } = new();
    public Exception? SaveFailure { get; set; }
    public ReportsFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ReportsConnection", "Server=localhost;Database=ReportsTests;Integrated Security=True");
        builder.UseSetting("Reports:MigrateOnStartup", "false");
        builder.UseSetting("Jwt:SecretKey", Secret);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ReportsDbContext>>();
            services.RemoveAll<ReportsDbContext>();
            services.AddDbContext<ReportsDbContext>(options => options.UseSqlite(_connection).AddInterceptors(new SimulatedSaveFailure(this)));
            services.AddSingleton<IHttpClientFactory>(Fuente);
            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<ReportsDbContext>().Database.EnsureCreated();
        });
    }

    private sealed class SimulatedSaveFailure(ReportsFactory factory) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (factory.SaveFailure is { } failure) throw failure;
            return ValueTask.FromResult(result);
        }
    }

    public static AuthenticationHeaderValue Token(string rol)
    {
        var token = new JwtSecurityToken("HomologacionesFISEI", "HomologacionesFISEIClient",
            [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, rol)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _connection.Dispose(); Fuente.Dispose(); }
    }
}

public sealed class FuenteFalsa : HttpMessageHandler, IHttpClientFactory
{
    public Guid ExpedienteId { get; } = Guid.NewGuid();
    private readonly Guid _destino = Guid.NewGuid();
    private readonly Guid _origen = Guid.NewGuid();
    private readonly Guid _estudiante = Guid.NewGuid();
    private readonly Guid _carrera = Guid.NewGuid();
    private readonly Guid _malla = Guid.NewGuid();
    public string Nombre { get; set; } = "Ana";
    public int Estado { get; set; } = 5;
    public int ResultadoFinal { get; set; }
    public int ResultadoCalculado { get; set; } = 1;
    public bool SinDecisiones { get; set; }
    public string Justificacion { get; set; } = "Se acepta por revisión académica documentada.";
    public int CantidadTemas { get; set; } = 1;
    public HttpStatusCode EstadoRespuesta { get; set; } = HttpStatusCode.OK;
    public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://localhost/") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        object resultado;
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/informe"))
        {
            if (EstadoRespuesta != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(EstadoRespuesta));
            resultado = new
            {
                expediente = new { id = ExpedienteId, estudianteId = _estudiante, carreraDestinoId = _carrera,
                    mallaDestinoId = _malla, profesorId = Guid.Empty, profesorNombre = "Docente de prueba", estado = Estado },
                decisiones = new[] { new { id = _destino, asignaturaDestinoId = _destino,
                    origenIdsJson = JsonSerializer.Serialize(new[] { _origen }), resultadoCalculado = ResultadoCalculado, resultadoFinal = ResultadoFinal,
                    justificacion = Justificacion,
                    profesorId = Guid.Empty, registradoEn = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc) } }
                    .Take(SinDecisiones ? 0 : 1).ToArray(),
                analisis = new
                {
                    propuestas = new[] { new { asignaturaDestinoId = _destino, nombreDestino = "Programación",
                        origenes = new[] { new { asignaturaCursadaId = _origen, nombre = "Fundamentos de programación" } },
                        evaluacion = new { reglas = new { razonCreditos = 0.7m, coberturaContenidos = 0.8m,
                            razonHoras = (decimal?)null, versionReglas = "1.0.0", advertencias = new[] { "Revisión docente." } },
                            explicacion = "Evaluación respaldada por el catálogo.",
                            cobertura = new { temasCoincidentes = Enumerable.Range(1, CantidadTemas)
                                .Select(i => new { descripcion = $"Tema {i}: Variables y ciclos",
                                    evidencia = "Tema presente en el sílabo de origen. Se revisó el contenido académico documentado." })
                                .ToArray(), temasAusentes = Array.Empty<object>() } } } },
                    advertencias = Array.Empty<string>()
                }
            };
        }
        else if (path.StartsWith("/api/v1/estudiantes/"))
            resultado = new { nombres = Nombre, apellidos = "Paz", cedula = "DEMO-001", tipo = 0,
                historial = new { carreraOrigenNombre = "Ingeniería en Sistemas", institucionOrigen = "Universidad Externa" } };
        else if (path.EndsWith("/mallas"))
            resultado = new[] { new { id = _malla, version = "2026" } };
        else resultado = new[] { new { id = _carrera, nombre = "Ingeniería de Software" } };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(resultado) });
    }
}
