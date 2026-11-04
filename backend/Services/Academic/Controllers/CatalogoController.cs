using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using AcademicService.Data;
using AcademicService.Contracts;
using AcademicService.Models;
using BuildingBlocks.Roles;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/catalogo")]
[Authorize(Roles = $"{AppRoles.Administrador},{AppRoles.Profesor}")]
public class CatalogoController : ControllerBase
{
    private readonly AcademicDbContext _context;

    public CatalogoController(AcademicDbContext context)
    {
        _context = context;
    }

    // POST: api/v1/catalogo/carreras
    /// <summary>Crea una carrera. Solo Administrador.</summary>
    [HttpPost("carreras")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(CarreraCatalogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CarreraCatalogoResponse>> CrearCarrera(
        CarreraWriteRequest request,
        CancellationToken ct)
    {
        var codigo = request.Codigo.Trim();
        var nombre = request.Nombre.Trim();
        if (codigo.Length == 0 || nombre.Length == 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "Codigo y nombre son obligatorios."));
        }

        if (await _context.Carreras.AnyAsync(c => c.Codigo.ToUpper() == codigo.ToUpper(), ct))
        {
            return Conflicto("Ya existe una carrera con ese código.");
        }

        var carrera = new Carrera { Codigo = codigo, Nombre = nombre, EsDemo = request.EsDemo };
        _context.Carreras.Add(carrera);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe una carrera con ese código.");
        }

