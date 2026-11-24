using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcademicService.Contracts;
using AcademicService.Data;
using AcademicService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/catalogo/mallas/{mallaId:guid}/planes-analiticos")]
[Authorize(Roles = AppRoles.Administrador)]
public sealed class PlanesMasivosController(AcademicDbContext db) : ControllerBase
{
    public sealed record Entrada(string Archivo, Guid? AsignaturaId, PlanAnaliticoPreview? Datos, string? Error,
        bool Seleccionado = true, bool Reemplazar = false, bool TienePlan = false);

    [HttpPost("analizar")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Analizar(Guid mallaId, IFormFile archivo, CancellationToken ct)
    {
        var malla = await db.Mallas.Include(x => x.Carrera).SingleOrDefaultAsync(x => x.Id == mallaId, ct);
        if (malla is null) return NotFound();
        try
        {
            var files = await LeerZip(archivo, ct);
            var materias = await db.Asignaturas.Where(x => x.MallaVersionId == mallaId).ToListAsync(ct);
            var conPlan = await db.PlanesAnaliticos.Where(p => materias.Select(x => x.Id).Contains(p.AsignaturaId)).Select(p => p.AsignaturaId).ToListAsync(ct);
            var entradas = new List<Entrada>();
            foreach (var (nombre, bytes) in files)
            {
                try
                {
                    var preview = await Parse(nombre, bytes, ct);
                    if (!preview.Codigo.Contains($"-{malla.Carrera!.Codigo}-", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("La carrera del PDF no coincide con la carrera de la malla seleccionada.");
                    var matches = materias.Where(x => string.Equals(x.Codigo, preview.Codigo, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (matches.Length == 0) matches = materias.Where(x => Clave(x.Nombre) == Clave(preview.Nombre)).ToArray();
                    if (matches.Length > 1) throw new InvalidDataException("Hay varias asignaturas coincidentes. Selecciona la asignatura en la vista previa.");
                    var match = matches.SingleOrDefault();
                    entradas.Add(new(nombre, match?.Id, preview, null, true, false, match is not null && conPlan.Contains(match.Id)));
                }
                catch (InvalidDataException ex) { entradas.Add(new(nombre, null, null, ex.Message, false)); }
            }
            return Ok(new { entradas, mensaje = "Revisa las asignaturas, niveles y contenidos antes de confirmar. Los planes existentes requieren autorización de reemplazo." });
        }
        catch (InvalidDataException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPost("confirmar")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Confirmar(Guid mallaId, IFormFile archivo, [FromForm] string datos, CancellationToken ct)
    {
        var malla = await db.Mallas.Include(x => x.Carrera).SingleOrDefaultAsync(x => x.Id == mallaId, ct);
        if (malla is null) return NotFound();
        if (malla.Estado != EstadoMalla.Borrador) return Conflict(new { mensaje = "Carga los planes en una malla borrador antes de publicarla." });
        try
        {
            var files = await LeerZip(archivo, ct);
            var seleccion = JsonSerializer.Deserialize<Entrada[]>(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Where(x => x.Seleccionado).ToArray() ?? [];
            if (seleccion.Length == 0) return BadRequest(new { mensaje = "Selecciona al menos un plan válido." });
            if (seleccion.Select(x => x.Archivo).Distinct().Count() != seleccion.Length) throw new InvalidDataException("Un archivo no puede seleccionarse dos veces.");
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in seleccion)
            {
                if (!files.TryGetValue(item.Archivo, out var bytes)) throw new InvalidDataException("Un archivo seleccionado no existe en el ZIP.");
                var original = await Parse(item.Archivo, bytes, ct);
                var p = item.Datos;
                if (!original.Codigo.Contains($"-{malla.Carrera!.Codigo}-", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("El ZIP contiene planes de otra carrera.");
                if (p is null || string.IsNullOrWhiteSpace(p.Nombre) || p.Nombre.Length > 200 || string.IsNullOrWhiteSpace(p.Codigo) || p.Codigo.Length > 30
                    || p.Creditos is <= 0 or > 999.99m || p.Horas <= 0 || p.Nivel is null or < 1 or > 20 || p.Temas is null || p.Temas.Count == 0
                    || p.Temas.Any(t => string.IsNullOrWhiteSpace(t.Nombre) || t.Nombre.Length > 300 || t.Subtemas is null || t.Subtemas.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 300)))
                    throw new InvalidDataException($"Completa nombre, código, nivel, créditos, horas y contenidos de {item.Archivo}.");
                if (!usados.Add(item.AsignaturaId?.ToString() ?? Clave(p.Codigo))) throw new InvalidDataException("Dos archivos intentan actualizar la misma asignatura.");
            }
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resultados = new List<object>();
            foreach (var item in seleccion)
            {
                var p = item.Datos!;
                var materia = item.AsignaturaId.HasValue ? await db.Asignaturas.Include(x => x.Temas).ThenInclude(x => x.Subtemas).SingleOrDefaultAsync(x => x.Id == item.AsignaturaId && x.MallaVersionId == mallaId, ct) : null;
                if (item.AsignaturaId.HasValue && materia is null) throw new InvalidDataException("Una asignatura seleccionada no pertenece a esta malla.");
                if (await db.Asignaturas.AnyAsync(x => x.MallaVersionId == mallaId && x.Codigo == p.Codigo && (materia == null || x.Id != materia.Id), ct))
                    throw new InvalidDataException($"El código {p.Codigo} ya está ocupado. Asocia el archivo con la asignatura existente.");
                var plan = materia is null ? null : await db.PlanesAnaliticos.SingleOrDefaultAsync(x => x.AsignaturaId == materia.Id, ct);
                if (plan is not null && !item.Reemplazar)
                {
                    if (plan.Contenido.SequenceEqual(files[item.Archivo])) { resultados.Add(new { item.Archivo, estado = "Sin cambios" }); continue; }
                    throw new InvalidDataException($"Autoriza el reemplazo del plan existente de {p.Nombre}.");
                }
                var nivel = await db.Niveles.SingleOrDefaultAsync(x => x.MallaVersionId == mallaId && x.Numero == p.Nivel, ct)
                    ?? db.Niveles.Local.SingleOrDefault(x => x.MallaVersionId == mallaId && x.Numero == p.Nivel);
                if (nivel is null) { nivel = new Nivel { MallaVersionId = mallaId, Numero = p.Nivel!.Value }; db.Niveles.Add(nivel); }
                if (materia is null) { materia = new Asignatura { MallaVersionId = mallaId }; db.Asignaturas.Add(materia); }
                materia.NivelId = nivel.Id; materia.Nombre = p.Nombre.Trim(); materia.Codigo = p.Codigo.Trim(); materia.Creditos = p.Creditos; materia.Horas = p.Horas;
                materia.UnidadOrganizacional = p.UnidadOrganizacional;
                db.Temas.RemoveRange(materia.Temas);
                foreach (var (tema, orden) in p.Temas.Select((t, i) => (t, i + 1)))
                    db.Temas.Add(new Tema { AsignaturaId = materia.Id, Nombre = tema.Nombre.Trim(), Orden = orden,
                        Subtemas = tema.Subtemas.Distinct(StringComparer.OrdinalIgnoreCase).Select((s, i) => new Subtema { Nombre = s.Trim(), Orden = i + 1 }).ToList() });
                if (plan is null) { plan = new PlanAnalitico { AsignaturaId = materia.Id }; db.PlanesAnaliticos.Add(plan); }
                plan.Contenido = files[item.Archivo]; plan.NombreArchivo = Path.GetFileName(item.Archivo); plan.SubidoEn = DateTime.UtcNow;
                plan.SubidoPor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "administrador";
                resultados.Add(new { item.Archivo, estado = "Importado" });
            }
            malla.ModificadoEn = DateTime.UtcNow; malla.ModificadoPor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Ok(new { resultados });
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException) { return BadRequest(new { mensaje = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { mensaje = "El catálogo cambió o hay códigos duplicados. Revisa la vista previa antes de reintentar." }); }
    }

    private static async Task<PlanAnaliticoPreview> Parse(string nombre, byte[] bytes, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes);
        return await PlanesAnaliticosController.LeerPdf(new FormFile(stream, 0, bytes.Length, "archivo", Path.GetFileName(nombre)), ct);
    }
    public static string Clave(string value) => Regex.Replace(new string(value.Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant(), @"[^A-Z0-9]", "");

    private static async Task<Dictionary<string, byte[]>> LeerZip(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0 || archivo.Length > 30 * 1024 * 1024 || !archivo.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selecciona un ZIP de hasta 30 MB.");
        using var memory = new MemoryStream(); await archivo.CopyToAsync(memory, ct); memory.Position = 0;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
        if (zip.Entries.Count > 400 || zip.Entries.Sum(x => x.Length) > 150 * 1024 * 1024) throw new InvalidDataException("El contenido del ZIP supera el límite permitido.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries.Where(x => x.Name.Length > 0 && !x.FullName.StartsWith("__MACOSX/")))
        {
            if (entry.FullName.Replace('\\', '/').Split('/').Contains("..") || entry.FullName.StartsWith('/') || entry.FullName.Contains(':')) throw new InvalidDataException("El ZIP contiene rutas no válidas.");
            if (!entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Length == 0 || entry.Length > 10 * 1024 * 1024 || files.Count >= 200) throw new InvalidDataException("Se permiten hasta 200 PDF de 10 MB cada uno.");
            using var output = new MemoryStream(); await using var input = entry.Open(); await input.CopyToAsync(output, ct);
            if (!files.TryAdd(entry.FullName, output.ToArray())) throw new InvalidDataException("El ZIP contiene nombres de archivos duplicados.");
        }
        if (files.Count == 0) throw new InvalidDataException("El ZIP no contiene PDF.");
        return files;
    }
}
