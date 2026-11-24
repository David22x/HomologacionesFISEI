using BuildingBlocks.AcademicImports;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentsService.Services;

public static class RecordAcademicoReader
{
    public static VistaRecord Leer(byte[] bytes)
    {
        try
        {
            if (bytes.Length < 5 || !System.Text.Encoding.ASCII.GetString(bytes, 0, 5).StartsWith("%PDF-")) throw new InvalidDataException("El archivo no es un PDF válido.");
            using var doc = PdfDocument.Open(bytes);
            if (doc.NumberOfPages > 80) throw new InvalidDataException("El récord supera 80 páginas.");
            var textos = doc.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToArray();
            if (string.IsNullOrWhiteSpace(string.Join("", textos))) throw new InvalidDataException("El PDF parece escaneado. Se requiere una versión con texto u OCR antes de importar; no se inventarán notas.");
            var ids = Regex.Matches(string.Join("\n", textos), @"(?:identidad|c[eé]dula)\s*(?:n[uú]mero|nro\.?)?\s*:?\s*(\d{10})", RegexOptions.IgnoreCase).Select(m => m.Groups[1].Value).Distinct().ToArray();
            if (ids.Length > 1) throw new InvalidDataException("El PDF contiene identificaciones de varios estudiantes. Carga un récord por estudiante.");
            var filas = LeerTablaCompleta(doc, textos); var notas = new List<string>();
            string carrera = "", periodo = "";
            var tablaCompleta = filas.Count > 0;
            for (var pagina = 1; !tablaCompleta && pagina <= doc.NumberOfPages; pagina++)
            {
                var page = doc.GetPage(pagina);
                var lines = new List<(double Y, List<UglyToad.PdfPig.Content.Word> Words)>();
                foreach (var word in page.GetWords().OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
                {
                    var line = lines.FirstOrDefault(l => Math.Abs(l.Y - word.BoundingBox.Bottom) < 2);
                    if (line.Words is null) { line = (word.BoundingBox.Bottom, []); lines.Add(line); }
                    line.Words.Add(word);
                }
                var inTable = false; var pending = "";
                foreach (var text in lines.OrderByDescending(l => l.Y).Select(l => string.Join(" ", l.Words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))))
                {
                    var c = Regex.Match(text, @"CARRERA\s+DE\s*:?\s*(.+?)(?:,|$)", RegexOptions.IgnoreCase);
                    if (c.Success) carrera = c.Groups[1].Value.Trim();
                    var per = Regex.Match(text, @"(?:per[ií]odo\s*:?\s*)?([A-ZÁÉÍÓÚ]{3,12}\s*/?\s*\d{2,4}\s*[-–]\s*[A-ZÁÉÍÓÚ]{3,12}\s*/?\s*\d{2,4}|\d{4}\s*[-/]\s*[AB12])", RegexOptions.IgnoreCase);
                    if (per.Success) periodo = per.Groups[1].Value.Trim();
                    if (Regex.IsMatch(text, @"ASIGNATURA.*(?:PROMEDIO|CALIFICACI|NOTA)", RegexOptions.IgnoreCase)) { inTable = true; pending = ""; continue; }
                    if (Regex.IsMatch(text, @"SUMA\s+TOTAL|PROMEDIO\s*:|OBSERVACI[OÓ]N|FIRMADO|P[ÁA]GINA\s+\d", RegexOptions.IgnoreCase)) { inTable = false; pending = ""; }
                    if (!inTable) continue;
                    var row = Regex.Match(text, @"^(.*?)\s+(\d{1,2}(?:[.,]\d{1,2})?)\s*(?:/\s*10\b)?\s+.*?\b(APROBADO|REPROBADO|NO\s+APROBADO)\b", RegexOptions.IgnoreCase);
                    if (!row.Success) { if (!Regex.IsMatch(text, @"ASIGNATURA|ESTADO|PROMEDIO", RegexOptions.IgnoreCase)) pending = (pending + " " + text).Trim(); continue; }
                    var nombre = (pending + " " + row.Groups[1].Value).Trim(); pending = "";
                    if (!decimal.TryParse(row.Groups[2].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var nota) || nota is < 0 or > 10)
                        throw new InvalidDataException($"Nota fuera de la escala 0–10 en la página {pagina}.");
                    var estado = row.Groups[3].Value.ToUpperInvariant();
                    filas.Add(new(nombre, carrera, periodo, nota, estado, pagina, estado == "APROBADO"));
                }
            }
            if (filas.Count == 0) throw new InvalidDataException("No se reconocieron filas de asignaturas, notas y estado. Usa el formato de certificación UTA o completa el historial manualmente.");
            if (ids.Length == 0) notas.Add("Falta identificar al estudiante en el documento. Completa y verifica la identificación antes de confirmar.");
            if (filas.Any(x => x.Periodo.Length == 0)) notas.Add("Hay asignaturas sin período reconocido: complétalo en la vista previa.");
            if (filas.Any(x => x.Carrera.Length == 0)) notas.Add("Hay asignaturas sin carrera reconocida: complétala en la vista previa.");
            notas.Add("Revisa las notas y asociaciones de catálogo. La importación no verifica la firma electrónica del PDF.");
            return new(ids.SingleOrDefault() ?? "", filas.DistinctBy(x => (x.Nombre, x.Periodo, x.Carrera, x.Nota, x.Estado)).ToArray(), notas, Perfil(string.Join("\n", textos)));
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se pudo leer el PDF; verifica que no esté dañado ni protegido.", ex); }
    }
    public static string Texto(byte[] bytes)
    {
        try {
            using var doc = PdfDocument.Open(bytes);
            if (doc.NumberOfPages > 80) throw new InvalidDataException("El documento supera 80 páginas.");
            var text = string.Join("\n", doc.GetPages().Select(p => $"[Página {p.Number}]\n" + ContentOrderTextExtractor.GetText(p)));
            if (text.Length > 180000) throw new InvalidDataException("El documento contiene demasiado texto. Divide el récord en archivos más pequeños.");
            if (!doc.GetPages().Any(p => p.Letters.Count > 0)) throw new InvalidDataException("El PDF es escaneado. Se requiere OCR o un PDF con texto seleccionable.");
            return text;
        } catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se pudo leer el PDF.", ex); }
    }
    private static PerfilRecord Perfil(string text)
    {
        var career = Regex.Match(text, @"CARRERA\s+DE\s*:?\s*([^\r\n,]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
        var match = Regex.Match(text, @"(?:Srta\)\.|Sr\(a\)\.|CERTIFICA:.*?\)\.)\s*([\p{L}\s]+?)\s+con\s+documento\s+de\s+identidad", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var full = Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim();
        var words = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new(words.Length >= 3 ? string.Join(" ", words.SkipLast(2)) : full,
            words.Length >= 3 ? string.Join(" ", words.TakeLast(2)) : "", full, career,
            Regex.IsMatch(text, @"Universidad T[eé]cnica de Ambato", RegexOptions.IgnoreCase) ? "Universidad Técnica de Ambato" : "", EscalaMaxima: 10);
    }
    private static List<FilaRecord> LeerTablaCompleta(PdfDocument doc, string[] textos)
    {
        var result = new List<FilaRecord>();
        var first = doc.GetPages().Select(p => new { Page = p, Words = p.GetWords().ToArray() })
            .FirstOrDefault(p => p.Words.Any(w => w.Text == "CRED") && p.Words.Any(w => w.Text == "PERIODO"));
        if (first is null) return result;
        var header = first.Words.First(w => w.Text == "CRED"); var headerY = header.BoundingBox.Bottom;
        var same = first.Words.Where(w => Math.Abs(w.BoundingBox.Bottom - headerY) < 2).ToArray();
        double X(string token) => same.FirstOrDefault(w => string.Equals(w.Text.Normalize(System.Text.NormalizationForm.FormD).Replace("\u0301", ""), token, StringComparison.OrdinalIgnoreCase))?.BoundingBox.Left ?? throw new InvalidDataException($"No se reconoció la columna {token} del récord. Usa la extracción para otras universidades o revisa el formato.");
        var creditX = X("CRED") - 3; var periodX = X("PERIODO") - 20; var folioX = X("FOLIO") - 2;
        var gradeX = X("PROMEDIO") - 3; var observationX = X("OBSERVACION") - 3; var stateX = X("ESTADO") - 4; var timesX = X("VEZ") - 2;
        var career = Perfil(string.Join("\n", textos)).Carrera;
        foreach (var page in doc.GetPages().Where(p => p.Number >= first.Page.Number))
        {
            var words = page.GetWords().ToArray();
            var top = page.Number == first.Page.Number ? headerY - 2 : page.Height;
            var stop = words.Where(w => Regex.IsMatch(w.Text, @"^(PROMEDIO:|NOTA:|CREDITOS)$", RegexOptions.IgnoreCase))
                .Select(w => w.BoundingBox.Bottom).DefaultIfEmpty(0).Max();
            var anchors = words.Where(w => w.BoundingBox.Left >= creditX && w.BoundingBox.Left < periodX && w.BoundingBox.Bottom < top && w.BoundingBox.Bottom > stop
                && Regex.IsMatch(w.Text, @"^\d+(?:[.,]\d+)?$")).OrderByDescending(w => w.BoundingBox.Bottom).ToArray();
            for (var i = 0; i < anchors.Length; i++) {
                var y = anchors[i].BoundingBox.Bottom; var bottom = i + 1 < anchors.Length ? anchors[i + 1].BoundingBox.Bottom + 2 : stop;
                var row = words.Where(w => w.BoundingBox.Bottom <= y + 2 && w.BoundingBox.Bottom > bottom).OrderByDescending(w => Math.Round(w.BoundingBox.Bottom / 2)).ThenBy(w => w.BoundingBox.Left).ToArray();
                string Cell(double left, double right) => string.Join(" ", row.Where(w => w.BoundingBox.Left >= left && w.BoundingBox.Left < right).Select(w => w.Text)).Trim();
                var name = Cell(0, creditX); if (name.Length == 0) continue;
                var state = Cell(stateX, timesX).ToUpperInvariant();
                if (state.Length == 0) state = "SIN CURSAR";
                var grade = Cell(gradeX, observationX); decimal? nota = decimal.TryParse(grade.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
                var period = Cell(periodX, folioX);
                var credits = decimal.Parse(anchors[i].Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                result.Add(new(name, career, period, nota, state, page.Number, state == "APROBADO" && nota.HasValue, Creditos: credits, CreditosDocumento: credits));
            }
        }
        return result;
    }

}
