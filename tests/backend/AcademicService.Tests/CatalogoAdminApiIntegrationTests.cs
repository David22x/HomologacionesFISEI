using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AcademicService.Contracts;
using Xunit;

namespace AcademicService.Tests;

public sealed class CatalogoAdminApiIntegrationTests : IClassFixture<CatalogApiFactory>
{
    private readonly CatalogApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogoAdminApiIntegrationTests(CatalogApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Administrador_puede_crear_y_actualizar_el_catalogo_completo()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var codigoCarrera = $"T{suffix}";

        using var crearCarrera = await EnviarJsonAsync(HttpMethod.Post, "/api/v1/catalogo/carreras",
            new CarreraWriteRequest(codigoCarrera, "Carrera temporal", false));
        Assert.Equal(HttpStatusCode.Created, crearCarrera.StatusCode);
        var carrera = await crearCarrera.Content.ReadFromJsonAsync<CarreraCatalogoResponse>();
        Assert.NotNull(carrera);

        using var actualizarCarrera = await EnviarJsonAsync(HttpMethod.Put,
            $"/api/v1/catalogo/carreras/{carrera.Id}",
            new CarreraWriteRequest(codigoCarrera, "Carrera temporal editada", false));
        Assert.Equal(HttpStatusCode.OK, actualizarCarrera.StatusCode);

        using var crearMalla = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/carreras/{carrera.Id}/mallas",
            new MallaWriteRequest("1.0"));
        Assert.Equal(HttpStatusCode.Created, crearMalla.StatusCode);
        var malla = await crearMalla.Content.ReadFromJsonAsync<MallaCatalogoResponse>();
        Assert.NotNull(malla);
        Assert.Equal("Borrador", malla.Estado);
        Assert.False(malla.Vigente);

        using var actualizarMalla = await EnviarJsonAsync(HttpMethod.Put,
            $"/api/v1/catalogo/mallas/{malla.Id}", new MallaWriteRequest("1.1"));
        Assert.Equal(HttpStatusCode.OK, actualizarMalla.StatusCode);

        using var crearNivel = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/niveles", new NivelWriteRequest(1));
        Assert.Equal(HttpStatusCode.Created, crearNivel.StatusCode);
        var nivel = await crearNivel.Content.ReadFromJsonAsync<NivelCatalogoResponse>();
        Assert.NotNull(nivel);

        using var actualizarNivel = await EnviarJsonAsync(HttpMethod.Put,
            $"/api/v1/catalogo/niveles/{nivel.Id}", new NivelWriteRequest(2));
        Assert.Equal(HttpStatusCode.OK, actualizarNivel.StatusCode);

