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

public sealed class SeleccionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Seleccion_persiste_valida_conflictos_y_confirma_desde_ambas_fuentes(bool usarIa)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var exp = new Expediente { ProfesorId = Guid.NewGuid() }; db.Add(exp); await db.SaveChangesAsync();
        var origen = Guid.NewGuid(); var destino = Guid.NewGuid(); var otroDestino = Guid.NewGuid();
        object Evaluation() => new { reglas = new { resultado = 0, razonCreditos = 1m, coberturaContenidos = .9m,
            razonHoras = 1m, versionReglas = "1", advertencias = Array.Empty<string>() },
            cobertura = new { porcentaje = .9m, temasCoincidentes = Array.Empty<object>(), temasAusentes = Array.Empty<object>() } };
        object Proposal(Guid id) => new { asignaturaDestinoId = id, nombreDestino = "Física",
            origenes = new[] { new { asignaturaCursadaId = origen, nombre = "Física" } }, evaluacion = Evaluation() };
        var source = usarIa
            ? JsonSerializer.Serialize(new { propuestas = new[] { Proposal(destino), Proposal(otroDestino) }, advertencias = Array.Empty<string>() })
            : JsonSerializer.Serialize(new[] { new { parId = Guid.NewGuid(), asignaturaCursadaId = origen, nombreOrigen = "Física", asignaturaDestinoId = destino, nombreDestino = "Física", evaluacion = Evaluation() } });
        db.Add(new EjecucionReglas { ExpedienteId = exp.Id, ProfesorId = exp.ProfesorId, EsAnalisisIa = usarIa, ResultadosJson = source }); await db.SaveChangesAsync();
        var controller = new ParesExpedienteController(db, new DatosEvaluacionClient(new SinRed()))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, exp.ProfesorId.ToString()), new Claim(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        controller.Request.Headers.Authorization = "Bearer test";
        var seleccion = new ParesExpedienteController.SeleccionDto(destino, [origen]);
        Assert.IsType<OkObjectResult>(await controller.GuardarSeleccion(exp.Id, [seleccion], default));
        Assert.Contains(origen.ToString(), exp.SeleccionJson);
        Assert.IsType<BadRequestObjectResult>(await controller.GuardarSeleccion(exp.Id, [seleccion, seleccion], default));
        Assert.IsType<ConflictObjectResult>(await controller.GuardarSeleccion(exp.Id, [seleccion, new(otroDestino, [origen])], default));
        Assert.IsType<ConflictObjectResult>(await controller.GuardarSeleccion(exp.Id, [new(destino, [Guid.NewGuid()])], default));
        Assert.IsType<OkObjectResult>(await controller.RevisarPropuestaIa(exp.Id, new(destino, [origen], true, null, null), default));
        Assert.IsType<ConflictObjectResult>(await controller.GuardarSeleccion(exp.Id, [], default));
        Assert.IsType<OkObjectResult>(await controller.ConsultarSeleccion(exp.Id, default));
        Assert.Contains(await db.EventosAuditoria.ToListAsync(), e => e.CambiosJson.Contains("SeleccionJson"));
        Assert.Equal(0, Assert.Single(await db.Decisiones.ToListAsync()).ResultadoFinal);
        exp.Estado = EstadoExpediente.Resuelto; await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await controller.GuardarSeleccion(exp.Id, [], default));
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        Assert.IsType<NotFoundResult>(await controller.ConsultarSeleccion(exp.Id, default));
    }

    private sealed class SinRed : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No se requiere red para seleccionar evaluaciones guardadas.");
    }
}
