using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BuildingBlocks.Roles;
using HomologationService.Controllers;
using HomologationService.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomologationService.Tests;

public sealed class ExpedientesOrigenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Crear_rechaza_misma_carrera_o_estudiante_eliminado(bool eliminado)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var career = Guid.NewGuid();
        using var services = new StudentSource(career, eliminado);
        var controller = new ExpedientesController(db, services)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        controller.Request.Headers.Authorization = "Bearer prueba";
        Assert.IsType<BadRequestObjectResult>(await controller.CrearExpediente(new CrearExpedienteDto
        { EstudianteRefId = Guid.NewGuid(), CarreraDestinoRefId = career, MallaAplicableRefId = Guid.NewGuid() }, default));
        Assert.Empty(await db.Expedientes.ToListAsync());
    }

    [Fact]
    public async Task Crear_permite_externo_sin_carrera_local_de_origen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var malla = Guid.NewGuid();
        using var services = new StudentSource(null, false, malla);
        var controller = new ExpedientesController(db, services)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        controller.Request.Headers.Authorization = "Bearer prueba";
        Assert.IsType<CreatedAtActionResult>(await controller.CrearExpediente(new CrearExpedienteDto
        { EstudianteRefId = Guid.NewGuid(), CarreraDestinoRefId = Guid.NewGuid(), MallaAplicableRefId = malla }, default));
        Assert.Single(await db.Expedientes.ToListAsync());
    }

    private sealed class StudentSource(Guid? career, bool eliminado, Guid? malla = null) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://test") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = request.RequestUri!.AbsolutePath.EndsWith("/mallas")
                ? JsonContent.Create(new[] { new { id = malla } })
                : JsonContent.Create(new { eliminado, historial = new { carreraOrigenId = career } }) });
    }
}
