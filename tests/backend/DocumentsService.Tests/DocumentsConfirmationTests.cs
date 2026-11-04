using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DocumentsService.Tests;

public sealed class DocumentsConfirmationTests : IClassFixture<DocumentsApiFactory>
{
    private readonly DocumentsApiFactory _factory;
    private readonly HttpClient _client;

    public DocumentsConfirmationTests(DocumentsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Confirmacion_ValidaElArchivo_ReenviaElJwtYLaImportacionAStudents()
    {
        using var request = CreateRequest(CreateValidWorkbook(), includeToken: true);

        using var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync());
        Assert.Equal("/api/v1/estudiantes/00000000-0000-0000-0000-000000000001",
            response.Headers.Location?.OriginalString);
        Assert.Equal("/internal/estudiantes/importaciones", _factory.Handler.RequestPath);
        Assert.Equal("Bearer test-token", _factory.Handler.Authorization);
        using var body = JsonDocument.Parse(_factory.Handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("AB12345", root.GetProperty("cedula").GetString());
        Assert.Equal(1, root.GetProperty("tipoIdentificacion").GetInt32());
        Assert.Equal(1, root.GetProperty("tipo").GetInt32());
        Assert.Equal("Ingeniería", root.GetProperty("historial").GetProperty("carreraOrigenNombre").GetString());
        Assert.Equal(3, root.GetProperty("historial").GetProperty("nivelActual").GetInt32());
        var subject = root.GetProperty("historial").GetProperty("asignaturas")[0];
        Assert.Equal("MAT1", subject.GetProperty("codigo").GetString());
        Assert.Equal(3.5m, subject.GetProperty("creditos").GetDecimal());
        Assert.Equal(9.5m, subject.GetProperty("nota").GetDecimal());
    }

    [Fact]
    public async Task Confirmacion_SinJwtNoLlamaAStudents()
    {
        var callsBefore = _factory.Handler.CallCount;
        using var request = CreateRequest(CreateValidWorkbook(), includeToken: false);

        using var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
            await response.Content.ReadAsStringAsync());
        Assert.Equal(callsBefore, _factory.Handler.CallCount);
    }

    [Fact]
    public async Task Confirmacion_ArchivoInvalidoNoLlamaAStudents()
    {
        var callsBefore = _factory.Handler.CallCount;
        using var request = CreateRequest([1, 2, 3], includeToken: true);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(callsBefore, _factory.Handler.CallCount);
    }

    private static HttpRequestMessage CreateRequest(byte[] workbook, bool includeToken)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(file, "archivo", "importacion.xlsx");
        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/documentos/importaciones/confirmar")
        {
            Content = multipart
        };
        if (includeToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        return request;
    }

    private static string CreateToken()
    {
        const string key = "documents-tests-signing-key-at-least-32-bytes";
        var token = new JwtSecurityToken(
            issuer: "HomologacionesFISEI",
            audience: "HomologacionesFISEIClient",
            claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Profesor")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static byte[] CreateValidWorkbook()
    {
        using var workbook = new XLWorkbook();
        var student = workbook.Worksheets.Add("Estudiante");
        student.Cell(1, 1).Value = "Identificacion";
        student.Cell(1, 2).Value = "TipoIdentificacion";
        student.Cell(1, 3).Value = "Nombres";
        student.Cell(1, 4).Value = "Apellidos";
        student.Cell(1, 5).Value = "InstitucionOrigen";
        student.Cell(1, 6).Value = "CarreraOrigen";
        student.Cell(1, 7).Value = "NivelActual";
        student.Cell(2, 1).Value = "AB12345";
        student.Cell(2, 2).Value = "Pasaporte";
        student.Cell(2, 3).Value = "Ana";
        student.Cell(2, 4).Value = "Paz";
        student.Cell(2, 5).Value = "Universidad de origen";
        student.Cell(2, 6).Value = "Ingeniería";
        student.Cell(2, 7).Value = "3.0";

        var subjects = workbook.Worksheets.Add("Asignaturas");
        var subjectHeaders = new[] { "Codigo", "Nombre", "Creditos", "Horas", "Nota", "Periodo" };
        for (var index = 0; index < subjectHeaders.Length; index++)
            subjects.Cell(1, index + 1).Value = subjectHeaders[index];
        subjects.Cell(2, 1).Value = "MAT1";
        subjects.Cell(2, 2).Value = "Matemática";
        subjects.Cell(2, 3).Value = "3.5";
        subjects.Cell(2, 4).Value = "48";
        subjects.Cell(2, 5).Value = "9,5";
        subjects.Cell(2, 6).Value = "2026-A";

        var contents = workbook.Worksheets.Add("Contenidos");
        contents.Cell(1, 1).Value = "CodigoAsignatura";
        contents.Cell(1, 2).Value = "Tema";
        contents.Cell(1, 3).Value = "Subtema";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

public sealed class DocumentsApiFactory : WebApplicationFactory<Program>
{
    public StudentsServiceHandler Handler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SecretKey", "documents-tests-signing-key-at-least-32-bytes");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient("StudentsService")
                .ConfigurePrimaryHttpMessageHandler(() => Handler);
        });
    }
}

public sealed class StudentsServiceHandler : HttpMessageHandler
{
    public string? RequestPath { get; private set; }
    public string? Authorization { get; private set; }
    public string? RequestBody { get; private set; }
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        RequestPath = request.RequestUri?.AbsolutePath;
        Authorization = request.Headers.Authorization?.ToString();
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"id":"00000000-0000-0000-0000-000000000001"}""",
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.Location =
            new Uri("/api/v1/estudiantes/00000000-0000-0000-0000-000000000001", UriKind.Relative);
        return response;
    }
}
