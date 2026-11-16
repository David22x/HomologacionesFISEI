using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Roles;
using HomologationService.Controllers;
using HomologationService.Data;
using HomologationService.Models;
using HomologationService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomologationService.Tests;

public sealed class AnalisisIaExpedienteTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Analizar_generaPropuestasYReutilizaLaEjecucion(bool fallaPrimero, bool sinHistorial)
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(conexion).Options;
        await using var db = new HomologationDbContext(opciones);
        await db.Database.EnsureCreatedAsync();
        var expediente = new Expediente { ProfesorId = Guid.NewGuid() };
        db.Expedientes.Add(expediente);
        await db.SaveChangesAsync();

        using var servicios = new ServiciosAnalisis(sinHistorial) { FallarEn = fallaPrimero ? 4 : null };
        var controlador = new ParesExpedienteController(db, new DatosEvaluacionClient(servicios))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controlador.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, expediente.ProfesorId.ToString()), new(ClaimTypes.Role, AppRoles.Profesor)],
            "Prueba"));
        controlador.Request.Headers.Authorization = "Bearer prueba";

        if (fallaPrimero)
        {
            var error = Assert.IsType<ObjectResult>(await controlador.AnalizarIa(expediente.Id, default));
            Assert.Equal(503, error.StatusCode);
            Assert.Empty(await db.EjecucionesReglas.ToListAsync());
            Assert.Equal(EstadoExpediente.Borrador, expediente.Estado);
        }

        var respuesta = Assert.IsType<OkObjectResult>(await controlador.AnalizarIa(expediente.Id, default));
        var json = JsonSerializer.SerializeToElement(respuesta.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("AnalizadoConIa", json.GetProperty("estado").GetString());
        var propuestas = json.GetProperty("propuestas").EnumerateArray().ToArray();
        if (sinHistorial)
        {
            Assert.Empty(propuestas);
            Assert.NotEmpty(json.GetProperty("advertencias").EnumerateArray());
        }
        else
        {
            Assert.Equal(6, propuestas.Length);
            Assert.Equal(3, propuestas.Count(x => x.GetProperty("origenes").GetArrayLength() == 1));
            Assert.Equal(3, propuestas.Count(x => x.GetProperty("origenes").GetArrayLength() == 2));
            Assert.DoesNotContain(propuestas, x => x.GetProperty("origenes").EnumerateArray()
                .Any(y => y.GetProperty("nombre").GetString() == "Historia"));
            Assert.Contains(propuestas, x => Resultado(x) == ResultadoHomologacion.RechazoPorCreditos);
            Assert.Contains(propuestas, x => Resultado(x) == ResultadoHomologacion.DatosIncompletos);
            var viable = Assert.Single(propuestas, x => Resultado(x) == ResultadoHomologacion.Homologable);
            Assert.Equal(2, viable.GetProperty("origenes").GetArrayLength());
            Assert.Equal(1m, viable.GetProperty("evaluacion").GetProperty("reglas")
                .GetProperty("coberturaContenidos").GetDecimal());
        }

        var ejecucion = Assert.Single(await db.EjecucionesReglas.ToListAsync());
        Assert.True(ejecucion.EsAnalisisIa);
        Assert.Equal(EstadoExpediente.AnalizadoConIa, expediente.Estado);
        var llamadas = servicios.Llamadas;
        var repetida = Assert.IsType<OkObjectResult>(await controlador.AnalizarIa(expediente.Id, default));
        Assert.Equal(JsonSerializer.Serialize(respuesta.Value), JsonSerializer.Serialize(repetida.Value));
        Assert.Equal(llamadas, servicios.Llamadas);
        Assert.Equal(1, await db.EjecucionesReglas.CountAsync());
    }

    private static ResultadoHomologacion Resultado(JsonElement propuesta) =>
        (ResultadoHomologacion)propuesta.GetProperty("evaluacion").GetProperty("reglas").GetProperty("resultado").GetInt32();

    private sealed class ServiciosAnalisis(bool sinHistorial) : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Guid _destino = Guid.NewGuid();
        private readonly Guid _origenA = Guid.NewGuid();
        private readonly Guid _origenB = Guid.NewGuid();
        private readonly IAgenteIaService _agente = new AgenteIaService(
            new MotorReglasHomologacion(new ParametrosReglas()), new ComparadorDePrueba());
        public int? FallarEn { get; init; }
        public int Llamadas { get; private set; }

        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://localhost/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("prueba", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/historial"))
                return sinHistorial ? new(HttpStatusCode.NotFound) : Ok(new
                {
                    asignaturas = new[]
                    {
                        new { id = Guid.NewGuid(), asignaturaId = (Guid?)_origenA, nombre = "Programación I", creditos = 2, horas = 32, nota = 8 },
                        new { id = Guid.NewGuid(), asignaturaId = (Guid?)_origenB, nombre = "Programación II", creditos = 2, horas = 32, nota = 8 },
                        new { id = Guid.NewGuid(), asignaturaId = (Guid?)null, nombre = "Taller de Programación", creditos = 4, horas = 64, nota = 8 },
                        new { id = Guid.NewGuid(), asignaturaId = (Guid?)null, nombre = "Historia", creditos = 4, horas = 64, nota = 8 }
                    }
                });
            if (path.EndsWith("/asignaturas"))
                return Ok(new[] { new { id = _destino, nombre = "Programación", creditos = 4, horas = 64 } });
            if (path.EndsWith("/subtemas"))
                return Ok(Array.Empty<object>());
            if (path.EndsWith("/temas"))
            {
                var id = Guid.Parse(path.Split('/')[^2]);
                var nombres = id == _destino ? new[] { "Variables", "Ciclos" }
                    : new[] { id == _origenA ? "Variables" : "Ciclos" };
                return Ok(nombres.Select(nombre => new { id = Guid.NewGuid(), nombre, horasEstimadas = 4 }));
            }
            if (request.Method == HttpMethod.Get)
                return Ok(new { id = Guid.Parse(path.Split('/')[^1]), nombre = "Programación", creditos = 2, horas = 32 });

            Assert.Equal("/api/v1/agent-ia/evaluar-reglas", path);
            if (++Llamadas == FallarEn)
                return new(HttpStatusCode.ServiceUnavailable);

            var solicitud = await request.Content!.ReadFromJsonAsync<SolicitudEvaluacionIa>(cancellationToken: ct);
            return Ok(await _agente.EvaluarAsync(solicitud!, ct));
        }

        private static HttpResponseMessage Ok(object value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    private sealed class ComparadorDePrueba : IComparadorContenidosIa
    {
        public Task<IReadOnlyList<CoincidenciaIa>> CompararAsync(
            IReadOnlyList<ContenidoOrigenIa> origen, IReadOnlyList<ContenidoDestinoIa> destino, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoincidenciaIa>>(destino
                .Where(x => origen.Any(y => y.Descripcion == x.Descripcion))
                .Select(x => new CoincidenciaIa(x.Codigo, 1m, x.Descripcion))
                .ToArray());
    }
}
