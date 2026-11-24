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

        if (resultado.TryGetProperty("falloComparacion", out var fallo)
            && fallo.ValueKind == JsonValueKind.True)
            throw new HttpRequestException(
                "La comparación de contenidos no pudo completarse. Reintenta cuando Ollama esté disponible.",
                null, HttpStatusCode.ServiceUnavailable);

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

    internal static void ValidarPreparacion(Historial? historial)
    {
        if (historial?.EsExterno != true) return;
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(historial.InstitucionOrigen)) faltantes.Add("Institución de origen");
        if (string.IsNullOrWhiteSpace(historial.CarreraOrigenNombre)) faltantes.Add("Carrera de origen");
        if (historial.Asignaturas.Length == 0) faltantes.Add("Asignaturas cursadas");
        foreach (var a in historial.Asignaturas)
        {
            var campos = new List<string>();
            if (a.Creditos is null or <= 0) campos.Add("créditos");
            if (a.Horas is null or <= 0) campos.Add("horas");
            if (a.Temas is null || a.Temas.Length == 0) campos.Add("temas y subtemas");
            foreach (var tema in a.Temas ?? [])
            {
                if (string.IsNullOrWhiteSpace(tema.Nombre)) campos.Add("nombre de tema");
                if (tema.Subtemas is null || !tema.Subtemas.Any(x => !string.IsNullOrWhiteSpace(x))) campos.Add($"subtemas de {tema.Nombre}");
            }
            if (campos.Count > 0) faltantes.Add($"{a.Nombre}: {string.Join(", ", campos)}");
        }
        if (faltantes.Count > 0) throw new InformacionIncompletaException(faltantes);
    }

    internal static Contenido[] ContenidosExternos(Origen origen) => (origen.Temas ?? []).Select((t, i) =>
        new Contenido($"externo:{origen.Id}:{i}", string.Join(" | ", (t.Subtemas ?? []).Prepend(t.Nombre)), t.Horas)).ToArray();

    internal sealed record Historial(Origen[] Asignaturas, bool EsExterno = false, string? InstitucionOrigen = null, string? CarreraOrigenNombre = null);
    internal sealed record Origen(Guid Id, Guid? AsignaturaId, string Nombre, decimal? Creditos, int? Horas, decimal Nota, TemaExterno[]? Temas = null);
    internal sealed record TemaExterno(string Nombre, string[] Subtemas, int? Horas = null);
    internal sealed record Asignatura(Guid Id, string Nombre, decimal Creditos, int Horas, bool EsTransversal);
    internal sealed record Tema(Guid Id, string Nombre, int? HorasEstimadas);
    internal sealed record Subtema(string Nombre);
    internal sealed record Contenido(string Codigo, string Descripcion, int? Horas = null);
    internal sealed record SolicitudReglas(decimal? CreditosOrigen, decimal CreditosDestino,
        decimal? HorasOrigen, int HorasDestino, bool OrigenAprobado, decimal NotaOrigen,
        bool OrigenTransversal, bool DestinoTransversal,
        IReadOnlyList<Contenido> ContenidosOrigen, IReadOnlyList<Contenido> ContenidosDestino);
}

public sealed class InformacionIncompletaException(IReadOnlyList<string> faltantes) : Exception("Falta información para homologar. Completa los datos del estudiante externo.")
{ public IReadOnlyList<string> Faltantes { get; } = faltantes; }
