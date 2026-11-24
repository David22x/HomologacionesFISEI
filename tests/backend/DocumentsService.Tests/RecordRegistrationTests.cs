using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildingBlocks.AcademicImports;
using DocumentsService.Controllers;
using DocumentsService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using Xunit;

namespace DocumentsService.Tests;

public sealed class RecordRegistrationTests
{
    [Fact]
    public void Record_completo_distingue_creditos_notas_y_continuacion_sin_encabezado()
    {
        var record = RecordAcademicoReader.Leer(Tabla());
        Assert.Equal("ANA MARIA", record.Perfil!.Nombres); Assert.Equal("PAZ LOPEZ", record.Perfil.Apellidos);
        Assert.Equal("Software", record.Perfil.Carrera); Assert.Equal("Universidad Técnica de Ambato", record.Perfil.Institucion);
        Assert.Equal(3, record.Filas.Count); Assert.Equal(2, record.Filas.Count(x => x.Seleccionado));
        var pendiente = Assert.Single(record.Filas, x => x.Estado == "SIN CURSAR"); Assert.Null(pendiente.Nota); Assert.Equal(6m, pendiente.Creditos);
        var row = Assert.Single(record.Filas, x => x.Nombre == "PROGRAMACION"); Assert.Equal(9m, row.Nota); Assert.Equal(3m, row.Creditos);
        Assert.Equal("ENERO 2026 - JULIO 2026", row.Periodo);
        Assert.Contains(record.Filas, x => x.Pagina == 2 && x.Nota == 8.6m && x.Periodo == "AGOSTO 2025 - ENERO 2026");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registrar_crea_estudiante_e_historial_en_una_sola_operacion_o_rechaza_identidad(bool identidadIncorrecta)
    {
        using var source = new Source(); var controller = Controller(source); var file = File(Tabla());
        var doc = RecordAcademicoReader.Leer(Tabla());
        doc = doc with { Identificacion = identidadIncorrecta ? "0102030401" : doc.Identificacion,
            Filas = [doc.Filas.First(x => x.Seleccionado) with { AsignaturaId = Source.Subject }] };
        var data = new RecordsAcademicosController.Registro(doc, 0, Source.Career, Source.Malla, 3);
        var result = await controller.Registrar(file, JsonSerializer.Serialize(data), default);
        if (identidadIncorrecta) { Assert.IsType<ConflictObjectResult>(result); Assert.Null(source.Created); return; }
        Assert.Equal(201, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.NotNull(source.Created);
        var body = JsonSerializer.Deserialize<JsonElement>(source.Created!);
        Assert.Equal("ANA MARIA", body.GetProperty("nombres").GetString());
        Assert.Equal("PAZ LOPEZ", body.GetProperty("apellidos").GetString());
        Assert.Single(body.GetProperty("historial").GetProperty("asignaturas").EnumerateArray());
        Assert.Equal(3m, body.GetProperty("historial").GetProperty("asignaturas")[0].GetProperty("creditos").GetDecimal());
        Assert.Equal(9m, body.GetProperty("historial").GetProperty("asignaturas")[0].GetProperty("nota").GetDecimal());
    }

    [Fact]
    public async Task Externo_puede_guardarse_con_creditos_y_horas_pendientes_y_reintento_no_crea_otro_estudiante()
    {
        using var source = new Source(); var controller = Controller(source);
        var doc = new VistaRecord("AB12345", [new("Programming", "Computing", "2026-A", 8.5m, "APROBADO", 1)], [],
            new PerfilRecord("Ana", "Paz", "Ana Paz", "Computing", "Universidad extranjera", 1));
        var data = new RecordsAcademicosController.Registro(doc, 1);
        var file = File(RecordImportTests.Pdf(["University report Ana Paz AB12345 Programming 85 / 100 Passed"]));
        Assert.Equal(201, Assert.IsType<ContentResult>(await controller.Registrar(file, JsonSerializer.Serialize(data), default)).StatusCode);
        Assert.NotNull(source.Created);
        var body = JsonSerializer.Deserialize<JsonElement>(source.Created!);
        var course = body.GetProperty("historial").GetProperty("asignaturas")[0];
        Assert.Equal(JsonValueKind.Null, course.GetProperty("creditos").ValueKind); Assert.Equal(JsonValueKind.Null, course.GetProperty("horas").ValueKind);
        Assert.Empty(course.GetProperty("temas").EnumerateArray());
        Assert.Equal(200, Assert.IsType<ContentResult>(await controller.Registrar(file, JsonSerializer.Serialize(data), default)).StatusCode);
        Assert.Equal(1, source.Creations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extraccion_externa_llama_al_agente_o_muestra_formulario_manual(bool caido)
    {
        using var source = new Source { AgentDown = caido }; var controller = Controller(source);
        var result = Assert.IsType<OkObjectResult>(await controller.AnalizarRegistro(File(Tabla()), default, true));
        var doc = Assert.IsType<VistaRecord>(result.Value);
        Assert.Equal(caido ? "" : "AB12345", doc.Identificacion);
        Assert.True(source.AgentCalled);
        if (caido) Assert.Contains(doc.Advertencias, a => a.Contains("manualmente"));
    }

    private static RecordsAcademicosController Controller(Source source) {
        var controller = new RecordsAcademicosController(source) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers.Authorization = "Bearer prueba"; return controller;
    }
    private static IFormFile File(byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", "record.pdf");
    private sealed class Source : HttpMessageHandler, IHttpClientFactory
    {
        public static readonly Guid Subject = Guid.NewGuid(), Career = Guid.NewGuid(), Malla = Guid.NewGuid();
        public string? Created; public int Creations; public bool AgentDown; public bool AgentCalled;
        private JsonObject? student;
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new Uri("http://test") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Assert.Equal("Bearer prueba", request.Headers.Authorization!.ToString());
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("extraer-record")) { AgentCalled = true; return AgentDown ? new(HttpStatusCode.ServiceUnavailable) : Ok(new VistaRecord("AB12345", [], [], new PerfilRecord())); }
            if (request.Method == HttpMethod.Post) {
                Creations++; Created = await request.Content!.ReadAsStringAsync(ct); student = JsonNode.Parse(Created)!.AsObject(); student["id"] = Guid.NewGuid().ToString();
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(student) };
            }
            if (request.Method == HttpMethod.Put) return Ok(new { guardado = true });
            if (path.EndsWith("/mallas")) return Ok(new[] { new { id = Malla } });
            if (path.Contains("catalogo/asignaturas")) return Ok(new { id = Subject, mallaVersionId = Malla, codigo = "PROG", nombre = "PROGRAMACION", creditos = 4, horas = 48 });
            return student is null ? new(HttpStatusCode.NotFound) : Ok(student);
        }
        private static HttpResponseMessage Ok(object obj) => new(HttpStatusCode.OK) { Content = JsonContent.Create(obj) };
    }
    private static byte[] Tabla() {
        using var builder = new PdfDocumentBuilder(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        void T(PdfPageBuilder p, string text, double x, double y) => p.AddText(text, 5, new PdfPoint(x, y), font);
        T(page, "Universidad Tecnica de Ambato", 35, 810); T(page, "CARRERA DE Software", 35, 790);
        T(page, "CERTIFICA: Que el/la (Sr(a),Srta).", 35, 770); T(page, "ANA MARIA PAZ LOPEZ con documento de identidad numero: 0102030400", 35, 750);
        T(page, "ASIGNATURA", 35, 700); T(page, "CRED", 212, 700); T(page, "PERIODO", 251, 700); T(page, "FOLIO", 299, 700);
        T(page, "MATRICULA", 325, 700); T(page, "PROMEDIO", 363, 700); T(page, "OBSERVACION", 402, 700); T(page, "ESTADO", 460, 700); T(page, "VEZ", 501, 700);
        T(page, "PROYECTO", 35, 670); T(page, "6", 219, 670); T(page, "0", 506, 670);
        T(page, "PROGRAMACION", 35, 640); T(page, "3", 219, 640); T(page, "ENERO 2026 - JULIO", 236, 640); T(page, "2026", 260, 630);
        T(page, "9.00", 373, 640); T(page, "Aprobado", 460, 640); T(page, "1", 506, 640);
        page = builder.AddPage(595, 842);
        T(page, "METODOLOGIA", 35, 790); T(page, "2", 219, 790); T(page, "AGOSTO 2025 - ENERO", 236, 790); T(page, "2026", 260, 780);
        T(page, "8.60", 373, 790); T(page, "Aprobado", 460, 790); T(page, "1", 506, 790); T(page, "PROMEDIO: 8.8", 35, 740);
        return builder.Build();
    }
}
