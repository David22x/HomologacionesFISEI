using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace DocumentsService.Controllers;

[ApiController]
[Route("api/v1/documentos/[controller]")]

public class ImportacionesController : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    
    [HttpGet("plantilla")]
    public IActionResult DescargarPlantilla()
    {
        using var workbook = new XLWorkbook();
        
        
        workbook.Worksheets.Add("Estudiante");
        workbook.Worksheets.Add("Asignaturas");
        workbook.Worksheets.Add("Contenidos");
        workbook.Worksheets.Add("Escala de Calificaciones");

        
        var wsAsignaturas = workbook.Worksheet("Asignaturas");
        wsAsignaturas.Cell(1, 1).Value = "Código";
        wsAsignaturas.Cell(1, 2).Value = "Nombre Asignatura";
        wsAsignaturas.Cell(1, 3).Value = "Créditos";
        wsAsignaturas.Cell(1, 4).Value = "Horas Totales";
        wsAsignaturas.Cell(1, 5).Value = "Calificación";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Plantilla_Historial_Externo.xlsx");
    }

    
    [HttpPost]
    public IActionResult CargarHistorial(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { Mensaje = "No se proporcionó ningún archivo." });

        var extension = Path.GetExtension(archivo.FileName).ToLower();
        if (extension != ".xlsx")
            return BadRequest(new { Mensaje = "Solo se permiten archivos .xlsx. Se rechazan formatos como .xlsm u otros." }); //[cite: 106]

        if (archivo.Length > MaxFileSize)
            return BadRequest(new { Mensaje = "El archivo supera el límite permitido de 5 MB." }); //[cite: 106]

        var errores = new List<object>();

        using var stream = archivo.OpenReadStream();
        using var workbook = new XLWorkbook(stream);

        
        var hojasRequeridas = new[] { "Estudiante", "Asignaturas", "Contenidos" };
        foreach (var hoja in hojasRequeridas)
        {
            if (!workbook.Worksheets.Contains(hoja))
            {
                errores.Add(new { Hoja = "General", Fila = 0, Error = $"Falta la hoja obligatoria: {hoja}" });
            }
        }

        if (errores.Count > 0)
            return BadRequest(new { Errores = errores });

        
        var wsAsignaturas = workbook.Worksheet("Asignaturas");
        var rowCount = wsAsignaturas.LastRowUsed()?.RowNumber() ?? 0;

        for (int i = 2; i <= rowCount; i++) 
        {
            var creditosStr = wsAsignaturas.Cell(i, 3).GetString(); 
            
            
            if (!decimal.TryParse(creditosStr, out decimal creditos) || creditos <= 0)
            {
                errores.Add(new { Hoja = "Asignaturas", Fila = i, Error = "Los créditos deben ser numéricos y mayores a 0." }); //[cite: 106, 107]
            }

            
        }

        if (errores.Count > 0)
            return BadRequest(new { Errores = errores }); 

        
        return Ok(new { Mensaje = "Archivo validado correctamente.", DatosVistaPrevia = "..." });
    }
}