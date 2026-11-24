using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentsService.Controllers;
using DocumentsService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DocumentsService.Tests;

public sealed class RecordImportTests
{
    [Fact]
    public void Lee_todos_los_periodos_y_carreras_y_no_selecciona_reprobadas()
    {
        var pdf = Pdf(["CARRERA DE Software", "documento de identidad numero: 0102030400", "periodo AGO/25-ENE/26", "ASIGNATURA PROMEDIO ESTADO", "PROGRAMACION 8.8/10 APROBADO", "CALCULO 5.0/10 REPROBADO"],
            ["CARRERA DE Tecnologias de la Informacion", "periodo FEB/26-JUN/26", "ASIGNATURA PROMEDIO ESTADO", "REDES 9.1/10 APROBADO"]);
        var preview = RecordAcademicoReader.Leer(pdf);
        Assert.Equal("0102030400", preview.Identificacion);
        Assert.Equal(3, preview.Filas.Count);
        Assert.Equal(2, preview.Filas.Select(x => x.Carrera).Distinct().Count());
        Assert.Equal(2, preview.Filas.Select(x => x.Periodo).Distinct().Count());
        Assert.False(Assert.Single(preview.Filas, x => x.Nota == 5m).Seleccionado);
    }

    [Fact]
    public void Pdf_corrupto_no_produce_notas_inventadas() => Assert.Throws<InvalidDataException>(() => RecordAcademicoReader.Leer([1, 2, 3]));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmar_valida_identidad_y_guarda_sin_borrar_historial(bool identidadIncorrecta)
    {
        using var handler = new Source();
        var controller = new RecordsAcademicosController(handler) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers.Authorization = "Bearer prueba";
        var bytes = Pdf(["CARRERA DE Software", "documento de identidad numero: 0102030400", "periodo AGO/25-ENE/26", "ASIGNATURA PROMEDIO ESTADO", "PROGRAMACION 8.8/10 APROBADO"]);
        var original = RecordAcademicoReader.Leer(bytes);
        var request = new RecordsAcademicosController.Confirmacion(Guid.NewGuid(), identidadIncorrecta ? "otro" : original.Identificacion,
            [original.Filas[0] with { AsignaturaId = Source.Subject }]);
        using var stream = new MemoryStream(bytes);
        var response = await controller.Confirmar(new FormFile(stream, 0, bytes.Length, "archivo", "record.pdf"), JsonSerializer.Serialize(request), default);
        if (identidadIncorrecta) { Assert.IsType<ConflictObjectResult>(response); Assert.Null(handler.Saved); }
        else {
            Assert.Equal(200, Assert.IsType<ContentResult>(response).StatusCode);
            var history = JsonSerializer.Deserialize<JsonElement>(handler.Saved!);
            Assert.Equal(2, history.GetProperty("asignaturas").GetArrayLength());
            Assert.Contains(history.GetProperty("asignaturas").EnumerateArray(), a => a.GetProperty("nombre").GetString() == "EXISTENTE");
        }
    }

    private sealed class Source : HttpMessageHandler, IHttpClientFactory
    {
        public static readonly Guid Subject = Guid.NewGuid();
        public string? Saved { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://test") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("Bearer prueba", request.Headers.Authorization!.ToString());
            if (request.Method == HttpMethod.Put) { Saved = await request.Content!.ReadAsStringAsync(ct); return Ok(new { actualizado = true }); }
            if (request.RequestUri!.AbsolutePath.Contains("catalogo")) return Ok(new { id = Subject, codigo = "PROG", nombre = "PROGRAMACION", creditos = 4, horas = 64 });
            return Ok(new { cedula = "0102030400", tipo = 0, eliminado = false, historial = new { carreraOrigenId = Guid.NewGuid(), nivelActual = 3,
                asignaturas = new[] { new { asignaturaId = Guid.NewGuid(), codigo = "OLD", nombre = "EXISTENTE", creditos = 3, horas = 48, nota = 9, periodo = "2025-A" } } } });
        }
        private static HttpResponseMessage Ok(object data) => new(HttpStatusCode.OK) { Content = JsonContent.Create(data) };
    }

    internal static byte[] Pdf(params string[][] pages)
    {
        var objects = new List<string> { "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", "", "<< /Type /Catalog /Pages 2 0 R >>" };
        var kids = new List<int>();
        foreach (var lines in pages)
        {
            var content = string.Join("\n", lines.Select((line, i) => $"BT /F1 10 Tf 1 0 0 1 35 {790 - i * 22} Tm ({line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")}) Tj ET"));
            var stream = objects.Count + 1;
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            kids.Add(objects.Count + 1);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 1 0 R >> >> /Contents {stream} 0 R >>");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(' ', kids.Select(i => $"{i} 0 R"))}] >>";
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++) { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = pdf.Length; pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 3 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
