using BuildingBlocks.AcademicImports;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildingBlocks.Roles;
using DocumentsService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentsService.Controllers;

[ApiController]
[Route("api/v1/documentos/records")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class RecordsAcademicosController(IHttpClientFactory clients) : ControllerBase
{
    public sealed record Confirmacion(Guid EstudianteId, string Identificacion, FilaRecord[] Filas);

    [HttpPost("analizar")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Analizar(IFormFile archivo, CancellationToken ct)
    {
        try { return Ok(RecordAcademicoReader.Leer(await LeerArchivo(archivo, ct))); }
        catch (InvalidDataException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPost("confirmar")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Confirmar(IFormFile archivo, [FromForm] string datos, CancellationToken ct, [FromQuery] bool externo = false)
    {
        try
        {
            var bytes = await LeerArchivo(archivo, ct);
            VistaRecord original;
            if (externo) { RecordAcademicoReader.Texto(bytes); original = new("", [], []); }
            else original = RecordAcademicoReader.Leer(bytes);
            var request = JsonSerializer.Deserialize<Confirmacion>(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (request is null || request.EstudianteId == Guid.Empty || string.IsNullOrWhiteSpace(request.Identificacion) || request.Filas is null) return BadRequest();
            var seleccion = request.Filas.Where(x => x.Seleccionado).ToArray();
            if (seleccion.Length == 0 || seleccion.Any(x => string.IsNullOrWhiteSpace(x.Nombre) || x.Nombre.Length > 200 || string.IsNullOrWhiteSpace(x.Carrera) || x.Carrera.Length > 200
                || string.IsNullOrWhiteSpace(x.Periodo) || x.Periodo.Length > 30 || x.Nota is null or < 0 or > 10 || x.Estado != "APROBADO"))
                return BadRequest(new { mensaje = "Selecciona asignaturas aprobadas con carrera, nota y período completos." });
            var student = await LeerJson("StudentsService", $"/api/v1/estudiantes/{request.EstudianteId}", ct);
            if (student is null) return NotFound(new { mensaje = "El estudiante no existe." });
            if (student["eliminado"]?.GetValue<bool>() == true) return Conflict(new { mensaje = "El estudiante fue eliminado del registro activo." });
            var identificacion = student["cedula"]!.GetValue<string>();
            if (identificacion != request.Identificacion.Trim() || (original.Identificacion.Length > 0 && original.Identificacion != identificacion))
                return Conflict(new { mensaje = "La identificación del PDF no corresponde al estudiante seleccionado." });
            var esExterno = student["tipo"]!.GetValue<int>() == 1;
            var history = student["historial"]?.DeepClone() as JsonObject;
            if (history is null) return Conflict(new { mensaje = "Registra primero la carrera, nivel y malla del estudiante." });
            var asignaturas = history["asignaturas"]!.AsArray();
            var incomingIds = new HashSet<string>();
            foreach (var fila in seleccion)
            {
                if (!esExterno && fila.AsignaturaId is null) return BadRequest(new { mensaje = $"Asocia {fila.Nombre} a una asignatura del catálogo antes de confirmar." });
                JsonObject? ficha = fila.AsignaturaId is Guid id ? await LeerJson("AcademicService", $"/api/v1/catalogo/asignaturas/{id}", ct) : null;
                if (fila.AsignaturaId.HasValue && ficha is null) return BadRequest(new { mensaje = "Una asignatura seleccionada ya no existe en el catálogo." });
                if (esExterno && fila.AsignaturaId.HasValue) return BadRequest(new { mensaje = "Los contenidos externos deben completarse desde el historial externo, sin vincularlos como asignaturas internas." });
                var codigo = ficha?["codigo"]?.GetValue<string>() ?? "PDF-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fila.Carrera.Trim().ToUpperInvariant() + "|" + fila.Nombre.Trim().ToUpperInvariant())))[..16];
                var creditos = fila.Creditos ?? ficha?["creditos"]?.GetValue<decimal>();
                var horas = fila.Horas ?? ficha?["horas"]?.GetValue<int>();
                if ((!esExterno && (creditos is null or <= 0 || horas is null or <= 0)) || creditos is <= 0 or > 999.99m || horas is <= 0) return BadRequest(new { mensaje = $"Completa créditos y horas de {fila.Nombre}; no figuran en el PDF." });
                var key = fila.AsignaturaId?.ToString() ?? codigo;
                if (!incomingIds.Add(key)) return Conflict(new { mensaje = $"{fila.Nombre} aparece varias veces. Selecciona el intento aprobado que debe usarse en la homologación." });
                var actual = asignaturas.OfType<JsonObject>().FirstOrDefault(a => fila.AsignaturaId.HasValue
                    ? a["asignaturaId"]?.GetValue<string>() == fila.AsignaturaId.ToString() : a["codigo"]?.GetValue<string>() == codigo);
                if (actual is not null && (actual["nota"]!.GetValue<decimal>() != fila.Nota || actual["periodo"]!.GetValue<string>() != fila.Periodo) && !fila.Reemplazar)
                    return Conflict(new { mensaje = $"Ya existe {fila.Nombre} con otra nota o período. Autoriza su reemplazo o desmarca la fila." });
                if (actual is not null && !fila.Reemplazar) continue;
                var nueva = new JsonObject { ["asignaturaId"] = fila.AsignaturaId?.ToString(), ["codigo"] = codigo,
                    ["nombre"] = ficha?["nombre"]?.GetValue<string>() ?? fila.Nombre, ["creditos"] = creditos,
                    ["horas"] = horas, ["nota"] = fila.Nota, ["periodo"] = fila.Periodo,
                    ["carreraOrigenNombre"] = fila.Carrera.Trim(),
                    ["temas"] = actual?["temas"]?.DeepClone() ?? new JsonArray() };
                if (actual is not null) asignaturas.Remove(actual);
                asignaturas.Add(nueva);
            }
            using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/estudiantes/{request.EstudianteId}/historial") { Content = JsonContent.Create(history) };
            message.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
            using var response = await clients.CreateClient("StudentsService").SendAsync(message, ct);
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = await response.Content.ReadAsStringAsync(ct) };
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException) { return BadRequest(new { mensaje = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { mensaje = "No se pudieron consultar Students o Academic. No se guardaron cambios." }); }
    }

    public sealed record Registro(VistaRecord Documento, int Tipo, Guid? CarreraOrigenId = null, Guid? MallaOrigenId = null, int? NivelActual = null);

    [HttpPost("analizar-registro")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> AnalizarRegistro(IFormFile archivo, CancellationToken ct, [FromQuery] bool externo = false)
    {
        try {
            var bytes = await LeerArchivo(archivo, ct);
            if (!externo) return Ok(RecordAcademicoReader.Leer(bytes));
            var texto = RecordAcademicoReader.Texto(bytes);
            if (texto.Length > 40000) return BadRequest(new { mensaje = "Divide el documento externo en archivos de hasta 40.000 caracteres de texto para la extracción con IA." });
            try {
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agent-ia/extraer-record") { Content = JsonContent.Create(new SolicitudExtraccionRecord(texto)) };
                message.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
                using var response = await clients.CreateClient("AgentIaService").SendAsync(message, ct);
                if (response.IsSuccessStatusCode) { var extracted = await response.Content.ReadFromJsonAsync<VistaRecord>(cancellationToken: ct); if (extracted?.Filas is not null) return Ok(extracted); }
            } catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested) { }
            return Ok(new VistaRecord("", [], ["La extracción con IA no está disponible o no devolvió datos válidos. Puedes reintentar o completar manualmente los campos y asignaturas. No se guardó ningún dato."], new PerfilRecord(TipoIdentificacion: 1, EscalaMaxima: null)));
        } catch (InvalidDataException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPost("registrar")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Registrar(IFormFile archivo, [FromForm] string datos, CancellationToken ct)
    {
        try {
            var request = JsonSerializer.Deserialize<Registro>(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (request?.Documento?.Perfil is not PerfilRecord perfil || request.Tipo is < 0 or > 1)
                return BadRequest(new { mensaje = "Revisa el perfil del estudiante y el tipo de registro." });
            var bytes = await LeerArchivo(archivo, ct);
            var original = request.Tipo == 0 ? RecordAcademicoReader.Leer(bytes) : new VistaRecord("", [], []);
            if (request.Tipo == 1) RecordAcademicoReader.Texto(bytes);
            var documento = request.Documento;
            if (string.IsNullOrWhiteSpace(documento.Identificacion) || string.IsNullOrWhiteSpace(perfil.Nombres) || string.IsNullOrWhiteSpace(perfil.Apellidos))
                return BadRequest(new { mensaje = "Completa identificación, nombres y apellidos del estudiante." });
            if (original.Identificacion.Length > 0 && original.Identificacion != documento.Identificacion.Trim())
                return Conflict(new { mensaje = "La identificación editada no coincide con la del documento." });
            var rows = (documento.Filas ?? []).Where(x => x.Seleccionado).ToArray();
            if (rows.Length == 0 || rows.Length > 500 || rows.Any(x => string.IsNullOrWhiteSpace(x.Nombre) || x.Nombre.Length > 200 || string.IsNullOrWhiteSpace(x.Carrera)
                || x.Carrera.Length > 200 || string.IsNullOrWhiteSpace(x.Periodo) || x.Periodo.Length > 30 || x.Nota is null or < 0 or > 10 || x.Estado != "APROBADO"))
                return BadRequest(new { mensaje = "Selecciona asignaturas aprobadas con nombre, carrera, período y nota confirmada entre 0 y 10. Los datos faltantes deben completarse." });
            var existing = await LeerJson("StudentsService", $"/api/v1/estudiantes/cedula/{Uri.EscapeDataString(documento.Identificacion.Trim())}", ct);
            if (existing is not null) {
                if (existing["tipo"]!.GetValue<int>() != request.Tipo) return Conflict(new { mensaje = "La identificación ya pertenece a un estudiante de otro tipo." });
                var confirm = new Confirmacion(Guid.Parse(existing["id"]!.GetValue<string>()), documento.Identificacion, rows);
                return await Confirmar(archivo, JsonSerializer.Serialize(confirm), ct, request.Tipo == 1);
            }
            if (request.Tipo == 0) {
                if (request.CarreraOrigenId is null || request.MallaOrigenId is null || request.NivelActual is null or <= 0)
                    return BadRequest(new { mensaje = "Confirma carrera, malla y nivel actual del estudiante interno." });
                using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/catalogo/carreras/{request.CarreraOrigenId}/mallas");
                message.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
                using var response = await clients.CreateClient("AcademicService").SendAsync(message, ct); response.EnsureSuccessStatusCode();
                var mallas = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken: ct);
                if (mallas is null || !mallas.OfType<JsonObject>().Any(m => m["id"]!.GetValue<string>() == request.MallaOrigenId.ToString()))
                    return BadRequest(new { mensaje = "La malla no pertenece a la carrera seleccionada." });
            }
            var asignaturas = new List<object>(); var used = new HashSet<string>();
            foreach (var row in rows) {
                JsonObject? subject = null;
                if (request.Tipo == 0) {
                    if (!row.AsignaturaId.HasValue) return BadRequest(new { mensaje = $"Asocia {row.Nombre} al catálogo antes de registrar." });
                    subject = await LeerJson("AcademicService", $"/api/v1/catalogo/asignaturas/{row.AsignaturaId}", ct);
                    if (subject is null || subject["mallaVersionId"]?.GetValue<string>() != request.MallaOrigenId.ToString())
                        return BadRequest(new { mensaje = "Una materia no pertenece a la malla de origen seleccionada." });
                } else if (row.AsignaturaId.HasValue) return BadRequest(new { mensaje = "Las materias externas no deben vincularse como materias internas." });
                var code = subject?["codigo"]?.GetValue<string>() ?? "PDF-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Carrera.Trim().ToUpperInvariant() + "|" + row.Nombre.Trim().ToUpperInvariant())))[..16];
                if (!used.Add(row.AsignaturaId?.ToString() ?? code)) return Conflict(new { mensaje = $"Selecciona un solo intento aprobado de {row.Nombre}." });
                var credits = row.Creditos ?? subject?["creditos"]?.GetValue<decimal>(); var hours = row.Horas ?? subject?["horas"]?.GetValue<int>();
                if (credits is <= 0 or > 999.99m || hours is <= 0) return BadRequest(new { mensaje = "Revisa los créditos y las horas: deben ser positivos cuando se informan." });
                asignaturas.Add(new { asignaturaId = request.Tipo == 0 ? row.AsignaturaId : null, codigo = code, nombre = subject?["nombre"]?.GetValue<string>() ?? row.Nombre,
                    creditos = credits, horas = hours, nota = row.Nota, periodo = row.Periodo, carreraOrigenNombre = row.Carrera, temas = Array.Empty<object>() });
            }
            var body = new { cedula = documento.Identificacion.Trim(), tipoIdentificacion = perfil.TipoIdentificacion, nombres = perfil.Nombres.Trim(), apellidos = perfil.Apellidos.Trim(), tipo = request.Tipo,
                historial = new { carreraOrigenId = request.Tipo == 0 ? request.CarreraOrigenId : null, mallaOrigenId = request.Tipo == 0 ? request.MallaOrigenId : null,
                    carreraOrigenNombre = perfil.Carrera, institucionOrigen = perfil.Institucion, nivelActual = request.NivelActual, asignaturas } };
            using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/estudiantes") { Content = JsonContent.Create(body) };
            create.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
            using var saved = await clients.CreateClient("StudentsService").SendAsync(create, ct);
            return new ContentResult { StatusCode = (int)saved.StatusCode, ContentType = "application/json", Content = await saved.Content.ReadAsStringAsync(ct) };
        } catch (Exception ex) when (ex is InvalidDataException or JsonException) { return BadRequest(new { mensaje = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { mensaje = "No se pudo completar el registro. Verifica Students y Academic antes de reintentar." }); }
    }

    private async Task<JsonObject?> LeerJson(string service, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
        using var response = await clients.CreateClient(service).SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
    }
    private static async Task<byte[]> LeerArchivo(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0 || archivo.Length > 10 * 1024 * 1024 || !archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selecciona un PDF de hasta 10 MB.");
        using var memory = new MemoryStream(); await archivo.CopyToAsync(memory, ct); return memory.ToArray();
    }
}
