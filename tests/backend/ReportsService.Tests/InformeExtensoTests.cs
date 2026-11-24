using System.Text;
using System.Text.Json;
using QuestPDF.Infrastructure;
using ReportsService.Services;
using UglyToad.PdfPig;

namespace ReportsService.Tests;

public sealed class InformeExtensoTests
{
    [Fact]
    public void Pdf_con_40_decisiones_resume_homologadas_y_motivos_en_pocas_paginas()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var nombres = new[]
        {
            "ALGEBRA LINEAL", "CALCULO DIFERENCIAL", "ESTRUCTURA DE DATOS", "FISICA", "INTRODUCCION A REDES",
            "LOGICA MATEMATICA", "METODOLOGIA DE LA INVESTIGACION", "PROBABILIDAD Y ESTADISTICA", "REALIDAD NACIONAL",
            "PROGRAMACION ORIENTADA A OBJETOS", "BASES DE DATOS", "SISTEMAS OPERATIVOS", "INGENIERIA DE REQUISITOS",
            "ARQUITECTURA DE SOFTWARE", "DESARROLLO WEB", "DESARROLLO MOVIL", "INTELIGENCIA ARTIFICIAL",
            "AUDITORIA DE SISTEMAS DE INFORMACION", "DESARROLLO DE PROYECTOS", "SEGURIDAD INFORMATICA",
            "CALCULO INTEGRAL", "ECUACIONES DIFERENCIALES", "MATEMATICAS DISCRETAS", "ELECTRONICA DIGITAL",
            "REDES DE COMPUTADORAS", "GESTION DE PROYECTOS", "PRUEBAS DE SOFTWARE", "DISENO DE INTERFACES",
            "COMPUTACION EN LA NUBE", "ETICA PROFESIONAL", "EMPRENDIMIENTO", "INGLES TECNICO",
            "ANALISIS NUMERICO", "ALGORITMOS AVANZADOS", "PROGRAMACION CONCURRENTE", "MINERIA DE DATOS",
            "SIMULACION", "TEORIA DE LA COMPUTACION", "COMPILADORES", "TRABAJO DE TITULACION"
        };
        var propuestas = new List<object>();
        var decisiones = new List<DecisionDatos>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var destino = Guid.NewGuid();
            var origen = Guid.NewGuid();
            var calculado = i < 8 ? 0 : i == 8 ? 4 : i == 9 ? 1 : i is 10 or 11 ? 0 : i % 3 == 0 ? 1 : 2;
            var final = i == 9 ? 0 : i is 10 or 11 ? 2 : calculado;
            var creditos = calculado is 0 or 4 ? 1m : calculado == 1 ? 0.7m : 1m;
            var contenidos = calculado is 0 ? 0.9m : calculado == 1 ? 0.85m : calculado == 2 ? 0.55m : (decimal?)null;
            if (i is 10 or 11) contenidos = 0.88m;
            var ausentes = i switch
            {
                10 => new[] { "Consultas SQL avanzadas | Subconsultas, vistas y procedimientos almacenados", "Normalizacion y formas normales" },
                11 => new[] { "Planificacion de procesos | Algoritmos de planificacion", "Gestion de memoria | Paginacion y segmentacion",
                    "Sistemas de archivos", "Concurrencia | Semaforos y monitores", "Interbloqueos", "Entrada y salida", "Virtualizacion" },
                _ => calculado == 2 ? new[] { "Tema ausente de ejemplo | Detalle del contenido que no se encontro en el silabo de origen" } : Array.Empty<string>()
            };
            propuestas.Add(new
            {
                asignaturaDestinoId = destino, nombreDestino = nombres[i],
                origenes = new[] { new { asignaturaCursadaId = origen, nombre = i == 4 ? "FUNDAMENTOS DE REDES Y COMUNICACION DE DATOS" : nombres[i] } },
                evaluacion = new
                {
                    reglas = new { razonCreditos = creditos, coberturaContenidos = contenidos, razonHoras = (decimal?)1m,
                        versionReglas = "1.0.0", advertencias = Array.Empty<string>() },
                    explicacion = "Evaluacion de muestra.",
                    cobertura = new
                    {
                        temasCoincidentes = Enumerable.Range(1, 6).Select(n => new { descripcion = $"Tema {n}", evidencia = "x" }).ToArray(),
                        temasAusentes = ausentes.Select(a => new { descripcion = a }).ToArray()
                    }
                }
            });
            var justificacion = i switch
            {
                9 => "El profesor reviso el silabo y los contenidos cubren lo exigido. Es recomendable homologar",
                10 => "Faltan temas de bases de datos relacionales en el silabo de origen.",
                11 => "El silabo de origen no incluye gestion de memoria ni concurrencia.",
                _ => calculado is 0 or 4 ? null : "Decision de prueba: el resultado calculado no cumple los requisitos."
            };
            decisiones.Add(new DecisionDatos(Guid.NewGuid(), destino, JsonSerializer.Serialize(new[] { origen }), calculado, final,
                justificacion, Guid.Empty, new DateTime(2026, 11, 24, 5, 16, 11, DateTimeKind.Utc)));
        }
        var analisis = JsonSerializer.SerializeToElement(new { propuestas, advertencias = Array.Empty<string>() },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var fuente = new FuenteHomologacion(
            new ExpedienteDatos(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "profesor@uta.edu.ec", 5),
            decisiones.ToArray(), analisis);
        var datos = new DatosInforme(fuente, "Zuarez, Luna", "0102030400", "Software", "2026",
            "Tecnologías de la Información", "Interna");

        var pdf = InformePdf.Generar(datos, 2, new DateTime(2026, 11, 24, 19, 29, 0, DateTimeKind.Utc));

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        using var documento = PdfDocument.Open(pdf);
        Assert.InRange(documento.NumberOfPages, 1, 3);
        var texto = string.Join(" ", documento.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        Assert.Contains("Resumen 40 10 30 Decisiones Homologadas Rechazadas", texto);
        Assert.Contains("Asignaturas homologadas (10)", texto);
        Assert.Contains("Asignaturas no homologadas por decisión del profesor (2)", texto);
        Assert.Contains("Otras 28 asignaturas no cumplieron los requisitos de homologación", texto);
        Assert.Contains("Total de asignaturas homologadas: 10 de 40", texto);
        Assert.Contains("Justificación del profesor: Faltan temas de bases de datos relacionales en el silabo de origen.", texto);
        Assert.DoesNotContain("Evidencia:", texto);
    }
}