        var codigoAsignatura = $"A-{suffix}";
        using var crearAsignatura = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas",
            new AsignaturaWriteRequest(nivel.Id, codigoAsignatura, "Asignatura temporal", 3m, 48, "Básica"));
        Assert.Equal(HttpStatusCode.Created, crearAsignatura.StatusCode);
        var asignatura = await crearAsignatura.Content.ReadFromJsonAsync<AsignaturaCatalogoResponse>();
        Assert.NotNull(asignatura);

        using var actualizarAsignatura = await EnviarJsonAsync(HttpMethod.Put,
            $"/api/v1/catalogo/asignaturas/{asignatura.Id}",
            new AsignaturaWriteRequest(nivel.Id, codigoAsignatura, "Asignatura editada", 4m, 64, "Profesional"));
        Assert.Equal(HttpStatusCode.OK, actualizarAsignatura.StatusCode);

        using var publicar = await EnviarSinBodyAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/publicar");
        Assert.Equal(HttpStatusCode.OK, publicar.StatusCode);
        var publicada = await publicar.Content.ReadFromJsonAsync<MallaCatalogoResponse>();
        Assert.NotNull(publicada);
        Assert.Equal("Publicada", publicada.Estado);
        Assert.True(publicada.Vigente);
        Assert.Equal("test-Administrador", publicada.ModificadoPor);
        Assert.True(publicada.ModificadoEn > DateTime.UnixEpoch);
    }

    [Fact]
    public async Task Publicar_una_malla_reemplaza_la_publicada_de_la_carrera_y_sincroniza_vigente()
    {
        using var carrerasResponse = await EnviarSinBodyAsync(HttpMethod.Get, "/api/v1/catalogo/carreras");
        var carreras = await carrerasResponse.Content.ReadFromJsonAsync<List<CarreraCatalogoResponse>>();
        var carrera = Assert.Single(carreras!, item => item.Codigo == "TI");

        using var crearResponse = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/carreras/{carrera.Id}/mallas",
            new MallaWriteRequest($"PR-{Guid.NewGuid():N}"[..11]));
        Assert.Equal(HttpStatusCode.Created, crearResponse.StatusCode);
        var nuevaMalla = await crearResponse.Content.ReadFromJsonAsync<MallaCatalogoResponse>();
        Assert.NotNull(nuevaMalla);

        using var publicarResponse = await EnviarSinBodyAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{nuevaMalla.Id}/publicar");
        Assert.Equal(HttpStatusCode.OK, publicarResponse.StatusCode);

        using var mallasResponse = await EnviarSinBodyAsync(HttpMethod.Get,
            $"/api/v1/catalogo/carreras/{carrera.Id}/mallas");
        var mallas = await mallasResponse.Content.ReadFromJsonAsync<List<MallaCatalogoResponse>>();
        Assert.NotNull(mallas);
        Assert.Equal(1, mallas.Count(m => m.Estado == "Publicada"));
        Assert.Equal(1, mallas.Count(m => m.Vigente));
        Assert.Contains(mallas, m => m.Id == nuevaMalla.Id && m.Estado == "Publicada" && m.Vigente);
        Assert.Contains(mallas, m => m.Id != nuevaMalla.Id && m.Estado == "Borrador" && !m.Vigente);
    }

    [Fact]
    public async Task Validaciones_evitan_creditos_no_positivos_niveles_de_otra_malla_y_duplicados()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var carrera = await CrearCarreraAsync($"V{suffix}");
        var malla = await CrearMallaAsync(carrera.Id, "2026");
        var otraMalla = await CrearMallaAsync(carrera.Id, "2027");
        var nivel = await CrearNivelAsync(malla.Id, 1);
        var otroNivel = await CrearNivelAsync(otraMalla.Id, 1);

        using var creditosInvalidos = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas",
            new AsignaturaWriteRequest(nivel.Id, $"X-{suffix}", "Inválida", 0m, 40, null));
        Assert.Equal(HttpStatusCode.BadRequest, creditosInvalidos.StatusCode);

        using var nivelDeOtraMalla = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas",
            new AsignaturaWriteRequest(otroNivel.Id, $"Y-{suffix}", "Inválida", 3m, 40, null));
        Assert.Equal(HttpStatusCode.NotFound, nivelDeOtraMalla.StatusCode);

        var codigo = $"D-{suffix}";
        using var primera = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas",
            new AsignaturaWriteRequest(nivel.Id, codigo, "Primera", 3m, 40, null));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        using var duplicada = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{malla.Id}/asignaturas",
            new AsignaturaWriteRequest(nivel.Id, codigo.ToLowerInvariant(), "Duplicada", 3m, 40, null));
        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);

        using var mallaDuplicada = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/carreras/{carrera.Id}/mallas", new MallaWriteRequest("2026"));
        Assert.Equal(HttpStatusCode.Conflict, mallaDuplicada.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/catalogo/carreras", "{\"codigo\":\"DENY\",\"nombre\":\"No autorizada\",\"esDemo\":false}")]
    [InlineData("PUT", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001", "{\"codigo\":\"DENY\",\"nombre\":\"No autorizada\",\"esDemo\":false}")]
    [InlineData("POST", "/api/v1/catalogo/carreras/00000000-0000-0000-0000-000000000001/mallas", "{\"version\":\"2027\"}")]
    [InlineData("PUT", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001", "{\"version\":\"2027\"}")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/niveles", "{\"numero\":1}")]
    [InlineData("PUT", "/api/v1/catalogo/niveles/00000000-0000-0000-0000-000000000001", "{\"numero\":1}")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/asignaturas", "{\"nivelId\":\"00000000-0000-0000-0000-000000000001\",\"codigo\":\"A-1\",\"nombre\":\"A\",\"creditos\":3,\"horas\":40}")]
    [InlineData("PUT", "/api/v1/catalogo/asignaturas/00000000-0000-0000-0000-000000000001", "{\"nivelId\":\"00000000-0000-0000-0000-000000000001\",\"codigo\":\"A-1\",\"nombre\":\"A\",\"creditos\":3,\"horas\":40}")]
    [InlineData("POST", "/api/v1/catalogo/mallas/00000000-0000-0000-0000-000000000001/publicar", null)]
    public async Task Escrituras_son_solo_para_administrador_y_requieren_token(
        string method,
        string path,
        string? json)
    {
        using var sinTokenRequest = new HttpRequestMessage(new HttpMethod(method), path);
        if (json is not null)
        {
            sinTokenRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }
        using var sinToken = await _client.SendAsync(sinTokenRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        using var profesorRequest = new HttpRequestMessage(new HttpMethod(method), path);
        if (json is not null)
        {
            profesorRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }
        profesorRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CatalogApiFactory.CreateToken("Profesor"));
        using var profesor = await _client.SendAsync(profesorRequest);
        Assert.Equal(HttpStatusCode.Forbidden, profesor.StatusCode);
    }

    [Fact]
    public async Task Swagger_documenta_los_endpoints_de_escritura_y_publicacion()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/v1/catalogo/carreras").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/carreras/{carreraId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/carreras/{carreraId}/mallas").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/mallas/{mallaId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/mallas/{mallaId}/niveles").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/niveles/{nivelId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/mallas/{mallaId}/asignaturas").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/asignaturas/{asignaturaId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/catalogo/mallas/{mallaId}/publicar").TryGetProperty("post", out _));
    }

    private async Task<CarreraCatalogoResponse> CrearCarreraAsync(string codigo)
    {
        using var response = await EnviarJsonAsync(HttpMethod.Post, "/api/v1/catalogo/carreras",
            new CarreraWriteRequest(codigo, "Carrera de prueba", false));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CarreraCatalogoResponse>())!;
    }

    private async Task<MallaCatalogoResponse> CrearMallaAsync(Guid carreraId, string version)
    {
        using var response = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/carreras/{carreraId}/mallas", new MallaWriteRequest(version));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MallaCatalogoResponse>())!;
    }

    private async Task<NivelCatalogoResponse> CrearNivelAsync(Guid mallaId, int numero)
    {
        using var response = await EnviarJsonAsync(HttpMethod.Post,
            $"/api/v1/catalogo/mallas/{mallaId}/niveles", new NivelWriteRequest(numero));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NivelCatalogoResponse>())!;
    }

    private Task<HttpResponseMessage> EnviarJsonAsync<T>(HttpMethod method, string path, T value)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(value)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CatalogApiFactory.CreateToken("Administrador"));
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> EnviarSinBodyAsync(HttpMethod method, string path) =>
        EnviarAdministradorAsync(method, path);

    private Task<HttpResponseMessage> EnviarAdministradorAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CatalogApiFactory.CreateToken("Administrador"));
        return _client.SendAsync(request);
    }

}
