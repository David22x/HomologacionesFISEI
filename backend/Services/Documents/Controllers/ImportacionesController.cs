using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocumentsService.Controllers;

[ApiController]
[Route("api/v1/documentos/importaciones")]
public sealed class ImportacionesController : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private sealed record Column(string Name, bool Required, int MaxLength = 0);
    private sealed record RowError(string Hoja, int Fila, string Columna, string Mensaje);
    private sealed record ValidationOutcome(
        bool IsValid, string Message, IReadOnlyList<RowError> Errors, ExternalStudentImport? Import);
    private sealed record ExternalStudentImport(
        string Cedula, int TipoIdentificacion, string Nombres, string Apellidos, int Tipo, ExternalHistoryImport Historial);
    private sealed record ExternalHistoryImport(
        Guid? CarreraOrigenId, string? CarreraOrigenNombre, string? InstitucionOrigen, int? NivelActual,
        IReadOnlyList<ExternalSubjectImport> Asignaturas);
    private sealed record ExternalSubjectImport(
        Guid? AsignaturaId, string? Codigo, string Nombre, decimal? Creditos, int? Horas, decimal Nota, string Periodo);

    private static readonly IReadOnlyDictionary<string, Column[]> Schema = new Dictionary<string, Column[]>
    {
        ["Estudiante"] =
        [
            new("Identificacion", true, 20), new("TipoIdentificacion", true), new("Nombres", true, 150),
            new("Apellidos", true, 150), new("InstitucionOrigen", true, 200),
            new("CarreraOrigen", true, 200), new("NivelActual", false)
        ],
        ["Asignaturas"] =
        [
            new("Codigo", true, 30), new("Nombre", true, 200), new("Creditos", true),
            new("Horas", true), new("Nota", true), new("Periodo", true, 30)
        ],
        ["Contenidos"] =
        [new("CodigoAsignatura", true, 30), new("Tema", true, 300), new("Subtema", false, 300)]
    };

    [HttpGet("plantilla")]
    public IActionResult DescargarPlantilla()
    {
        using var workbook = new XLWorkbook();
        foreach (var (sheetName, columns) in Schema)
        {
            var sheet = workbook.Worksheets.Add(sheetName);
            for (var index = 0; index < columns.Length; index++)
            {
                var cell = sheet.Cell(1, index + 1);
                cell.Value = columns[index].Name;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.Column(index + 1).Width = Math.Clamp(columns[index].Name.Length + 8, 16, 32);
            }

            sheet.SheetView.FreezeRows(1);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), ExcelMimeType, "plantilla_historial_externo.xlsx");
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public IActionResult ValidarArchivo([FromForm] IFormFile? archivo)
    {
        var outcome = ValidateFile(archivo);
        return outcome.IsValid
            ? Ok(new { mensaje = outcome.Message, errores = outcome.Errors, vistaPrevia = outcome.Import })
            : BadRequest(new { mensaje = outcome.Message, errores = outcome.Errors, vistaPrevia = (ExternalStudentImport?)null });
    }

    /// <summary>Confirma una importación validada y guarda el estudiante externo con su historial.</summary>
    [HttpPost("confirmar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirmar(
        [FromForm] IFormFile? archivo, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        var outcome = ValidateFile(archivo);
        if (!outcome.IsValid)
            return BadRequest(new { mensaje = outcome.Message, errores = outcome.Errors });
        if (outcome.Import is null)
            return BadRequest(new { mensaje = "No se pudo interpretar la información del archivo." });
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/internal/estudiantes/importaciones")
        {
            Content = JsonContent.Create(outcome.Import, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        request.Headers.Authorization = authorization;
        using var response = await httpClientFactory.CreateClient("StudentsService").SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        if (response.Headers.Location is not null)
            Response.Headers.Location = response.Headers.Location.ToString();
        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    private static ValidationOutcome ValidateFile(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return new(false, "Selecciona un archivo .xlsx para cargar.", Array.Empty<RowError>(), null);
        if (!string.Equals(Path.GetExtension(archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return new(false, "El formato no es válido. Carga un archivo .xlsx.", Array.Empty<RowError>(), null);
        if (archivo.Length > MaxFileSize)
            return new(false, "El archivo supera el tamaño máximo de 5 MB.", Array.Empty<RowError>(), null);

        var errors = new List<RowError>();
        ExternalStudentImport? import = null;
        try
        {
            using var stream = archivo.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheets = new Dictionary<string, (IXLWorksheet Worksheet, Dictionary<string, int> Headers)>();

            foreach (var (sheetName, columns) in Schema)
            {
                if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
                {
                    errors.Add(new("General", 0, "Hoja", $"Falta la hoja obligatoria '{sheetName}'."));
                    continue;
                }

                var headerRow = sheet.Row(1);
                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                {
                    var header = cell.GetString().Trim();
                    if (header.Length > 0 && !headers.TryAdd(header, cell.Address.ColumnNumber))
                        errors.Add(new(sheetName, 1, header, $"El encabezado '{header}' está repetido."));
                }

                foreach (var column in columns)
                {
                    if (!headers.ContainsKey(column.Name))
                        errors.Add(new(sheetName, 1, column.Name, $"Falta la columna obligatoria '{column.Name}'."));
                }
                sheets[sheetName] = (sheet, headers);
            }

            if (errors.Count == 0)
            {
                var studentRows = GetDataRows(sheets["Estudiante"].Worksheet, sheets["Estudiante"].Headers);
                if (studentRows.Count != 1)
                    errors.Add(new("Estudiante", studentRows.Count == 0 ? 2 : studentRows[1], "Identificacion", "La hoja debe contener exactamente una fila de datos."));
                else
                    ValidateStudent(sheets["Estudiante"], studentRows[0], errors);

                var subjectRows = GetDataRows(sheets["Asignaturas"].Worksheet, sheets["Asignaturas"].Headers);
                if (subjectRows.Count == 0)
                    errors.Add(new("Asignaturas", 2, "Codigo", "Agrega al menos una asignatura aprobada."));
                var subjectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in subjectRows)
                    ValidateSubject(sheets["Asignaturas"], row, subjectCodes, errors);

                ValidateContents(sheets["Contenidos"], subjectCodes, errors);
                if (errors.Count == 0)
                    import = CreateImport(sheets["Estudiante"], studentRows[0], sheets["Asignaturas"], subjectRows);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(false, "No se pudo leer el archivo. Verifica que sea un libro .xlsx válido y que no esté dañado.",
                Array.Empty<RowError>(), null);
        }

        if (errors.Count > 0)
            return new(false, $"Se encontraron {errors.Count} error(es). Corrige las filas indicadas y carga el archivo nuevamente.",
                errors, null);

        return new(true, "El archivo es válido. No se encontraron errores.", Array.Empty<RowError>(), import);
    }

    private static ExternalStudentImport CreateImport(
        (IXLWorksheet Worksheet, Dictionary<string, int> Headers) studentSheet,
        int studentRow,
        (IXLWorksheet Worksheet, Dictionary<string, int> Headers) subjectsSheet,
        IReadOnlyList<int> subjectRows)
    {
        var identificationType = Value(studentSheet, studentRow, "TipoIdentificacion");
        var level = Value(studentSheet, studentRow, "NivelActual");
        int? currentLevel = null;
        if (level.Length > 0)
        {
            TryNumber(level, out var numericLevel);
            currentLevel = decimal.ToInt32(numericLevel);
        }
        var subjects = subjectRows.Select(row =>
        {
            TryNumber(Value(subjectsSheet, row, "Creditos"), out var credits);
            TryNumber(Value(subjectsSheet, row, "Horas"), out var hours);
            TryNumber(Value(subjectsSheet, row, "Nota"), out var grade);
            return new ExternalSubjectImport(
                null,
                Value(subjectsSheet, row, "Codigo"),
                Value(subjectsSheet, row, "Nombre"),
                credits,
                decimal.ToInt32(hours),
                grade,
                Value(subjectsSheet, row, "Periodo"));
        }).ToList();

        return new ExternalStudentImport(
            Value(studentSheet, studentRow, "Identificacion"),
            identificationType == "Pasaporte" ? 1 : 0,
            Value(studentSheet, studentRow, "Nombres"),
            Value(studentSheet, studentRow, "Apellidos"),
            1,
            new ExternalHistoryImport(
                null,
                Value(studentSheet, studentRow, "CarreraOrigen"),
                Value(studentSheet, studentRow, "InstitucionOrigen"),
                currentLevel,
                subjects));
    }

    private static List<int> GetDataRows(IXLWorksheet sheet, IReadOnlyDictionary<string, int> headers)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<int>();
        for (var row = 2; row <= lastRow; row++)
        {
            if (headers.Values.Any(column => !sheet.Cell(row, column).IsEmpty()))
                rows.Add(row);
        }
        return rows;
    }

    private static string Value((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, string column)
        => sheet.Headers.TryGetValue(column, out var index) ? sheet.Worksheet.Cell(row, index).GetFormattedString().Trim() : string.Empty;

    private static void Required((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, string column, List<RowError> errors)
    {
        if (string.IsNullOrWhiteSpace(Value(sheet, row, column)))
            errors.Add(new(sheet.Worksheet.Name, row, column, $"'{column}' es obligatorio."));
    }

    private static void MaxLength((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, string column, int max, List<RowError> errors)
    {
        var value = Value(sheet, row, column);
        if (value.Length > max)
            errors.Add(new(sheet.Worksheet.Name, row, column, $"'{column}' no puede superar {max} caracteres."));
    }

    private static bool TryNumber(string value, out decimal number)
    {
        var style = System.Globalization.NumberStyles.Number;
        var ecuadorCulture = System.Globalization.CultureInfo.GetCultureInfo("es-EC");
        if (value.Contains(',') && !value.Contains('.')
            && decimal.TryParse(value, style, ecuadorCulture, out number))
            return true;
        return decimal.TryParse(value, style, System.Globalization.CultureInfo.InvariantCulture, out number)
            || decimal.TryParse(value, style, ecuadorCulture, out number);
    }

    private static void ValidateStudent((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, List<RowError> errors)
    {
        foreach (var column in Schema["Estudiante"].Where(column => column.Required)) Required(sheet, row, column.Name, errors);
        foreach (var column in Schema["Estudiante"].Where(column => column.MaxLength > 0)) MaxLength(sheet, row, column.Name, column.MaxLength, errors);

        var identificationType = Value(sheet, row, "TipoIdentificacion");
        var identification = Value(sheet, row, "Identificacion");
        if (!string.IsNullOrEmpty(identificationType) && identificationType is not ("Cedula" or "Pasaporte"))
            errors.Add(new("Estudiante", row, "TipoIdentificacion", "Usa 'Cedula' o 'Pasaporte'."));
        else if (identificationType == "Cedula" && !IsValidEcuadorianId(identification))
            errors.Add(new("Estudiante", row, "Identificacion", "La cédula debe tener 10 dígitos y un dígito verificador válido."));
        else if (identificationType == "Pasaporte" && (identification.Length is < 5 or > 20 || !identification.All(char.IsLetterOrDigit)))
            errors.Add(new("Estudiante", row, "Identificacion", "El pasaporte debe tener entre 5 y 20 letras o números, sin símbolos."));

        var level = Value(sheet, row, "NivelActual");
        if (level.Length > 0 && (!decimal.TryParse(level, out var parsedLevel) || parsedLevel <= 0 || parsedLevel != decimal.Truncate(parsedLevel)))
            errors.Add(new("Estudiante", row, "NivelActual", "El nivel actual debe ser un número entero mayor que 0."));
    }

    private static bool IsValidEcuadorianId(string value)
    {
        if (value.Length != 10 || !value.All(char.IsDigit)) return false;
        var province = int.Parse(value[..2], System.Globalization.CultureInfo.InvariantCulture);
        if (province is < 1 or > 24 || value[2] > '5') return false;
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var digit = value[i] - '0';
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        var verifier = (10 - sum % 10) % 10;
        return verifier == value[9] - '0';
    }

    private static void ValidateSubject((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, HashSet<string> codes, List<RowError> errors)
    {
        foreach (var column in Schema["Asignaturas"].Where(column => column.Required)) Required(sheet, row, column.Name, errors);
        foreach (var column in Schema["Asignaturas"].Where(column => column.MaxLength > 0)) MaxLength(sheet, row, column.Name, column.MaxLength, errors);

        var code = Value(sheet, row, "Codigo");
        if (code.Length > 0 && !codes.Add(code)) errors.Add(new("Asignaturas", row, "Codigo", $"El código '{code}' está repetido."));
        ValidateDecimal(sheet, row, "Creditos", errors, number => number > 0, "Debe ser un número mayor que 0.");
        ValidateDecimal(sheet, row, "Horas", errors, number => number > 0 && number == decimal.Truncate(number), "Debe ser un número entero mayor que 0.");
        ValidateDecimal(sheet, row, "Nota", errors, number => number is >= 0 and <= 10, "Debe ser un número entre 0 y 10.");
    }

    private static void ValidateDecimal((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, int row, string column, List<RowError> errors, Func<decimal, bool> predicate, string message)
    {
        var value = Value(sheet, row, column);
        if (value.Length > 0 && (!TryNumber(value, out var number) || !predicate(number)))
            errors.Add(new(sheet.Worksheet.Name, row, column, $"'{column}' no es válido. {message}"));
    }

    private static void ValidateContents((IXLWorksheet Worksheet, Dictionary<string, int> Headers) sheet, HashSet<string> subjectCodes, List<RowError> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in GetDataRows(sheet.Worksheet, sheet.Headers))
        {
            foreach (var column in Schema["Contenidos"].Where(column => column.Required)) Required(sheet, row, column.Name, errors);
            foreach (var column in Schema["Contenidos"].Where(column => column.MaxLength > 0)) MaxLength(sheet, row, column.Name, column.MaxLength, errors);
            var code = Value(sheet, row, "CodigoAsignatura");
            if (code.Length > 0 && !subjectCodes.Contains(code))
                errors.Add(new("Contenidos", row, "CodigoAsignatura", $"El código '{code}' no existe en la hoja Asignaturas."));
            var topic = Value(sheet, row, "Tema");
            var subtopic = Value(sheet, row, "Subtema");
            if (code.Length > 0 && topic.Length > 0 && !seen.Add($"{code}\u001f{topic}\u001f{subtopic}"))
                errors.Add(new("Contenidos", row, "Tema", "La combinación de asignatura, tema y subtema está repetida."));
        }
    }
}
