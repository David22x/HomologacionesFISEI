using System.Net.Http.Headers;
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

public sealed class DecisionesTests
{
    [Fact]
    public async Task RevisionYResolucion_exigenJustificacion_y_bloqueanCambios()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>()
            .UseSqlite(conexion).Options);
        await db.Database.EnsureCreatedAsync();
        var expediente = new Expediente { ProfesorId = Guid.NewGuid() };
        db.Expedientes.Add(expediente);
        await db.SaveChangesAsync();
        var destino = Guid.NewGuid();
        var origen = Guid.NewGuid();
        var analisis = new
        {
            propuestas = new[] { new { asignaturaDestinoId = destino, nombreDestino = "Materia",
                origenes = new[] { new { asignaturaCursadaId = origen, nombre = "Origen" } },
                evaluacion = new { reglas = new { resultado = 1 } } } },
            advertencias = Array.Empty<string>()
        };
        db.EjecucionesReglas.Add(new EjecucionReglas { ExpedienteId = expediente.Id, EsAnalisisIa = true,
            ProfesorId = expediente.ProfesorId, ResultadosJson = JsonSerializer.Serialize(analisis) });
        await db.SaveChangesAsync();
        var controller = new ParesExpedienteController(db, new DatosEvaluacionClient(new SinRed()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, expediente.ProfesorId.ToString()), new(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        controller.Request.Headers.Authorization = "Bearer prueba";

        Assert.IsType<ConflictObjectResult>(await controller.Resolver(expediente.Id, default));
        var solicitud = new ParesExpedienteController.RevisionPropuestaIaDto(destino, [origen], true, null, null);
        Assert.IsType<BadRequestObjectResult>(await controller.RevisarPropuestaIa(expediente.Id, solicitud, default));
        Assert.Empty(await db.Decisiones.ToListAsync());
        Assert.IsType<OkObjectResult>(await controller.RevisarPropuestaIa(expediente.Id,
            solicitud with { Observacion = "Los créditos son insuficientes." }, default));
        var decision = Assert.Single(await db.Decisiones.ToListAsync());
        Assert.Equal(1, decision.ResultadoCalculado);
        Assert.Equal(1, decision.ResultadoFinal);
        Assert.Equal(EstadoExpediente.EnRevision, expediente.Estado);
        Assert.IsType<BadRequestObjectResult>(await controller.RevisarPropuestaIa(expediente.Id,
            solicitud with { AceptarPropuestaIa = false, ResultadoPropio = 0 }, default));
        Assert.IsType<OkObjectResult>(await controller.RevisarPropuestaIa(expediente.Id,
            solicitud with { AceptarPropuestaIa = false, ResultadoPropio = 3,
                Observacion = "Falta el sílabo para decidir." }, default));
        Assert.IsType<ConflictObjectResult>(await controller.Resolver(expediente.Id, default));
        Assert.IsType<OkObjectResult>(await controller.RevisarPropuestaIa(expediente.Id,
            solicitud with { Observacion = "Se confirma el rechazo por créditos." }, default));
        Assert.IsType<OkObjectResult>(await controller.Resolver(expediente.Id, default));
        Assert.Equal(EstadoExpediente.Resuelto, expediente.Estado);
        var auditoria = await db.EventosAuditoria.ToListAsync();
        Assert.Contains(auditoria, x => x.CambiosJson.Contains("Decision"));
        Assert.Contains(auditoria, x => x.CambiosJson.Contains("Estado"));
        Assert.All(auditoria, x => Assert.Equal(expediente.ProfesorId, x.ActorId));
        var cantidad = auditoria.Count;
        Assert.IsType<ConflictObjectResult>(await controller.GuardarPares(expediente.Id, [], default));
        Assert.IsType<ConflictObjectResult>(await controller.AnalizarIa(expediente.Id, default));
        Assert.IsType<ConflictObjectResult>(await controller.Evaluar(expediente.Id, default));
        Assert.IsType<ConflictObjectResult>(await controller.RevisarPropuestaIa(expediente.Id, solicitud, default));
        Assert.IsType<OkObjectResult>(await controller.ConsultarDecisiones(expediente.Id, default));
        Assert.IsType<OkObjectResult>(await controller.ConsultarAnalisis(expediente.Id, default));
        Assert.Equal(cantidad, await db.EventosAuditoria.CountAsync());

        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        Assert.IsType<NotFoundResult>(await controller.ConsultarAuditoria(expediente.Id, default));
    }

    [Fact]
    public async Task ConsultarAnalisis_incluyePrecedentesDeExpedientesResueltos()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>()
            .UseSqlite(conexion).Options);
        await db.Database.EnsureCreatedAsync();
        var profesor = Guid.NewGuid();
        var anterior = new Expediente { ProfesorId = profesor, Estado = EstadoExpediente.Resuelto };
        var actual = new Expediente { ProfesorId = profesor };
        db.Expedientes.AddRange(anterior, actual);
        await db.SaveChangesAsync();
        var destino = Guid.NewGuid();
        var catalogo = Guid.NewGuid();
        db.Decisiones.Add(new Decision
        {
            ExpedienteId = anterior.Id,
            AsignaturaDestinoId = destino,
            OrigenIdsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }),
            OrigenCatalogoIdsJson = JsonSerializer.Serialize(new[] { catalogo }),
            ResultadoCalculado = 0,
            ResultadoFinal = 3,
            Justificacion = "Faltan contenidos de programacion.",
            ProfesorId = profesor
        });
        var analisis = new
        {
            propuestas = new[] { new { asignaturaDestinoId = destino, nombreDestino = "Materia",
                origenes = new[] { new { asignaturaCursadaId = Guid.NewGuid(), nombre = "Origen", asignaturaId = catalogo } },
                evaluacion = new { reglas = new { resultado = 0 } } } },
            advertencias = Array.Empty<string>()
        };
        db.EjecucionesReglas.Add(new EjecucionReglas { ExpedienteId = actual.Id, EsAnalisisIa = true,
            ProfesorId = profesor, ResultadosJson = JsonSerializer.Serialize(analisis) });
        await db.SaveChangesAsync();
        var controller = new ParesExpedienteController(db, new DatosEvaluacionClient(new SinRed()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, profesor.ToString()), new(ClaimTypes.Role, AppRoles.Profesor)], "test"));

        var respuesta = Assert.IsType<OkObjectResult>(await controller.ConsultarAnalisis(actual.Id, default));
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("Faltan contenidos de programacion.", json);
    }

    private sealed class SinRed : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No se esperaba una consulta de red.");
    }
}
