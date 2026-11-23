using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ReportsService.Services;

public static class InformePdf
{
    public static byte[] Generar(DatosInforme datos, int version, DateTime generadoEn)
    {
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().PaddingBottom(14).Column(header =>
            {
                header.Item().Text("FISEI · Informe de homologación").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken3);
                header.Item().Text($"Versión {version} · Expediente {datos.Fuente.Expediente.Id}").FontSize(9);
            });
            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().Text($"Estudiante: {datos.Estudiante}");
                column.Item().Text($"Identificación: {datos.Identificacion}");
                column.Item().Text($"Carrera de destino: {datos.Carrera} · Malla {datos.Malla}");
                column.Item().Text($"Profesor responsable: {datos.Fuente.Expediente.ProfesorNombre}");
                column.Item().Text($"Estado: Resuelto · Generado {generadoEn:yyyy-MM-dd HH:mm:ss} UTC");
                column.Item().PaddingTop(8).Text("Decisiones académicas").FontSize(14).SemiBold();
                foreach (var decision in datos.Fuente.Decisiones.OrderBy(x => x.AsignaturaDestinoId))
                {
                    var ids = JsonSerializer.Deserialize<Guid[]>(decision.OrigenIdsJson)!.Order().ToArray();
                    var propuesta = datos.Fuente.Analisis.GetProperty("propuestas").EnumerateArray().Single(x =>
                        x.GetProperty("asignaturaDestinoId").GetGuid() == decision.AsignaturaDestinoId &&
                        x.GetProperty("origenes").EnumerateArray().Select(o => o.GetProperty("asignaturaCursadaId").GetGuid())
                            .Order().SequenceEqual(ids));
                    var evaluacion = propuesta.GetProperty("evaluacion");
                    var reglas = evaluacion.GetProperty("reglas");
                    column.Item().PaddingTop(10).Text(propuesta.GetProperty("nombreDestino").GetString()!).FontSize(12).SemiBold();
                    column.Item().Text("Origen: " + string.Join(" + ", propuesta.GetProperty("origenes").EnumerateArray()
                        .Select(x => x.GetProperty("nombre").GetString())));
                    column.Item().Text($"Resultado calculado: {Resultado(decision.ResultadoCalculado)}");
                    column.Item().Text($"Decisión docente: {Resultado(decision.ResultadoFinal)}");
                    column.Item().Text("Justificación: " + (decision.Justificacion ?? "Aceptación de la recomendación calculada."));
                    column.Item().Text($"Decisión registrada {decision.RegistradoEn:yyyy-MM-dd HH:mm:ss} UTC · Profesor {decision.ProfesorId}");
                    column.Item().Text($"Créditos: {Porcentaje(reglas, "razonCreditos")} · Contenidos: {Porcentaje(reglas, "coberturaContenidos")} · Horas: {Porcentaje(reglas, "razonHoras")}");
                    column.Item().Text("Versión de reglas: " + reglas.GetProperty("versionReglas").GetString());
                    if (evaluacion.TryGetProperty("explicacion", out var explicacion))
                        column.Item().Text(explicacion.GetString() ?? "");
                    if (reglas.TryGetProperty("advertencias", out var advertencias))
                        foreach (var advertencia in advertencias.EnumerateArray())
                            column.Item().Text("Advertencia: " + advertencia.GetString());
                    if (evaluacion.TryGetProperty("cobertura", out var cobertura))
                    {
                        Temas(column, cobertura, "temasCoincidentes", "Temas coincidentes");
                        Temas(column, cobertura, "temasAusentes", "Temas ausentes");
                    }
                }
                if (datos.Fuente.Analisis.TryGetProperty("advertencias", out var notas))
                    foreach (var nota in notas.EnumerateArray())
                        column.Item().Text("Advertencia del expediente: " + nota.GetString());
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"Informe v{version} · Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void Temas(ColumnDescriptor column, JsonElement cobertura, string propiedad, string titulo)
    {
        if (!cobertura.TryGetProperty(propiedad, out var temas)) return;
        column.Item().Text(titulo).SemiBold();
        foreach (var tema in temas.EnumerateArray())
        {
            column.Item().Text("• " + tema.GetProperty("descripcion").GetString());
            if (tema.TryGetProperty("evidencia", out var evidencia) && evidencia.ValueKind == JsonValueKind.String)
                column.Item().Text("Evidencia: " + evidencia.GetString()).FontSize(9).FontColor(Colors.Grey.Darken2);
        }
        if (temas.GetArrayLength() == 0) column.Item().Text("Sin temas registrados.").FontSize(9);
    }

    private static string Porcentaje(JsonElement reglas, string nombre) =>
        reglas.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)
            ? (numero * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%" : "Sin datos";

    private static string Resultado(int resultado) => resultado switch
    {
        0 => "Homologable", 1 => "Rechazo por créditos", 2 => "Rechazo por contenidos",
        3 => "Datos incompletos", 4 => "Transversal directa", 5 => "Origen no aprobado",
        _ => throw new JsonException("Resultado de decisión inválido.")
    };
}

