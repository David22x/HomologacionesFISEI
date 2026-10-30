using System.Security.Claims;
using System.Text.RegularExpressions;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentsService.Contracts;
using StudentsService.Data;
using StudentsService.Models;

namespace StudentsService.Controllers;

[ApiController]
[Route("api/v1/estudiantes")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public sealed class EstudiantesController(StudentsDbContext context, ILogger<EstudiantesController> logger) : ControllerBase
{
    /// <summary>Lista y filtra estudiantes por cédula, nombres o apellidos e incluye su historial.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EstudianteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EstudianteResponse>>> GetEstudiantes(
        [FromQuery] string? filtro, CancellationToken ct)
    {
        var query = context.Estudiantes.AsNoTracking().Include(e => e.Historial)
            .ThenInclude(h => h!.Asignaturas).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var term = filtro.Trim().ToUpperInvariant();
            query = query.Where(e => e.Cedula.ToUpper().Contains(term)
                || e.Nombres.ToUpper().Contains(term) || e.Apellidos.ToUpper().Contains(term));
        }
        var entities = await query.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres).ToListAsync(ct);
        return Ok(entities.Select(ToResponse).ToList());
    }

    /// <summary>Obtiene un estudiante y su historial por identificador.</summary>
    [HttpGet("{estudianteId:guid}")]
    [ProducesResponseType(typeof(EstudianteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstudianteResponse>> GetEstudiante(Guid estudianteId, CancellationToken ct)
    {
        var entity = await context.Estudiantes.AsNoTracking().Include(e => e.Historial)
            .ThenInclude(h => h!.Asignaturas).FirstOrDefaultAsync(e => e.Id == estudianteId, ct);
        return entity is null ? NotFound() : Ok(ToResponse(entity));
    }

    /// <summary>Registra un estudiante y su historial académico.</summary>
    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
    [ProducesResponseType(typeof(EstudianteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstudianteResponse>> Crear(EstudianteWriteRequest request, CancellationToken ct)
    {
        var error = ValidarEstudiante(request);
        if (error is not null) return BadRequest(Problem(400, error));
        var key = NormalizarId(request.Cedula);
        if (await context.Estudiantes.AnyAsync(e => e.Cedula == key, ct))
            return Conflict(Problem(409, "Ya existe un estudiante con esa identificación."));

        var actor = ActorActual();
        var now = DateTime.UtcNow;
        var entity = new Estudiante { Cedula = key, TipoIdentificacion = request.TipoIdentificacion,
            Nombres = request.Nombres.Trim(), Apellidos = request.Apellidos.Trim(), Tipo = request.Tipo,
            CreadoPor = actor, CreadoEn = now, ModificadoPor = actor, ModificadoEn = now };
        if (request.Historial is not null)
            entity.Historial = CrearHistorial(entity, request.Historial);
        logger.LogInformation("Registered student identification {Identification}", MaskIdentification(key));
        context.Estudiantes.Add(entity);
        if (!await SaveAsync(ct)) return Conflict(Problem(409, "Ya existe un estudiante con esa identificación."));
        return CreatedAtAction(nameof(GetEstudiante), new { estudianteId = entity.Id }, ToResponse(entity));
    }

    /// <summary>Actualiza la información del estudiante y opcionalmente su historial.</summary>
    [HttpPut("{estudianteId:guid}")]
    [ProducesResponseType(typeof(EstudianteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstudianteResponse>> Actualizar(
        Guid estudianteId, EstudianteWriteRequest request, CancellationToken ct)
    {
        var entity = await context.Estudiantes.Include(e => e.Historial)
            .ThenInclude(h => h!.Asignaturas).FirstOrDefaultAsync(e => e.Id == estudianteId, ct);
        if (entity is null) return NotFound();
        var error = ValidarEstudiante(request, requireHistory: false);
        if (error is not null) return BadRequest(Problem(400, error));
        if (request.Tipo == TipoEstudiante.Interno && request.Historial is null && entity.Historial is null)
            return BadRequest(Problem(400, "El historial del estudiante interno es obligatorio."));
        var key = NormalizarId(request.Cedula);
        if (await context.Estudiantes.AnyAsync(e => e.Id != estudianteId && e.Cedula == key, ct))
            return Conflict(Problem(409, "Ya existe un estudiante con esa identificación."));

        logger.LogInformation("Updated student identification {Identification}", MaskIdentification(key));
        entity.Cedula = key;
        entity.TipoIdentificacion = request.TipoIdentificacion;
        entity.Nombres = request.Nombres.Trim();
        entity.Apellidos = request.Apellidos.Trim();
        entity.Tipo = request.Tipo;
        entity.ModificadoPor = ActorActual();
        entity.ModificadoEn = DateTime.UtcNow;
        if (request.Historial is not null)
        {
            error = ValidarHistorial(request.Historial, request.Tipo);
            if (error is not null) return BadRequest(Problem(400, error));
            if (entity.Historial is null) entity.Historial = CrearHistorial(entity, request.Historial);
            else ActualizarHistorial(entity.Historial, request.Historial);
        }
        if (!await SaveAsync(ct)) return Conflict(Problem(409, "Ya existe un estudiante con esa identificación."));
        return Ok(ToResponse(entity));
    }

    /// <summary>Consulta el historial y las asignaturas aprobadas de un estudiante.</summary>
    [HttpGet("{estudianteId:guid}/historial")]
    [ProducesResponseType(typeof(HistorialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistorialResponse>> GetHistorial(Guid estudianteId, CancellationToken ct)
    {
        if (!await context.Estudiantes.AnyAsync(e => e.Id == estudianteId, ct)) return NotFound();
        var historial = await context.Historiales.AsNoTracking().Include(h => h.Asignaturas)
            .FirstOrDefaultAsync(h => h.EstudianteId == estudianteId, ct);
        return historial is null ? NotFound() : Ok(ToResponse(historial));
    }

    /// <summary>Crea o reemplaza el historial académico del estudiante.</summary>
    [HttpPut("{estudianteId:guid}/historial")]
    [ProducesResponseType(typeof(HistorialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistorialResponse>> GuardarHistorial(
        Guid estudianteId, HistorialWriteRequest request, CancellationToken ct)
    {
        var student = await context.Estudiantes.Include(e => e.Historial)
            .ThenInclude(h => h!.Asignaturas).FirstOrDefaultAsync(e => e.Id == estudianteId, ct);
        if (student is null) return NotFound();
        var error = ValidarHistorial(request, student.Tipo);
        if (error is not null) return BadRequest(Problem(400, error));
        if (student.Historial is null) student.Historial = CrearHistorial(student, request);
        else ActualizarHistorial(student.Historial, request);
        student.ModificadoPor = ActorActual();
        student.ModificadoEn = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(ToResponse(student.Historial));
    }

    private static string? ValidarEstudiante(EstudianteWriteRequest request, bool requireHistory = true)
    {
        if (string.IsNullOrWhiteSpace(request.Cedula) || string.IsNullOrWhiteSpace(request.Nombres)
            || string.IsNullOrWhiteSpace(request.Apellidos))
            return "Identificación, nombres y apellidos son obligatorios.";
        if (!Enum.IsDefined(request.TipoIdentificacion) || !Enum.IsDefined(request.Tipo))
            return "El tipo de identificación o estudiante no es válido.";
        var id = request.Cedula.Trim();
        if (request.TipoIdentificacion == TipoIdentificacion.Cedula && !CedulaValida(id))
            return "La cédula debe tener 10 dígitos y un dígito verificador válido.";
        if (request.TipoIdentificacion == TipoIdentificacion.Pasaporte
            && !Regex.IsMatch(id, "^[A-Za-z0-9]{5,20}$", RegexOptions.CultureInvariant))
            return "El pasaporte debe contener entre 5 y 20 caracteres alfanuméricos.";
        if (requireHistory && request.Tipo == TipoEstudiante.Interno && request.Historial is null)
            return "El registro de un estudiante interno requiere su historial académico.";
        return request.Historial is null ? null : ValidarHistorial(request.Historial, request.Tipo);
    }

    private static bool CedulaValida(string id)
    {
        if (id.Length != 10 || !id.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var digit = id[i] - '0';
            if (i % 2 == 0) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
        }
        return (10 - sum % 10) % 10 == id[9] - '0';
    }

    private static string? ValidarHistorial(HistorialWriteRequest request, TipoEstudiante tipo)
    {
        if (request.Asignaturas is null) return "La lista de asignaturas aprobadas es obligatoria.";
        if (tipo == TipoEstudiante.Interno && request.CarreraOrigenId is null)
            return "La carrera de origen es obligatoria para un estudiante interno.";
        if (tipo == TipoEstudiante.Interno && request.NivelActual is null or <= 0)
            return "El nivel actual debe ser mayor que cero.";
        if (request.Asignaturas!.Any(a => string.IsNullOrWhiteSpace(a.Nombre) || string.IsNullOrWhiteSpace(a.Periodo)
            || a.Nota is < 0 or > 10 || a.Creditos is <= 0 or > 999.99m || a.Horas is <= 0
            || (tipo == TipoEstudiante.Interno && a.AsignaturaId is null)))
            return "Cada asignatura interna requiere ID de catálogo, nombre, período, nota entre 0 y 10, créditos de 0.01 a 999.99 y horas positivas si se informan.";
        var ids = request.Asignaturas.Where(a => a.AsignaturaId.HasValue).Select(a => a.AsignaturaId!.Value).ToList();
        return ids.Count == ids.Distinct().Count() ? null : "Una asignatura del catálogo no puede repetirse en el historial.";
    }

    private static HistorialAcademico CrearHistorial(Estudiante student, HistorialWriteRequest request)
    {
        var historial = new HistorialAcademico { Estudiante = student };
        ActualizarHistorial(historial, request);
        return historial;
    }

    private static void ActualizarHistorial(HistorialAcademico historial, HistorialWriteRequest request)
    {
        historial.CarreraOrigenId = request.CarreraOrigenId;
        historial.CarreraOrigenNombre = Normalizar(request.CarreraOrigenNombre);
        historial.InstitucionOrigen = Normalizar(request.InstitucionOrigen);
        historial.NivelActual = request.NivelActual;
        historial.ActualizadoEn = DateTime.UtcNow;
        var requestedIds = request.Asignaturas!.Where(a => a.AsignaturaId.HasValue)
            .Select(a => a.AsignaturaId!.Value).ToHashSet();
        foreach (var old in historial.Asignaturas.Where(a => a.AsignaturaId.HasValue
                     && !requestedIds.Contains(a.AsignaturaId.Value)).ToList())
            historial.Asignaturas.Remove(old);
        foreach (var item in request.Asignaturas!)
        {
            var course = item.AsignaturaId.HasValue
                ? historial.Asignaturas.FirstOrDefault(a => a.AsignaturaId == item.AsignaturaId) : null;
            if (course is null) { course = new AsignaturaCursada(); historial.Asignaturas.Add(course); }
            course.AsignaturaId = item.AsignaturaId;
            course.Codigo = Normalizar(item.Codigo);
            course.Nombre = item.Nombre.Trim();
            course.Creditos = item.Creditos;
            course.Horas = item.Horas;
            course.Nota = item.Nota;
            course.Periodo = item.Periodo.Trim();
        }
    }

    private async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { return false; }
    }
    private static bool IsDuplicate(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 2601 or 2627 }) return true;
        return false;
    }
    private string ActorActual() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "desconocido";
    private static string NormalizarId(string id) => id.Trim().ToUpperInvariant();
    private static string MaskIdentification(string id) => id.Length <= 4 ? new string('*' , id.Length) : id[..2] + new string('*', id.Length - 4) + id[^2..];
    private static string? Normalizar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static EstudianteResponse ToResponse(Estudiante e) => new(e.Id, e.Cedula, e.TipoIdentificacion,
        e.Nombres, e.Apellidos, e.Tipo, e.CreadoPor, e.CreadoEn, e.ModificadoPor, e.ModificadoEn,
        e.Historial is null ? null : ToResponse(e.Historial));
    private static HistorialResponse ToResponse(HistorialAcademico h) => new(h.Id, h.EstudianteId,
        h.CarreraOrigenId, h.CarreraOrigenNombre, h.InstitucionOrigen, h.NivelActual, h.ActualizadoEn,
        h.Asignaturas.OrderBy(a => a.Periodo).ThenBy(a => a.Nombre)
            .Select(a => new AsignaturaCursadaResponse(a.Id, a.AsignaturaId, a.Codigo, a.Nombre,
                a.Creditos, a.Horas, a.Nota, a.Periodo)).ToList());
    private static ProblemDetails Problem(int status, string detail) => new()
    { Status = status, Title = status == 400 ? "Solicitud inválida" : "Conflicto", Detail = detail };
}
