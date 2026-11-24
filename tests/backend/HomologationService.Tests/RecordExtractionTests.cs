using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.AcademicImports;
using HomologationService.Services;
using HomologationService.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HomologationService.Tests;

public sealed class RecordExtractionTests
{
    [Theory]
    [InlineData("Escala máxima: 100", 85, 100, 8.5)]
    [InlineData("Escala: 20", 17, 20, 8.5)]
    public void Escala_explicita_se_normaliza_y_se_conserva_nota_original(string escala, int nota, int max, double esperada)
    {
        var evidence = $"Programming {nota} Passed";
        var result = ExtractorRecordIa.Validar(new("AB12345", [new("Programming", "Computing", "2026-A", null, "APROBADO", 1,
            Evidencia: evidence, NotaOriginal: nota, EscalaOriginal: max)], [], new PerfilRecord(EscalaMaxima: max)), $"Student AB12345. {escala}. {evidence}");
        var row = Assert.Single(result.Filas);
        Assert.Equal((decimal)esperada, row.Nota); Assert.Equal(nota, row.NotaOriginal); Assert.True(row.Seleccionado);
    }

    [Fact]
    public void Escala_no_declarada_no_se_inventa_y_la_fila_queda_para_revision()
    {
        var result = ExtractorRecordIa.Validar(new("", [new("Programming", "", "", 9, "APROBADO", 1,
            Evidencia: "Programming 9 Passed", NotaOriginal: 9, EscalaOriginal: 10)], [], new PerfilRecord(EscalaMaxima: 10)), "Programming 9 Passed");
        var row = Assert.Single(result.Filas); Assert.Null(row.Nota); Assert.Null(row.EscalaOriginal); Assert.False(row.Seleccionado);
    }

    [Fact]
    public void Descarta_filas_inventadas_y_no_confirma_notas_o_estados_sin_evidencia()
    {
        var result = ExtractorRecordIa.Validar(new("IDINVENTADO", [new("Fake", "", "", 9, "APROBADO", 1, Evidencia: "Fake 9 Passed"),
            new("Programming", "", "", 9, "APROBADO", 1, Evidencia: "Programming 7", NotaOriginal: 9, EscalaOriginal: 10)], [], new PerfilRecord("Inventado")), "Escala: 10. Programming 7");
        var row = Assert.Single(result.Filas); Assert.Null(row.Nota); Assert.Equal("PENDIENTE", row.Estado); Assert.False(row.Seleccionado);
        Assert.Empty(result.Identificacion); Assert.Empty(result.Perfil!.Nombres);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extraccion_usa_una_sola_peticion_estructurada_o_devuelve_servicio_no_disponible(bool fallo)
    {
        using var source = new Ollama(fallo); using var http = new HttpClient(source) { BaseAddress = new Uri("http://ollama/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ollama:LanguageModel"] = "modelo-prueba" }).Build();
        var extractor = new ExtractorRecordIa(http, config); var controller = new RecordsController(extractor);
        var result = await controller.Extraer(new("Escala: 100. Programming 85 Passed"), default);
        Assert.Equal(1, source.Requests);
        if (fallo) Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
        else { var value = Assert.IsType<VistaRecord>(Assert.IsType<OkObjectResult>(result).Value); Assert.Equal(8.5m, Assert.Single(value.Filas).Nota); }
    }
    private sealed class Ollama(bool fallo) : HttpMessageHandler {
        public int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Requests++; Assert.Equal("/api/chat", request.RequestUri!.AbsolutePath);
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("modelo-prueba", body.GetProperty("model").GetString()); Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.Equal(0, body.GetProperty("options").GetProperty("temperature").GetInt32());
            if (fallo) throw new HttpRequestException("Ollama apagado");
            var content = JsonSerializer.Serialize(new VistaRecord("", [new("Programming", "", "2026", null, "APROBADO", 1,
                Evidencia: "Programming 85 Passed", NotaOriginal: 85, EscalaOriginal: 100)], [], new PerfilRecord(EscalaMaxima: 100)));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { message = new { content } }) };
        }
    }
}
