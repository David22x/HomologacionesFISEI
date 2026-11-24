using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ReportsService.Services;

public static class InformePdf
{
    public const string VersionPlantilla = "6";
    private const string Azul = "#004070";
    private const string AzulClaro = "#E8EEF4";
    private const string Verde = "#2E7D32";
    private const string VerdeClaro = "#E8F5E9";
    private const string Rojo = "#C62828";
    private const string RojoClaro = "#FFEBEE";
    private const string Naranja = "#E65100";
    private const string Gris = "#616161";
    private const decimal MinimoCreditos = 0.80m;
    private const decimal MinimoContenidos = 0.80m;

    private static readonly byte[] Logo = CargarLogo();

    private sealed record Fila(string Destino, string Origen, int Calculado, int Final, string? Justificacion,
        DateTime Registrado, JsonElement Evaluacion, JsonElement Reglas);

    private static byte[] CargarLogo()
    {
        using var stream = typeof(InformePdf).Assembly.GetManifestResourceStream("logo-uta.png")!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public static byte[] Generar(DatosInforme datos, int version, DateTime generadoEn)
    {
        var filas = Filas(datos);
        var homologadas = filas.Where(x => Homologada(x.Final)).ToArray();
        var rechazadas = filas.Where(x => !Homologada(x.Final)).ToArray();
        var revertidas = rechazadas.Where(x => Homologada(x.Calculado)).ToArray();
        var otrasRechazadas = rechazadas.Length - revertidas.Length;
        var observaciones = homologadas.Where(x => x.Calculado != x.Final || EsRecomendable(x.Justificacion)).ToArray();
        var expediente = datos.Fuente.Expediente.Id.ToString()[..8];
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().PaddingBottom(14).Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.ConstantItem(150).Image(Logo).FitWidth();
                    row.RelativeItem().AlignRight().AlignMiddle().Column(titulo =>
                    {
                        titulo.Item().AlignRight().Text("FISEI · Informe de homologación").FontSize(16).SemiBold().FontColor(Azul);
                        titulo.Item().AlignRight().Text($"Versión {version} · Expediente {expediente}").FontSize(8);
                    });
                });
                header.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Azul);
            });
            page.Content().Column(column =>
            {
                column.Spacing(8);
                Datos(column, datos, generadoEn);
                Resumen(column, filas.Length, homologadas.Length);
                Titulo(column, $"Asignaturas homologadas ({homologadas.Length})");
                if (homologadas.Length == 0)
                    column.Item().Text("Ninguna asignatura fue homologada.").FontColor(Gris);
                else
                    TablaHomologadas(column, homologadas);
                Titulo(column, $"Asignaturas no homologadas por decisión del profesor ({revertidas.Length})");
                column.Item().Text("Asignaturas que el sistema recomendaba homologar y que el profesor no homologó.")
                    .FontSize(9).FontColor(Gris);
                if (revertidas.Length == 0)
                    column.Item().Text("El profesor no rechazó ninguna asignatura recomendada por el sistema.").FontColor(Gris);
                else
                    TablaRechazadas(column, revertidas);
                if (otrasRechazadas > 0)
                    column.Item().Text($"Otras {otrasRechazadas} asignaturas no cumplieron los requisitos de homologación y no se detallan en este informe.")
                        .FontSize(9).FontColor(Gris);
                if (observaciones.Length > 0)
                {
                    Titulo(column, "Observaciones del profesor");
                    column.Item().Text("Asignaturas homologadas por decisión del profesor aunque el sistema no lo recomendaba, o con una nota del profesor.")
                        .FontSize(9).FontColor(Gris);
                    foreach (var fila in observaciones) Observacion(column, fila);
                }
                Totales(column, datos, filas.Length, homologadas.Length);
                Cierre(column, datos, generadoEn);
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

    private static Fila[] Filas(DatosInforme datos)
    {
        var propuestas = datos.Fuente.Analisis.GetProperty("propuestas").EnumerateArray().ToArray();
        return datos.Fuente.Decisiones.Select(decision =>
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(decision.OrigenIdsJson)!.Order().ToArray();
            var propuesta = propuestas.Single(x =>
                x.GetProperty("asignaturaDestinoId").GetGuid() == decision.AsignaturaDestinoId &&
                x.GetProperty("origenes").EnumerateArray().Select(o => o.GetProperty("asignaturaCursadaId").GetGuid())
                    .Order().SequenceEqual(ids));
            var evaluacion = propuesta.GetProperty("evaluacion");
            var origen = string.Join(" + ", propuesta.GetProperty("origenes").EnumerateArray()
                .Select(x => x.GetProperty("nombre").GetString()));
            return new Fila(propuesta.GetProperty("nombreDestino").GetString()!, origen, decision.ResultadoCalculado,
                decision.ResultadoFinal, decision.Justificacion, decision.RegistradoEn, evaluacion,
                evaluacion.GetProperty("reglas"));
        }).OrderBy(x => x.Destino, StringComparer.InvariantCultureIgnoreCase).ToArray();
    }

    private static void Totales(ColumnDescriptor column, DatosInforme datos, int total, int homologadas)
    {
        column.Item().PaddingTop(10).Text(texto =>
        {
            texto.Span("Total de asignaturas homologadas: ").SemiBold().FontColor(Azul);
            texto.Span($"{homologadas} de {total}");
        });
        var advertencias = datos.Fuente.Analisis.TryGetProperty("advertencias", out var notas) && notas.ValueKind == JsonValueKind.Array
            ? notas.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : [];
        column.Item().Text(texto =>
        {
            texto.Span("Observaciones: ").SemiBold().FontColor(Azul);
            texto.Span(advertencias.Length == 0 ? "Sin observaciones." : string.Join(" ", advertencias));
        });
    }

    private static string ContenidosTexto(Fila fila) =>
        fila.Calculado == 4 ? "No aplica" : Porcentaje(fila.Reglas, "coberturaContenidos");

    private static void Titulo(ColumnDescriptor column, string texto) =>
        column.Item().PaddingTop(8).Text(texto).FontSize(14).SemiBold().FontColor(Azul);

    private static void Datos(ColumnDescriptor column, DatosInforme datos, DateTime generadoEn)
    {
        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(bloque =>
        {
            bloque.Spacing(4);
            bloque.Item().Row(par =>
            {
                par.RelativeItem().Element(c => Dato(c, "Estudiante", datos.Estudiante));
                par.RelativeItem().Element(c => Dato(c, "Identificación", datos.Identificacion));
            });
            if (datos.Tipo is not null)
                bloque.Item().Element(c => Dato(c, "Tipo de homologación", datos.Tipo));
            bloque.Item().Element(c => Dato(c, "Carrera de origen", datos.CarreraOrigen));
            bloque.Item().Element(c => Dato(c, "Carrera de destino", $"{datos.Carrera} · Malla {datos.Malla}"));
            bloque.Item().Row(par =>
            {
                par.RelativeItem().Element(c => Dato(c, "Profesor responsable", datos.Fuente.Expediente.ProfesorNombre));
                par.RelativeItem().Element(c => Dato(c, "Estado", "Resuelto"));
            });
            bloque.Item().Element(c => Dato(c, "Generado", Fecha(generadoEn)));
        });
    }

    private static void Dato(IContainer contenedor, string etiqueta, string valor) =>
        contenedor.Text(texto =>
        {
            texto.Span(etiqueta + ": ").SemiBold().FontColor(Azul);
            texto.Span(valor);
        });

    private static bool Homologada(int resultado) => resultado is 0 or 4;

    private static bool EsRecomendable(string? justificacion) =>
        justificacion?.Contains("Es recomendable homologar", StringComparison.OrdinalIgnoreCase) == true;

    private static void Resumen(ColumnDescriptor column, int total, int homologadas)
    {
        column.Item().PaddingTop(6).Text("Resumen").FontSize(14).SemiBold().FontColor(Azul);
        column.Item().Row(fila =>
        {
            fila.Spacing(10);
            Caja(fila.RelativeItem(), total.ToString(), "Decisiones", Azul, AzulClaro);
            Caja(fila.RelativeItem(), homologadas.ToString(), "Homologadas", Verde, VerdeClaro);
            Caja(fila.RelativeItem(), (total - homologadas).ToString(), "Rechazadas", Rojo, RojoClaro);
        });
        column.Item().Text($"Criterios mínimos: créditos {Entero(MinimoCreditos)}% y contenidos {Entero(MinimoContenidos)}%. Las horas se muestran como referencia.")
            .FontSize(9).FontColor(Gris);
    }

    private static void Caja(IContainer contenedor, string valor, string etiqueta, string color, string fondo) =>
        contenedor.Background(fondo).Padding(8).Text(texto =>
        {
            texto.AlignCenter();
            texto.Line(valor).FontSize(22).SemiBold().FontColor(color);
            texto.Line(etiqueta).FontSize(9).FontColor(color);
        });

    private static void TablaHomologadas(ColumnDescriptor column, Fila[] filas)
    {
        column.Item().Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(22);
                columnas.RelativeColumn(3);
                columnas.RelativeColumn(3);
                columnas.ConstantColumn(48);
                columnas.ConstantColumn(56);
                columnas.RelativeColumn(2);
            });
            tabla.Header(cabecera =>
            {
                foreach (var titulo in new[] { "#", "Asignatura destino", "Origen", "Créditos", "Contenidos", "Decidida según" })
                    cabecera.Cell().Background(Azul).Padding(4)
                        .Text(titulo).FontSize(8.5f).SemiBold().FontColor(Colors.White);
            });
            for (var i = 0; i < filas.Length; i++)
            {
                var fila = filas[i];
                tabla.Cell().Element(Cuerpo).Text((i + 1).ToString()).FontSize(8.5f);
                tabla.Cell().Element(Cuerpo).Text(fila.Destino).FontSize(8.5f).SemiBold();
                tabla.Cell().Element(Cuerpo).Text(fila.Origen).FontSize(8.5f);
                tabla.Cell().Element(Cuerpo).Text(Porcentaje(fila.Reglas, "razonCreditos")).FontSize(8.5f)
                    .FontColor(ColorCumple(Cumplido(fila.Reglas, "razonCreditos", MinimoCreditos)));
                tabla.Cell().Element(Cuerpo).Text(ContenidosTexto(fila)).FontSize(8.5f)
                    .FontColor(fila.Calculado == 4 ? Gris : ColorCumple(Cumplido(fila.Reglas, "coberturaContenidos", MinimoContenidos)));
                tabla.Cell().Element(Cuerpo)
                    .Text(fila.Calculado == fila.Final ? "Sistema" : "Profesor").FontSize(8.5f);
            }
        });
    }

    private static void TablaRechazadas(ColumnDescriptor column, Fila[] filas)
    {
        column.Item().Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(22);
                columnas.RelativeColumn(2);
                columnas.RelativeColumn(2);
                columnas.RelativeColumn(5);
            });
            tabla.Header(cabecera =>
            {
                foreach (var titulo in new[] { "#", "Asignatura destino", "Origen", "Motivo del rechazo" })
                    cabecera.Cell().Background(Azul).Padding(4)
                        .Text(titulo).FontSize(8.5f).SemiBold().FontColor(Colors.White);
            });
            for (var i = 0; i < filas.Length; i++)
            {
                var fila = filas[i];
                tabla.Cell().Element(Cuerpo).Text((i + 1).ToString()).FontSize(8.5f);
                tabla.Cell().Element(Cuerpo).Text(fila.Destino).FontSize(8.5f).SemiBold();
                tabla.Cell().Element(Cuerpo).Text(fila.Origen).FontSize(8.5f);
                tabla.Cell().Element(Cuerpo).ShowEntire().Column(celda => Motivo(celda, fila));
            }
        });
    }

    private static void Motivo(ColumnDescriptor celda, Fila fila)
    {
        celda.Spacing(2);
        celda.Item().Text(Resultado(fila.Final)).FontSize(8.5f).SemiBold().FontColor(ColorResultado(fila.Final));
        if (Homologada(fila.Calculado))
            celda.Item().Text("El sistema recomendaba homologar; el profesor la rechazó.").FontSize(8.5f).FontColor(Naranja);
        celda.Item().Text(texto =>
        {
            texto.DefaultTextStyle(x => x.FontSize(8.5f));
            Metricas(texto, fila);
        });
        var ausentes = Ausentes(fila.Evaluacion);
        if (ausentes.Length > 0)
            celda.Item().Text("Temas ausentes: " + string.Join("; ", ausentes.Take(3).Select(x => Corto(x, 90)))
                + (ausentes.Length > 3 ? $" y {ausentes.Length - 3} más" : string.Empty)).FontSize(8.5f);
        if (!string.IsNullOrWhiteSpace(fila.Justificacion))
            celda.Item().Text(texto =>
            {
                texto.DefaultTextStyle(x => x.FontSize(8.5f));
                texto.Span("Justificación del profesor: ").SemiBold();
                texto.Span(fila.Justificacion!);
            });
        if (fila.Reglas.TryGetProperty("advertencias", out var advertencias))
            foreach (var advertencia in advertencias.EnumerateArray())
                celda.Item().Text("Advertencia: " + advertencia.GetString()).FontSize(8).FontColor(Naranja);
    }

    private static IContainer Cuerpo(IContainer contenedor) =>
        contenedor.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4);

    private static void Metricas(TextDescriptor texto, Fila fila)
    {
        texto.Span($"Créditos: {Porcentaje(fila.Reglas, "razonCreditos")}");
        texto.Span(Cumple(fila.Reglas, "razonCreditos", MinimoCreditos))
            .FontColor(ColorCumple(Cumplido(fila.Reglas, "razonCreditos", MinimoCreditos)));
        texto.Span($"  ·  Contenidos: {ContenidosTexto(fila)}");
        if (fila.Calculado != 4)
            texto.Span(Cumple(fila.Reglas, "coberturaContenidos", MinimoContenidos))
                .FontColor(ColorCumple(Cumplido(fila.Reglas, "coberturaContenidos", MinimoContenidos)));
        texto.Span($"  ·  Horas: {Porcentaje(fila.Reglas, "razonHoras")}");
    }

    private static void Observacion(ColumnDescriptor column, Fila fila)
    {
        column.Item().PaddingTop(8).ShowEntire().Column(bloque =>
        {
            bloque.Spacing(3);
            bloque.Item().Row(titulo =>
            {
                titulo.RelativeItem().Text(fila.Destino).FontSize(11).SemiBold().FontColor(Azul);
                titulo.AutoItem().Background(ColorResultado(fila.Final)).PaddingHorizontal(6).PaddingVertical(2)
                    .Text(Resultado(fila.Final)).FontSize(8).SemiBold().FontColor(Colors.White);
            });
            bloque.Item().Text(texto =>
            {
                texto.Span("Origen: ").SemiBold();
                texto.Span(fila.Origen);
            });
            bloque.Item().Text(texto =>
            {
                texto.Span("Resultado calculado: ").SemiBold();
                texto.Span(Resultado(fila.Calculado));
                texto.Span("  ·  Decisión docente: ").SemiBold();
                texto.Span(Resultado(fila.Final));
            });
            bloque.Item().Text(texto =>
            {
                texto.Span("Justificación: ").SemiBold();
                texto.Span(string.IsNullOrWhiteSpace(fila.Justificacion)
                    ? "Aceptación de la recomendación calculada." : fila.Justificacion);
            });
            if (EsRecomendable(fila.Justificacion))
                bloque.Item().Background(Colors.Green.Lighten4).Padding(4)
                    .Text("Nota: Es recomendable homologar").SemiBold().FontColor(Colors.Green.Darken3);
            bloque.Item().Text(texto => Metricas(texto, fila));
            if (fila.Evaluacion.TryGetProperty("cobertura", out var cobertura))
                Cobertura(bloque, cobertura);
            if (fila.Reglas.TryGetProperty("advertencias", out var advertencias))
                foreach (var advertencia in advertencias.EnumerateArray())
                    bloque.Item().Text("Advertencia: " + advertencia.GetString()).FontSize(8.5f).FontColor(Naranja);
            bloque.Item().Text($"Decisión registrada {Fecha(fila.Registrado)} · Reglas {fila.Reglas.GetProperty("versionReglas").GetString()}")
                .FontSize(8).FontColor(Gris);
            bloque.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void Cobertura(ColumnDescriptor bloque, JsonElement cobertura)
    {
        var coincidentes = Contar(cobertura, "temasCoincidentes");
        var ausentes = Ausentes(new JsonElementWrapper(cobertura).Valor);
        if (coincidentes + ausentes.Length == 0) return;
        bloque.Item().Text($"Temas del destino cubiertos: {coincidentes} de {coincidentes + ausentes.Length}");
        if (ausentes.Length == 0) return;
        bloque.Item().Text("Temas ausentes").SemiBold().FontSize(9).FontColor(Rojo);
        foreach (var tema in ausentes.Take(5))
            bloque.Item().PaddingLeft(8).Text("• " + Corto(tema, 150)).FontSize(8.5f);
        if (ausentes.Length > 5)
            bloque.Item().PaddingLeft(8).Text($"… y {ausentes.Length - 5} más").FontSize(8.5f).FontColor(Gris);
    }

    private static int Contar(JsonElement cobertura, string propiedad) =>
        cobertura.TryGetProperty(propiedad, out var temas) && temas.ValueKind == JsonValueKind.Array
            ? temas.GetArrayLength() : 0;

    private static string[] Ausentes(JsonElement evaluacionOCobertura)
    {
        var cobertura = evaluacionOCobertura;
        if (evaluacionOCobertura.ValueKind == JsonValueKind.Object &&
            evaluacionOCobertura.TryGetProperty("cobertura", out var interna))
            cobertura = interna;
        if (cobertura.ValueKind != JsonValueKind.Object ||
            !cobertura.TryGetProperty("temasAusentes", out var temas) || temas.ValueKind != JsonValueKind.Array)
            return [];
        return temas.EnumerateArray().Select(x => x.GetProperty("descripcion").GetString() ?? string.Empty).ToArray();
    }

    private readonly record struct JsonElementWrapper(JsonElement Valor);

    private static void Cierre(ColumnDescriptor column, DatosInforme datos, DateTime generadoEn)
    {
        column.Item().PaddingTop(24).ShowEntire().Column(cierre =>
        {
            cierre.Item().Row(par =>
            {
                par.RelativeItem().Column(firma =>
                {
                    firma.Item().PaddingTop(32).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    firma.Item().Text("Firma del profesor responsable").FontSize(9).FontColor(Gris);
                    firma.Item().Text(datos.Fuente.Expediente.ProfesorNombre).SemiBold();
                });
                par.ConstantItem(30);
                par.RelativeItem().Column(firma =>
                {
                    firma.Item().PaddingTop(32).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    firma.Item().Text("Firma del estudiante (acepta el informe)").FontSize(9).FontColor(Gris);
                    firma.Item().Text(datos.Estudiante).SemiBold();
                });
            });
            cierre.Item().PaddingTop(10).Text($"Fecha de emisión: {Fecha(generadoEn)}");
            cierre.Item().PaddingTop(12).Text("La decisión final de homologación corresponde al profesor responsable. " +
                "Los resultados calculados por el sistema son una recomendación de apoyo.")
                .FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    }

    private static string Fecha(DateTime utc) =>
        utc.AddHours(-5).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (hora de Ecuador)";

    private static string Corto(string? texto, int maximo)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length <= maximo ? limpio : limpio[..(maximo - 1)].TrimEnd() + "…";
    }

    private static string Entero(decimal minimo) => (minimo * 100m).ToString("0.##", CultureInfo.InvariantCulture);

    private static bool? Cumplido(JsonElement reglas, string nombre, decimal minimo) =>
        reglas.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)
            ? numero >= minimo : null;

    private static string ColorCumple(bool? cumple) => cumple switch { true => Verde, false => Rojo, _ => Gris };

    private static string ColorResultado(int resultado) =>
        resultado is 0 or 4 ? Verde : resultado is 3 or 5 ? Naranja : Rojo;

    private static string Cumple(JsonElement reglas, string nombre, decimal minimo) =>
        Cumplido(reglas, nombre, minimo) is bool cumple
            ? (cumple ? " (cumple" : " (no cumple") + ", mínimo " + Entero(minimo) + "%)"
            : string.Empty;

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
