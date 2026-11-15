using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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

public sealed class ParesExpedienteTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrecargaYEvaluacion_guardanSoloEjecucionesCompletas(bool agenteDisponible)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(connection).Options;
        await using var db = new HomologationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var expediente = new Expediente
        {
            ProfesorId = Guid.NewGuid(),
            EstudianteId = Guid.NewGuid(),
            CarreraDestinoId = Guid.NewGuid(),
            MallaDestinoId = Guid.NewGuid()
        };
        db.Expedientes.Add(expediente);
        await db.SaveChangesAsync();

        var servicios = new ServiciosFalsos(agenteDisponible);
        var controller = new ParesExpedienteController(db, new DatosEvaluacionClient(servicios))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, expediente.ProfesorId.ToString()), new(ClaimTypes.Role, AppRoles.Profesor)],
            "Prueba"));
        controller.Request.Headers.Authorization = "Bearer token";

        Assert.IsType<OkObjectResult>(await controller.Precargar(expediente.Id, default));
        Assert.IsType<OkObjectResult>(await controller.Precargar(expediente.Id, default));
        var par = Assert.Single(await db.Pares.ToListAsync());
        Assert.True(par.Precargado);
        Assert.Equal(servicios.OrigenId, par.AsignaturaCursadaId);

        var respuesta = await controller.Evaluar(expediente.Id, default);
        await db.Entry(expediente).ReloadAsync();
        if (agenteDisponible)
        {
            Assert.IsType<OkObjectResult>(respuesta);
            Assert.Equal(EstadoExpediente.EnRevision, expediente.Estado);
            var ejecucion = Assert.Single(await db.EjecucionesReglas.ToListAsync());
            Assert.Contains(par.Id.ToString(), ejecucion.ResultadosJson);
        }
        else
        {
            Assert.IsType<ObjectResult>(respuesta);
            Assert.Equal(EstadoExpediente.Borrador, expediente.Estado);
            Assert.Empty(await db.EjecucionesReglas.ToListAsync());
        }
    }

    private sealed class ServiciosFalsos(bool agenteDisponible) : HttpMessageHandler, IHttpClientFactory
    {
        public Guid OrigenId { get; } = Guid.NewGuid();
        private readonly Guid _destinoId = Guid.NewGuid();

        public HttpClient CreateClient(string name) =>
            new(this, disposeHandler: false) { BaseAddress = new Uri($"http://localhost/{name}") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/historial"))
            {
                return Ok(new
                {
                    asignaturas = new[]
                    {
                        new { id = OrigenId, nombre = " programación ", nota = 8m, creditos = 3, horas = 64 },
                        new { id = Guid.NewGuid(), nombre = "Programación aplicada", nota = 9m, creditos = 4, horas = 64 }
                    }
                });
            }

            if (path.EndsWith("/asignaturas"))
                return Ok(new[] { new { id = _destinoId, nombre = "Programación", creditos = 3, horas = 64 } });

            if (path.EndsWith("/temas"))
                return Ok(Array.Empty<object>());

            Assert.Equal("/api/v1/agent-ia/evaluar-reglas", path);
            var cuerpo = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("\"creditosOrigen\":3", cuerpo);
            Assert.Contains("\"notaOrigen\":8", cuerpo);
            return agenteDisponible
                ? Ok(new { reglas = new { resultado = 3, versionReglas = "1.0.0" } })
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        private static HttpResponseMessage Ok(object value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
