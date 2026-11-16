using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HomologationService.Services;

public sealed class DatosEvaluacionClient(IHttpClientFactory clients)
{
    internal Task<Historial?> HistorialAsync(Guid estudianteId, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        LeerAsync<Historial>("StudentsService", $"/api/v1/estudiantes/{estudianteId}/historial", bearer, ct);

    internal async Task<Asignatura[]> DestinosAsync(Guid mallaId, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        await LeerAsync<Asignatura[]>("AcademicService", $"/api/v1/catalogo/mallas/{mallaId}/asignaturas", bearer, ct)
        ?? throw new HttpRequestException("No se encontró la malla del expediente.");

    internal Task<Asignatura?> AsignaturaAsync(Guid id, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        LeerAsync<Asignatura>("AcademicService", $"/api/v1/catalogo/asignaturas/{id}", bearer, ct);

    internal async Task<Tema[]> TemasAsync(Guid id, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        await LeerAsync<Tema[]>("AcademicService", $"/api/v1/catalogo/asignaturas/{id}/temas", bearer, ct) ?? [];

    internal async Task<Subtema[]> SubtemasAsync(Guid id, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        await LeerAsync<Subtema[]>("AcademicService", $"/api/v1/catalogo/temas/{id}/subtemas", bearer, ct) ?? [];

    internal async Task<Contenido[]> ContenidosAsync(Guid asignaturaId, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        var temas = await TemasAsync(asignaturaId, bearer, ct);
        var contenidos = new List<Contenido>(temas.Length);
        foreach (var tema in temas)
        {
            var subtemas = await SubtemasAsync(tema.Id, bearer, ct);
            var descripcion = string.Join(" | ", subtemas.Select(x => x.Nombre).Prepend(tema.Nombre));
            contenidos.Add(new(tema.Id.ToString(), descripcion, tema.HorasEstimadas));
        }

        return contenidos.ToArray();
    }

    internal async Task<JsonElement> EvaluarAsync(SolicitudReglas solicitud, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        var resultado = await EnviarAsync<JsonElement>("AgentIaService",
            new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-ia/evaluar-reglas")
            { Content = JsonContent.Create(solicitud) }, bearer, ct);
        if (resultado.ValueKind != JsonValueKind.Object
            || !resultado.TryGetProperty("reglas", out var reglas)
            || reglas.ValueKind != JsonValueKind.Object
            || !reglas.TryGetProperty("resultado", out _)
            || !reglas.TryGetProperty("versionReglas", out _))
            throw new JsonException("Agent IA devolvió una evaluación no válida.");

        return resultado;
    }

    private Task<T?> LeerAsync<T>(string servicio, string ruta, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        EnviarAsync<T>(servicio, new HttpRequestMessage(HttpMethod.Get, ruta), bearer, ct);

    private async Task<T?> EnviarAsync<T>(string servicio, HttpRequestMessage request, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        using (request)
        {
            request.Headers.Authorization = bearer;
            using var client = clients.CreateClient(servicio);
            using var response = await client.SendAsync(request, ct);
            if (request.Method == HttpMethod.Get && response.StatusCode == HttpStatusCode.NotFound)
                return default;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new JsonException("El servicio devolvió una respuesta vacía.");
        }
    }

    internal sealed record Historial(Origen[] Asignaturas);
    internal sealed record Origen(Guid Id, Guid? AsignaturaId, string Nombre, decimal? Creditos, int? Horas, decimal Nota);
    internal sealed record Asignatura(Guid Id, string Nombre, decimal Creditos, int Horas, bool EsTransversal);
    internal sealed record Tema(Guid Id, string Nombre, int? HorasEstimadas);
    internal sealed record Subtema(string Nombre);
    internal sealed record Contenido(string Codigo, string Descripcion, int? Horas = null);
    internal sealed record SolicitudReglas(decimal? CreditosOrigen, decimal CreditosDestino,
        decimal? HorasOrigen, int HorasDestino, bool OrigenAprobado, decimal NotaOrigen,
        bool OrigenTransversal, bool DestinoTransversal,
        IReadOnlyList<Contenido> ContenidosOrigen, IReadOnlyList<Contenido> ContenidosDestino);
}
