using System.Net.Http.Headers;
using System.Text.Json;

namespace ReportsService.Services;

public sealed class FuenteInformeClient(IHttpClientFactory clients)
{
    public Task<FuenteHomologacion> ConsultarAsync(Guid id, AuthenticationHeaderValue bearer, CancellationToken ct) =>
        LeerAsync<FuenteHomologacion>("Homologation", $"/api/v1/homologacion/expedientes/{id}/informe", bearer, ct);

    public async Task<DatosInforme> CompletarAsync(FuenteHomologacion fuente, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        var estudiante = await LeerAsync<JsonElement>("Students",
            $"/api/v1/estudiantes/{fuente.Expediente.EstudianteId}", bearer, ct);
        var carreras = await LeerAsync<JsonElement>("Academic", "/api/v1/catalogo/carreras", bearer, ct);
        var carrera = carreras.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == fuente.Expediente.CarreraDestinoId);
        var mallas = await LeerAsync<JsonElement>("Academic",
            $"/api/v1/catalogo/carreras/{fuente.Expediente.CarreraDestinoId}/mallas", bearer, ct);
        var malla = mallas.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == fuente.Expediente.MallaDestinoId);
        return new(fuente, $"{Texto(estudiante, "apellidos")}, {Texto(estudiante, "nombres")}",
            Texto(estudiante, "cedula"), Texto(carrera, "nombre"), Texto(malla, "version"));
    }

    private async Task<T> LeerAsync<T>(string client, string path, AuthenticationHeaderValue bearer, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = bearer;
        using var response = await clients.CreateClient(client).SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new JsonException("Un servicio devolvió una respuesta vacía.");
    }

    public static string Texto(JsonElement element, string key) =>
        element.GetProperty(key).GetString() ?? string.Empty;
}

public sealed record ExpedienteDatos(Guid Id, Guid EstudianteId, Guid CarreraDestinoId, Guid MallaDestinoId,
    Guid ProfesorId, string ProfesorNombre, int Estado);
public sealed record DecisionDatos(Guid Id, Guid AsignaturaDestinoId, string OrigenIdsJson,
    int ResultadoCalculado, int ResultadoFinal, string? Justificacion, Guid ProfesorId, DateTime RegistradoEn);
public sealed record FuenteHomologacion(ExpedienteDatos Expediente, DecisionDatos[] Decisiones, JsonElement Analisis);
public sealed record DatosInforme(FuenteHomologacion Fuente, string Estudiante, string Identificacion,
    string Carrera, string Malla);