        var response = ToResponse(carrera);
        return Created("/api/v1/catalogo/carreras", response);
    }

    // PUT: api/v1/catalogo/carreras/{carreraId}
    /// <summary>Actualiza una carrera. Solo Administrador.</summary>
    [HttpPut("carreras/{carreraId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(CarreraCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CarreraCatalogoResponse>> ActualizarCarrera(
        Guid carreraId,
        CarreraWriteRequest request,
        CancellationToken ct)
    {
        var carrera = await _context.Carreras.FirstOrDefaultAsync(c => c.Id == carreraId, ct);
        if (carrera is null)
        {
            return NotFound();
        }

        var codigo = request.Codigo.Trim();
        var nombre = request.Nombre.Trim();
        if (codigo.Length == 0 || nombre.Length == 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "Codigo y nombre son obligatorios."));
        }

        if (await _context.Carreras.AnyAsync(
                c => c.Id != carreraId && c.Codigo.ToUpper() == codigo.ToUpper(), ct))
        {
            return Conflicto("Ya existe una carrera con ese código.");
        }

        carrera.Codigo = codigo;
        carrera.Nombre = nombre;
        carrera.EsDemo = request.EsDemo;
        var mallasDeCarrera = await _context.Mallas
            .Where(m => m.CarreraId == carreraId)
            .ToListAsync(ct);
        foreach (var malla in mallasDeCarrera)
        {
            MarcarModificacion(malla);
        }
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe una carrera con ese código.");
        }

        return Ok(ToResponse(carrera));
    }

    // POST: api/v1/catalogo/carreras/{carreraId}/mallas
    /// <summary>Crea una versión de malla en estado Borrador. Solo Administrador.</summary>
    [HttpPost("carreras/{carreraId:guid}/mallas")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(MallaCatalogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MallaCatalogoResponse>> CrearMalla(
        Guid carreraId,
        MallaWriteRequest request,
        CancellationToken ct)
    {
        if (!await _context.Carreras.AnyAsync(c => c.Id == carreraId, ct))
        {
            return NotFound();
        }

        var version = request.Version.Trim();
        if (version.Length == 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "La versión de la malla es obligatoria."));
        }

        if (await _context.Mallas.AnyAsync(
                m => m.CarreraId == carreraId && m.Version.ToUpper() == version.ToUpper(), ct))
        {
            return Conflicto("Ya existe esa versión de malla para la carrera.");
        }

        var malla = new MallaVersion
        {
            CarreraId = carreraId,
            Version = version,
            Estado = EstadoMalla.Borrador,
            Vigente = false,
            ModificadoPor = ActorActual(),
            ModificadoEn = DateTime.UtcNow
        };
        _context.Mallas.Add(malla);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe esa versión de malla para la carrera.");
        }

        return Created($"/api/v1/catalogo/carreras/{carreraId}/mallas", ToResponse(malla));
    }

    // PUT: api/v1/catalogo/mallas/{mallaId}
    /// <summary>Actualiza la versión de una malla. Solo Administrador.</summary>
    [HttpPut("mallas/{mallaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(MallaCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MallaCatalogoResponse>> ActualizarMalla(
        Guid mallaId,
        MallaWriteRequest request,
        CancellationToken ct)
    {
        var malla = await _context.Mallas.FirstOrDefaultAsync(m => m.Id == mallaId, ct);
        if (malla is null)
        {
            return NotFound();
        }

        var version = request.Version.Trim();
        if (version.Length == 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "La versión de la malla es obligatoria."));
        }

        if (await _context.Mallas.AnyAsync(
                m => m.Id != mallaId && m.CarreraId == malla.CarreraId
                    && m.Version.ToUpper() == version.ToUpper(), ct))
        {
            return Conflicto("Ya existe esa versión de malla para la carrera.");
        }

        malla.Version = version;
        MarcarModificacion(malla);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe esa versión de malla para la carrera.");
        }

        return Ok(ToResponse(malla));
    }

    // POST: api/v1/catalogo/mallas/{mallaId}/niveles
    /// <summary>Crea un nivel dentro de una malla. Solo Administrador.</summary>
    [HttpPost("mallas/{mallaId:guid}/niveles")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(NivelCatalogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NivelCatalogoResponse>> CrearNivel(
        Guid mallaId,
        NivelWriteRequest request,
        CancellationToken ct)
    {
        var malla = await _context.Mallas.FirstOrDefaultAsync(m => m.Id == mallaId, ct);
        if (malla is null)
        {
            return NotFound();
        }

        if (request.Numero <= 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "El número de nivel debe ser mayor que cero."));
        }

        if (await _context.Niveles.AnyAsync(
                n => n.MallaVersionId == mallaId && n.Numero == request.Numero, ct))
        {
            return Conflicto("Ya existe ese número de nivel en la malla.");
        }

        var nivel = new Nivel { MallaVersionId = mallaId, Numero = request.Numero };
        MarcarModificacion(malla);
        _context.Niveles.Add(nivel);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe ese número de nivel en la malla.");
        }

        return Created($"/api/v1/catalogo/mallas/{mallaId}/niveles", ToResponse(nivel));
    }

    // PUT: api/v1/catalogo/niveles/{nivelId}
    /// <summary>Actualiza el número de un nivel. Solo Administrador.</summary>
    [HttpPut("niveles/{nivelId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(NivelCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NivelCatalogoResponse>> ActualizarNivel(
        Guid nivelId,
        NivelWriteRequest request,
        CancellationToken ct)
    {
        var nivel = await _context.Niveles.FirstOrDefaultAsync(n => n.Id == nivelId, ct);
        if (nivel is null)
        {
            return NotFound();
        }

        if (request.Numero <= 0)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest,
                "El número de nivel debe ser mayor que cero."));
        }

        if (await _context.Niveles.AnyAsync(
                n => n.Id != nivelId && n.MallaVersionId == nivel.MallaVersionId
                    && n.Numero == request.Numero, ct))
        {
            return Conflicto("Ya existe ese número de nivel en la malla.");
        }

        var malla = await _context.Mallas.FirstOrDefaultAsync(m => m.Id == nivel.MallaVersionId, ct);
        if (malla is not null)
        {
            MarcarModificacion(malla);
        }
        nivel.Numero = request.Numero;
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe ese número de nivel en la malla.");
        }

        return Ok(ToResponse(nivel));
    }

    // POST: api/v1/catalogo/mallas/{mallaId}/asignaturas
    /// <summary>Crea una asignatura dentro de un nivel de la malla. Solo Administrador.</summary>
    [HttpPost("mallas/{mallaId:guid}/asignaturas")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(AsignaturaCatalogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignaturaCatalogoResponse>> CrearAsignatura(
        Guid mallaId,
        AsignaturaWriteRequest request,
        CancellationToken ct)
    {
        var malla = await _context.Mallas.FirstOrDefaultAsync(m => m.Id == mallaId, ct);
        if (malla is null)
        {
            return NotFound();
        }

        var nivel = await _context.Niveles.FirstOrDefaultAsync(
            n => n.Id == request.NivelId && n.MallaVersionId == mallaId, ct);
        if (nivel is null)
        {
            return NotFound(Problema(StatusCodes.Status404NotFound,
                "El nivel no existe en la malla indicada."));
        }

        var error = ValidarAsignatura(request);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var codigo = request.Codigo.Trim();
        if (await _context.Asignaturas.AnyAsync(
                a => a.MallaVersionId == mallaId && a.Codigo.ToUpper() == codigo.ToUpper(), ct))
        {
            return Conflicto("Ya existe ese código de asignatura en la malla.");
        }

        var asignatura = new Asignatura
        {
            MallaVersionId = mallaId,
            NivelId = nivel.Id,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Creditos = request.Creditos,
            Horas = request.Horas,
            UnidadOrganizacional = NormalizarOpcional(request.UnidadOrganizacional)
        };
        MarcarModificacion(malla);
        _context.Asignaturas.Add(asignatura);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe ese código de asignatura en la malla.");
        }

        return Created($"/api/v1/catalogo/mallas/{mallaId}/asignaturas",
            ToResponse(asignatura, nivel.Numero));
    }

    // PUT: api/v1/catalogo/asignaturas/{asignaturaId}
    /// <summary>Actualiza una asignatura sin cambiarla de malla. Solo Administrador.</summary>
    [HttpPut("asignaturas/{asignaturaId:guid}")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(AsignaturaCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignaturaCatalogoResponse>> ActualizarAsignatura(
        Guid asignaturaId,
        AsignaturaWriteRequest request,
        CancellationToken ct)
    {
        var asignatura = await _context.Asignaturas.FirstOrDefaultAsync(a => a.Id == asignaturaId, ct);
        if (asignatura is null)
        {
            return NotFound();
        }

        var nivel = await _context.Niveles.FirstOrDefaultAsync(
            n => n.Id == request.NivelId && n.MallaVersionId == asignatura.MallaVersionId, ct);
        if (nivel is null)
        {
            return NotFound(Problema(StatusCodes.Status404NotFound,
                "El nivel no existe en la malla de la asignatura."));
        }

        var malla = await _context.Mallas.FirstOrDefaultAsync(
            m => m.Id == asignatura.MallaVersionId, ct);
        if (malla is null)
        {
            return NotFound();
        }

        var error = ValidarAsignatura(request);
        if (error is not null)
        {
            return BadRequest(Problema(StatusCodes.Status400BadRequest, error));
        }

        var codigo = request.Codigo.Trim();
        if (await _context.Asignaturas.AnyAsync(
                a => a.Id != asignaturaId && a.MallaVersionId == asignatura.MallaVersionId
                    && a.Codigo.ToUpper() == codigo.ToUpper(), ct))
        {
            return Conflicto("Ya existe ese código de asignatura en la malla.");
        }

        asignatura.NivelId = nivel.Id;
        asignatura.Codigo = codigo;
        asignatura.Nombre = request.Nombre.Trim();
        asignatura.Creditos = request.Creditos;
        asignatura.Horas = request.Horas;
        asignatura.UnidadOrganizacional = NormalizarOpcional(request.UnidadOrganizacional);
        MarcarModificacion(malla);
        if (!await GuardarAsync(ct))
        {
            return Conflicto("Ya existe ese código de asignatura en la malla.");
        }

        return Ok(ToResponse(asignatura, nivel.Numero));
    }

    // POST: api/v1/catalogo/mallas/{mallaId}/publicar
    /// <summary>Publica una malla y deja en borrador la anterior publicada de la carrera.</summary>
    [HttpPost("mallas/{mallaId:guid}/publicar")]
    [Authorize(Roles = AppRoles.Administrador)]
    [ProducesResponseType(typeof(MallaCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MallaCatalogoResponse>> PublicarMalla(
        Guid mallaId,
        CancellationToken ct)
    {
        var malla = await _context.Mallas.FirstOrDefaultAsync(m => m.Id == mallaId, ct);
        if (malla is null)
        {
            return NotFound();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var anteriores = await _context.Mallas
            .Where(m => m.CarreraId == malla.CarreraId && m.Id != mallaId
                && (m.Estado == EstadoMalla.Publicada || m.Vigente))
            .ToListAsync(ct);
        foreach (var anterior in anteriores)
        {
            anterior.Estado = EstadoMalla.Borrador;
            anterior.Vigente = false;
            MarcarModificacion(anterior);
        }

        if (anteriores.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        malla.Estado = EstadoMalla.Publicada;
        malla.Vigente = true;
        MarcarModificacion(malla);
        if (!await GuardarAsync(ct))
        {
            await transaction.RollbackAsync(ct);
            return Conflicto("No se pudo publicar la malla por una restricción de unicidad.");
        }

        await transaction.CommitAsync(ct);
        return Ok(ToResponse(malla));
    }

    // GET: api/v1/catalogo/carreras
    /// <summary>Lista las carreras disponibles para consultar.</summary>
    [HttpGet("carreras")]
    [ProducesResponseType(typeof(IReadOnlyList<CarreraCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CarreraCatalogoResponse>>> GetCarreras(CancellationToken ct)
    {
        var carreras = await _context.Carreras
            .OrderBy(c => c.EsDemo)
            .ThenBy(c => c.Nombre)
            .Select(c => new CarreraCatalogoResponse(
                c.Id,
                c.Codigo,
                c.Nombre,
                c.EsDemo))
            .ToListAsync(ct);

        return Ok(carreras);
    }

    // GET: api/v1/catalogo/carreras/{carreraId}/mallas
    /// <summary>Lista las versiones de malla de una carrera.</summary>
    /// <param name="carreraId">Identificador de la carrera.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("carreras/{carreraId:guid}/mallas")]
    [ProducesResponseType(typeof(IReadOnlyList<MallaCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MallaCatalogoResponse>>> GetMallasPorCarrera(
        Guid carreraId,
        CancellationToken ct)
    {
        var existeCarrera = await _context.Carreras.AnyAsync(c => c.Id == carreraId, ct);
        if (!existeCarrera)
        {
            return NotFound();
        }

        var mallas = await _context.Mallas
            .Where(m => m.CarreraId == carreraId)
            .OrderByDescending(m => m.Vigente)
            .ThenBy(m => m.Version)
            .Select(m => new MallaCatalogoResponse(
                m.Id,
                m.CarreraId,
                m.Version,
                m.Vigente,
                m.Estado.ToString(),
                m.ModificadoPor,
                m.ModificadoEn))
            .ToListAsync(ct);

        return Ok(mallas);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/niveles
    /// <summary>Lista los niveles de una versión de malla.</summary>
    /// <param name="mallaId">Identificador de la versión de malla.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("mallas/{mallaId:guid}/niveles")]
    [ProducesResponseType(typeof(IReadOnlyList<NivelCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NivelCatalogoResponse>>> GetNivelesPorMalla(
        Guid mallaId,
        CancellationToken ct)
    {
        var existeMalla = await _context.Mallas.AnyAsync(m => m.Id == mallaId, ct);
        if (!existeMalla)
        {
            return NotFound();
        }

        var niveles = await _context.Niveles
            .Where(n => n.MallaVersionId == mallaId)
            .OrderBy(n => n.Numero)
            .Select(n => new NivelCatalogoResponse(
                n.Id,
                n.MallaVersionId,
                n.Numero))
            .ToListAsync(ct);

        return Ok(niveles);
    }

    // GET: api/v1/catalogo/mallas/{mallaId}/asignaturas?nivelId=...&nivel=...&search=...
    /// <summary>Lista las asignaturas de una malla con filtros opcionales.</summary>
    /// <param name="mallaId">Identificador de la versión de malla.</param>
    /// <param name="nivelId">Filtra por el identificador del nivel.</param>
    /// <param name="nivel">Filtra por el número del nivel.</param>
    /// <param name="search">Busca por código, nombre o unidad organizacional, sin distinguir mayúsculas.</param>
    /// <param name="ct">Token para cancelar la solicitud.</param>
    [HttpGet("mallas/{mallaId:guid}/asignaturas")]
    [ProducesResponseType(typeof(IReadOnlyList<AsignaturaCatalogoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AsignaturaCatalogoResponse>>> GetAsignaturas(
        Guid mallaId,
        [FromQuery] Guid? nivelId,
        [FromQuery] int? nivel,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var existeMalla = await _context.Mallas.AnyAsync(m => m.Id == mallaId, ct);
        if (!existeMalla)
        {
            return NotFound();
        }

        var query = _context.Asignaturas
            .AsNoTracking()
            .Where(a => a.MallaVersionId == mallaId);

        if (nivelId.HasValue)
        {
            query = query.Where(a => a.NivelId == nivelId.Value);
        }

        if (nivel.HasValue)
        {
            query = query.Where(a => a.Nivel != null && a.Nivel.Numero == nivel.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Nombre.ToLower().Contains(term) ||
                a.Codigo.ToLower().Contains(term) ||
                (a.UnidadOrganizacional != null && a.UnidadOrganizacional.ToLower().Contains(term)));
        }

        var asignaturas = await query
            .OrderBy(a => a.Nivel != null ? a.Nivel.Numero : 0)
            .ThenBy(a => a.Codigo)
            .Select(a => new AsignaturaCatalogoResponse(
                a.Id,
                a.MallaVersionId,
                a.NivelId,
                a.Nivel != null ? a.Nivel.Numero : 0,
                a.Codigo,
                a.Nombre,
                a.Creditos,
                a.Horas,
                a.UnidadOrganizacional))
            .ToListAsync(ct);

        return Ok(asignaturas);
    }

    private static CarreraCatalogoResponse ToResponse(Carrera carrera) =>
        new(carrera.Id, carrera.Codigo, carrera.Nombre, carrera.EsDemo);

    private static MallaCatalogoResponse ToResponse(MallaVersion malla) =>
        new(malla.Id, malla.CarreraId, malla.Version, malla.Vigente,
            malla.Estado.ToString(), malla.ModificadoPor, malla.ModificadoEn);

    private static NivelCatalogoResponse ToResponse(Nivel nivel) =>
        new(nivel.Id, nivel.MallaVersionId, nivel.Numero);

    private static AsignaturaCatalogoResponse ToResponse(Asignatura asignatura, int nivelNumero) =>
        new(asignatura.Id, asignatura.MallaVersionId, asignatura.NivelId, nivelNumero,
            asignatura.Codigo, asignatura.Nombre, asignatura.Creditos, asignatura.Horas,
            asignatura.UnidadOrganizacional);

    private static string? ValidarAsignatura(AsignaturaWriteRequest request)
    {
        if (request.NivelId == Guid.Empty)
        {
            return "El nivel es obligatorio.";
        }
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return "Código y nombre son obligatorios.";
        }
        if (request.Creditos <= 0 || request.Creditos > 999.99m)
        {
            return "Los créditos deben ser mayores que cero y no superar 999.99.";
        }
        if (request.Horas <= 0)
        {
            return "Las horas deben ser mayores que cero.";
        }

        return null;
    }

    private static string? NormalizarOpcional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string ActorActual() =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "desconocido";

    private void MarcarModificacion(MallaVersion malla)
    {
        malla.ModificadoPor = ActorActual();
        malla.ModificadoEn = DateTime.UtcNow;
    }

    private async Task<bool> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
        {
            return false;
        }
    }

    private static bool EsViolacionDeUnicidad(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627))
            {
                return true;
            }
        }

        return false;
    }

    private ConflictObjectResult Conflicto(string detail) =>
        Conflict(Problema(StatusCodes.Status409Conflict, detail));

    private static ProblemDetails Problema(int status, string detail) => new()
    {
        Status = status,
        Title = status == StatusCodes.Status409Conflict ? "Conflicto de datos" : "Solicitud no válida",
        Detail = detail
    };
}
