using System.Security.Claims;
using System.Text.RegularExpressions;
using AcademicService.Contracts;
using AcademicService.Data;
using AcademicService.Models;
using BuildingBlocks.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AcademicService.Controllers;

[ApiController]
[Route("api/v1/catalogo/asignaturas/{asignaturaId:guid}/plan-analitico")]
[Authorize(Roles = AppRoles.Administrador)]
public sealed class PlanesAnaliticosController(AcademicDbContext db) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;

    [HttpGet("archivo")]
    public async Task<IActionResult> Descargar(Guid asignaturaId, CancellationToken ct)
    {
        var plan = await db.PlanesAnaliticos.AsNoTracking().SingleOrDefaultAsync(p => p.AsignaturaId == asignaturaId, ct);
        return plan is null ? NotFound("Esta asignatura aún no tiene un plan analítico cargado.") : File(plan.Contenido, "application/pdf", plan.NombreArchivo);
    }

    [HttpPost("analizar")]
    [RequestSizeLimit(MaxBytes)]
    public async Task<ActionResult<PlanAnaliticoPreview>> Analizar(Guid asignaturaId, IFormFile archivo, CancellationToken ct)
    {
        if (!await db.Asignaturas.AnyAsync(a => a.Id == asignaturaId, ct)) return NotFound("No existe la asignatura.");
        try { return Ok(await LeerPdf(archivo, ct)); }
        catch (InvalidDataException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("confirmar")]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Confirmar(Guid asignaturaId, IFormFile archivo, [FromForm] string datos, CancellationToken ct)
    {
        var asignatura = await db.Asignaturas.Include(a => a.Temas).ThenInclude(t => t.Subtemas)
            .SingleOrDefaultAsync(a => a.Id == asignaturaId, ct);
        if (asignatura is null) return NotFound("No existe la asignatura.");
        PlanAnaliticoPreview preview;
        try { preview = System.Text.Json.JsonSerializer.Deserialize<PlanAnaliticoPreview>(datos, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception) { return BadRequest("Los datos editados no tienen un formato válido."); }
        if (preview is null || string.IsNullOrWhiteSpace(preview.Nombre) || string.IsNullOrWhiteSpace(preview.Codigo) || preview.Creditos <= 0 || preview.Horas <= 0 || preview.Temas.Count == 0)
            return BadRequest("Completa nombre, código, créditos, horas y al menos un tema.");
        if (!ValidarArchivo(archivo, out var error)) return BadRequest(error);
        // Releer el archivo antes de persistir para aceptar únicamente PDF textual válido.
        try { await LeerPdf(archivo, ct); }
        catch (InvalidDataException ex) { return BadRequest(ex.Message); }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        asignatura.Nombre = preview.Nombre.Trim(); asignatura.Codigo = preview.Codigo.Trim();
        asignatura.Creditos = preview.Creditos; asignatura.Horas = preview.Horas;
        asignatura.UnidadOrganizacional = preview.UnidadOrganizacional?.Trim();
        db.Temas.RemoveRange(asignatura.Temas);
        var orden = 0;
        foreach (var item in preview.Temas.Where(t => !string.IsNullOrWhiteSpace(t.Nombre)))
        {
            var tema = new Tema { AsignaturaId = asignatura.Id, Nombre = item.Nombre.Trim(), Orden = ++orden };
            var subOrden = 0;
            foreach (var sub in item.Subtemas.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
                tema.Subtemas.Add(new Subtema { Nombre = sub.Trim(), Orden = ++subOrden });
            db.Temas.Add(tema);
        }
        var stored = await db.PlanesAnaliticos.SingleOrDefaultAsync(p => p.AsignaturaId == asignaturaId, ct);
        if (stored is null) db.PlanesAnaliticos.Add(new PlanAnalitico { AsignaturaId = asignaturaId, NombreArchivo = Path.GetFileName(archivo.FileName), Contenido = await ReadBytes(archivo, ct), SubidoPor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "administrador" });
        else { stored.NombreArchivo = Path.GetFileName(archivo.FileName); stored.Contenido = await ReadBytes(archivo, ct); stored.SubidoEn = DateTime.UtcNow; stored.SubidoPor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "administrador"; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return NoContent();
    }

    private static bool ValidarArchivo(IFormFile? file, out string error)
    {
        error = "";
        if (file is null || file.Length == 0) error = "Selecciona un archivo PDF.";
        else if (file.Length > MaxBytes) error = "El PDF supera el límite de 10 MB.";
        else if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) error = "El archivo debe tener extensión PDF.";
        return error.Length == 0;
    }

    private static async Task<byte[]> ReadBytes(IFormFile file, CancellationToken ct)
    { await using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); return stream.ToArray(); }

    private static async Task<PlanAnaliticoPreview> LeerPdf(IFormFile file, CancellationToken ct)
    {
        if (!ValidarArchivo(file, out var error)) throw new InvalidDataException(error);
        var bytes = await ReadBytes(file, ct);
        if (bytes.Length < 5 || System.Text.Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-") throw new InvalidDataException("El archivo no parece ser un PDF válido.");
        try
        {
            using var doc = PdfDocument.Open(new MemoryStream(bytes));
            if (doc.NumberOfPages > 50) throw new InvalidDataException("El PDF supera el máximo de 50 páginas.");
            var text = string.Join("\n", Enumerable.Range(1, doc.NumberOfPages).Select(i => UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(doc.GetPage(i))));
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("No se detectó texto seleccionable; el PDF parece escaneado. Se requiere OCR antes de cargarlo.");
            string Match(string pattern) => Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim();
            var nombre = Match(@"Nombre\s+de\s+la\s+asignatura\s*([\s\S]*?)\s+Carrera");
            var codigo = Match(@"Código\s+(UTA-FISEI-[A-Z0-9-]+)");
            var creditosText = Match(@"Créditos\s*/\s*Horas\s+(\d+(?:[.,]\d+)?)");
            var unidad = Match(@"Unidad\s+organizacional\s+curricular\s*([\s\S]*?)\s+Créditos\s*/\s*Horas");
            var hoursMatches = Regex.Matches(text, @"Total\s+de\s+horas\s+(\d{2,4})", RegexOptions.IgnoreCase);
            var hoursValue = Regex.Match(text, @"TOTAL\s+DE\s+HORAS\s+AL\s+SEMESTRE[\s\S]{0,500}?(\d{2,4})", RegexOptions.IgnoreCase).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(hoursValue) && hoursMatches.Count > 0) hoursValue = hoursMatches[^1].Groups[1].Value;
            var horas = int.TryParse(hoursValue, out var h) ? h : 0;
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(codigo)) throw new InvalidDataException("El formato del plan no coincide con los planes analíticos FISEI reconocidos.");
            var contentPage = Enumerable.Range(1, doc.NumberOfPages).Select(doc.GetPage)
                .FirstOrDefault(p => Regex.IsMatch(UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(p), @"III\s+CONTENIDO", RegexOptions.IgnoreCase));
            var temas = contentPage is null ? new List<PlanAnaliticoTema>() : ExtraerTemas(contentPage);
            if (temas.Count == 0) throw new InvalidDataException("Se obtuvo la información general, pero no se pudo interpretar la tabla de contenidos. Revisa que el PDF use el formato de plan analítico FISEI.");
            return new PlanAnaliticoPreview(Normalizar(nombre), codigo, decimal.TryParse(creditosText?.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0, horas, Normalizar(unidad), temas, new[] { "Revisa los campos extraídos y corrige cualquier error antes de confirmar." });
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se pudo leer el PDF. Verifica que no esté dañado y que contenga texto seleccionable.", ex); }
    }

    private static string Normalizar(string value) => Regex.Replace(value.Replace('\n', ' ').Replace('\r', ' '), @"\s+", " ").Trim();

    private static List<PlanAnaliticoTema> ExtraerTemas(Page page)
    {
        var lines = new List<(double Y, List<Word> Words)>();
        foreach (var word in page.GetWords().Where(w => w.BoundingBox.Bottom > 120).OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var line = lines.FirstOrDefault(l => Math.Abs(l.Y - word.BoundingBox.Bottom) < 1.5);
            if (line.Words is null) { line = (word.BoundingBox.Bottom, new List<Word>()); lines.Add(line); }
            line.Words.Add(word);
        }
        var orderedLines = lines.OrderByDescending(l => l.Y).ToArray();
        var unitMarkers = orderedLines.SelectMany(l => l.Words.Where(w => w.BoundingBox.Left < 130 && Regex.IsMatch(w.Text, @"^U\.\d+$"))
            .Select(w => (l.Y, Unit: w.Text))).OrderByDescending(x => x.Y).ToArray();
        List<(double Y, string Text)> Cells(double left, double right) => orderedLines.Where(l => l.Y < 740)
            .Select(l => (l.Y, Text: string.Join(" ", l.Words.Where(w => w.BoundingBox.Left >= left && w.BoundingBox.Left < right).OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))))
            .Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToList();
        List<List<(double Y, string Text)>> Group(List<(double Y, string Text)> cells)
        {
            var result = new List<List<(double Y, string Text)>>();
            foreach (var cell in cells)
            {
                if (result.Count == 0 || result[^1][^1].Y - cell.Y > 15) result.Add(new());
                result[^1].Add(cell);
            }
            return result;
        }
        var themes = Group(Cells(page.Width * .218, page.Width * .436));
        var contents = Group(Cells(page.Width * .436, page.Width * .66));
        var result = new List<PlanAnaliticoTema>();
        for (var i = 0; i < Math.Min(themes.Count, contents.Count); i++)
        {
            var themeText = Normalizar(string.Join(" ", themes[i].Select(x => x.Text)));
            var contentText = Normalizar(string.Join(" ", contents[i].Select(x => x.Text)));
            if (string.IsNullOrWhiteSpace(themeText) || string.IsNullOrWhiteSpace(contentText)) continue;
            var y = themes[i][0].Y;
            var unit = unitMarkers.FirstOrDefault(u => u.Y >= y).Unit;
            if (string.IsNullOrEmpty(unit)) unit = unitMarkers.LastOrDefault().Unit;
            result.Add(new PlanAnaliticoTema($"{unit} — {themeText}", Regex.Split(contentText, @"(?<=[.;])\s+(?=[A-ZÁÉÍÓÚ0-9])").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()));
        }
        return result;
    }
}
