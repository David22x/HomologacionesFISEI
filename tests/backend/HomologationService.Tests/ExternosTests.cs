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

public sealed class ExternosTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Externos_usan_contenidos_propios_o_solicitan_temas_y_subtemas(int faltante)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new HomologationDbContext(new DbContextOptionsBuilder<HomologationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var expediente = new Expediente { ProfesorId = Guid.NewGuid() }; db.Add(expediente); await db.SaveChangesAsync();
        using var source = new Source(faltante);
        var controller = new ParesExpedienteController(db, new DatosEvaluacionClient(source)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, expediente.ProfesorId.ToString()), new Claim(ClaimTypes.Role, AppRoles.Profesor)], "test"));
        controller.Request.Headers.Authorization = "Bearer test";
        var response = await controller.AnalizarIa(expediente.Id, default);
        if (faltante == 0) {
            Assert.IsType<OkObjectResult>(response);
            Assert.NotNull(source.Sent);
            Assert.Contains("Declaracion", source.Sent);
            Assert.Contains("externo:", source.Sent);
            Assert.Single(await db.EjecucionesReglas.ToListAsync());
        } else {
            Assert.Equal(422, Assert.IsType<ObjectResult>(response).StatusCode);
            Assert.Null(source.Sent); Assert.Empty(await db.EjecucionesReglas.ToListAsync());
            Assert.Equal(EstadoExpediente.Borrador, expediente.Estado);
        }
    }
    private sealed class Source(int faltante) : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Guid destino = Guid.NewGuid(); private readonly Guid tema = Guid.NewGuid();
        public string? Sent { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://test") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/historial")) return Ok(new { esExterno = true, institucionOrigen = "Externa", carreraOrigenNombre = "Informatica",
                asignaturas = new[] { new { id = Guid.NewGuid(), asignaturaId = (Guid?)null, nombre = "Programacion", creditos = 4, horas = 64, nota = 9,
                    temas = faltante == 1 ? Array.Empty<object>() : new object[] { new { nombre = "Variables", subtemas = faltante == 2 ? Array.Empty<string>() : new[] { "Declaracion" } } } } } });
            if (path.EndsWith("/asignaturas")) return Ok(new[] { new { id = destino, nombre = "Programacion", creditos = 4, horas = 64 } });
            if (path.EndsWith("/temas")) return Ok(new[] { new { id = tema, nombre = "Variables", horasEstimadas = 8 } });
            if (path.EndsWith("/subtemas")) return Ok(new[] { new { nombre = "Declaracion" } });
            Assert.Equal(HttpMethod.Post, request.Method);
            Sent = await request.Content!.ReadAsStringAsync(ct);
            return Ok(new { reglas = new { resultado = 0, razonCreditos = 1, coberturaContenidos = 1, razonHoras = 1, versionReglas = "1", advertencias = Array.Empty<string>() },
                cobertura = new { porcentaje = 1, temasCoincidentes = Array.Empty<object>(), temasAusentes = Array.Empty<object>() } });
        }
        private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
