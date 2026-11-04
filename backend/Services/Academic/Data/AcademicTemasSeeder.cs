using System.Text.Json;
using AcademicService.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Data;

public sealed record SeedTemasResult(
    int AsignaturasCargadas,
    int AsignaturasOmitidas,
    int AsignaturasNoEncontradas,
    int TemasNuevos,
    int SubtemasNuevos);

public static class AcademicTemasSeeder
{
    public const string RutaRelativa = "Data/Seed/seed_temas_sprint2.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public sealed record ArchivoTemas(int Version, List<AsignaturaTemas> Asignaturas);
    public sealed record AsignaturaTemas(string Codigo, string? Nombre, List<TemaSeed> Temas);
    public sealed record TemaSeed(int Orden, string Nombre, List<SubtemaSeed> Subtemas);
    public sealed record SubtemaSeed(int Orden, string Nombre);

    /// <summary>Carga los temas iniciales. Solo en Desarrollo y solo en asignaturas que aun no tienen temas.</summary>
    public static async Task<SeedTemasResult?> SeedAsync(
        IServiceProvider services, bool isDevelopment, CancellationToken ct = default)
    {
        if (!isDevelopment)
        {
            return null;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AcademicTemasSeeder");

        var ruta = Path.Combine(AppContext.BaseDirectory, RutaRelativa);
        if (!File.Exists(ruta))
        {
            logger.LogWarning("No se encontro el archivo de temas en {Ruta}; no se cargan temas.", ruta);
            return null;
        }

        ArchivoTemas? archivo;
        await using (var stream = File.OpenRead(ruta))
        {
            archivo = await JsonSerializer.DeserializeAsync<ArchivoTemas>(stream, JsonOptions, ct);
        }

        if (archivo is null)
        {
            return null;
        }

        var codigos = archivo.Asignaturas.Select(a => a.Codigo).ToList();
        var asignaturas = await db.Asignaturas
            .Where(a => codigos.Contains(a.Codigo) && a.MallaVersion!.Vigente)
            .Include(a => a.Temas)
            .ToDictionaryAsync(a => a.Codigo, ct);

        int cargadas = 0, omitidas = 0, noEncontradas = 0, temasNuevos = 0, subtemasNuevos = 0;

        foreach (var semilla in archivo.Asignaturas)
        {
            if (!asignaturas.TryGetValue(semilla.Codigo, out var asignatura))
            {
                noEncontradas++;
                logger.LogWarning("Asignatura {Codigo} del archivo de temas no existe en el catalogo vigente.", semilla.Codigo);
                continue;
            }

            // Si ya tiene temas (cargados antes o editados por el Administrador) no se tocan.
            if (asignatura.Temas.Count > 0)
            {
                omitidas++;
                continue;
            }

            foreach (var t in semilla.Temas)
            {
                var tema = new Tema { AsignaturaId = asignatura.Id, Nombre = t.Nombre.Trim(), Orden = t.Orden };
                foreach (var st in t.Subtemas)
                {
                    tema.Subtemas.Add(new Subtema { Nombre = st.Nombre.Trim(), Orden = st.Orden });
                }

                db.Temas.Add(tema);
                temasNuevos++;
                subtemasNuevos += tema.Subtemas.Count;
            }

            cargadas++;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Temas iniciales: {Cargadas} asignaturas cargadas, {Omitidas} omitidas (ya tenian temas), {NoEncontradas} no encontradas; {Temas} temas y {Subtemas} subtemas nuevos.",
            cargadas, omitidas, noEncontradas, temasNuevos, subtemasNuevos);

        return new SeedTemasResult(cargadas, omitidas, noEncontradas, temasNuevos, subtemasNuevos);
    }
}
