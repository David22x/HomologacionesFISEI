using System.Text.Json;
using AcademicService.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Data;

public sealed record SeedResult(
    int CarrerasNuevas,
    int MallasNuevas,
    int NivelesNuevos,
    int AsignaturasNuevas,
    int AsignaturasActualizadas);

public static class AcademicDbSeeder
{
    public const string RutaRelativaSemilla = "Data/Seed/seed_academic_sprint1.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Aplica migraciones y carga la semilla. Solo actua en Desarrollo.</summary>
    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment, CancellationToken ct = default)
    {
        if (!isDevelopment)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AcademicDbSeeder");

        await db.Database.MigrateAsync(ct);

        var ruta = Path.Combine(AppContext.BaseDirectory, RutaRelativaSemilla);
        if (!File.Exists(ruta))
        {
            logger.LogWarning("No se encontro el archivo de semilla en {Ruta}; no se carga el catalogo.", ruta);
            return;
        }

        var json = await File.ReadAllTextAsync(ruta, ct);
        var r = await SeedFromJsonAsync(db, json, ct);
        logger.LogInformation(
            "Semilla del catalogo aplicada: {Carreras} carreras, {Mallas} mallas, {Niveles} niveles y {Nuevas} asignaturas nuevas ({Actualizadas} actualizadas).",
            r.CarrerasNuevas, r.MallasNuevas, r.NivelesNuevos, r.AsignaturasNuevas, r.AsignaturasActualizadas);
    }

    /// <summary>Carga el catalogo desde el JSON. Idempotente y transaccional.</summary>
    public static async Task<SeedResult> SeedFromJsonAsync(AcademicDbContext db, string json, CancellationToken ct = default)
    {
        var seed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)
            ?? throw new InvalidDataException("El archivo de semilla esta vacio o no es valido.");

        int carrerasNuevas = 0, mallasNuevas = 0, nivelesNuevos = 0, asigNuevas = 0, asigActualizadas = 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var sc in seed.Carreras)
        {
            Requerido(sc.Codigo, "carrera.codigo");
            Requerido(sc.Nombre, $"carrera {sc.Codigo}: nombre");
            var codigoCarrera = sc.Codigo.Trim();

            var carrera = await db.Carreras.FirstOrDefaultAsync(c => c.Codigo == codigoCarrera, ct);
            if (carrera is null)
            {
                carrera = new Carrera { Codigo = codigoCarrera, Nombre = sc.Nombre.Trim(), EsDemo = sc.EsDemo };
                db.Carreras.Add(carrera);
                carrerasNuevas++;
            }
            else
            {
                carrera.Nombre = sc.Nombre.Trim();
                carrera.EsDemo = sc.EsDemo;
            }

            if (sc.EsDemo)
            {
                // Las carreras DEMO se publican sin mallas ni asignaturas reales.
                // Limpia datos antiguos por si la semilla se aplica sobre una DB existente.
                var demoMallaIds = await db.Mallas
                    .Where(m => m.CarreraId == carrera.Id)
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                if (demoMallaIds.Count > 0)
                {
                    var asignaturasDemo = await db.Asignaturas
                        .Where(a => demoMallaIds.Contains(a.MallaVersionId))
                        .ToListAsync(ct);
                    db.Asignaturas.RemoveRange(asignaturasDemo);
                    await db.SaveChangesAsync(ct);

                    var nivelesDemo = await db.Niveles
                        .Where(n => demoMallaIds.Contains(n.MallaVersionId))
                        .ToListAsync(ct);
                    db.Niveles.RemoveRange(nivelesDemo);
                    await db.SaveChangesAsync(ct);

                    var mallasDemo = await db.Mallas
                        .Where(m => demoMallaIds.Contains(m.Id))
                        .ToListAsync(ct);
                    db.Mallas.RemoveRange(mallasDemo);
                    await db.SaveChangesAsync(ct);
                }

                await db.SaveChangesAsync(ct);
                continue;
            }

            foreach (var sm in sc.Mallas)
            {
                Requerido(sm.Version, $"carrera {codigoCarrera}: version de malla");
                var versionMalla = sm.Version.Trim();

                var malla = await db.Mallas.FirstOrDefaultAsync(
                    m => m.CarreraId == carrera.Id && m.Version == versionMalla, ct);
                if (malla is null)
                {
                    malla = new MallaVersion { CarreraId = carrera.Id, Version = versionMalla, Vigente = sm.Vigente };
                    db.Mallas.Add(malla);
                    mallasNuevas++;
                }
                else
                {
                    malla.Vigente = sm.Vigente;
                }

                foreach (var sn in sm.Niveles)
                {
                    if (sn.Numero <= 0)
                    {
                        throw new InvalidDataException($"Carrera {codigoCarrera}: el numero de nivel debe ser mayor que 0 (llego {sn.Numero}).");
                    }

                    var nivel = await db.Niveles.FirstOrDefaultAsync(
                        n => n.MallaVersionId == malla.Id && n.Numero == sn.Numero, ct);
                    if (nivel is null)
                    {
                        nivel = new Nivel { MallaVersionId = malla.Id, Numero = sn.Numero };
                        db.Niveles.Add(nivel);
                        nivelesNuevos++;
                    }

                    foreach (var sa in sn.Asignaturas)
                    {
                        Requerido(sa.Codigo, $"carrera {codigoCarrera}: codigo de asignatura");
                        Requerido(sa.Nombre, $"asignatura {sa.Codigo}: nombre");
                        if (sa.Creditos <= 0)
                        {
                            throw new InvalidDataException($"Asignatura {sa.Codigo}: los creditos deben ser mayores que 0 (llego {sa.Creditos}).");
                        }

                        var codigo = sa.Codigo.Trim();
                        var asignatura = await db.Asignaturas.FirstOrDefaultAsync(
                            a => a.MallaVersionId == malla.Id && a.Codigo == codigo, ct);

                        if (asignatura is null)
                        {
                            db.Asignaturas.Add(new Asignatura
                            {
                                MallaVersionId = malla.Id,
                                NivelId = nivel.Id,
                                Codigo = codigo,
                                Nombre = sa.Nombre.Trim(),
                                Creditos = sa.Creditos,
                                Horas = sa.Horas,
                                UnidadOrganizacional = sa.UnidadOrganizacional?.Trim()
                            });
                            asigNuevas++;
                        }
                        else
                        {
                            var cambio = asignatura.Nombre != sa.Nombre.Trim()
                                || asignatura.Creditos != sa.Creditos
                                || asignatura.Horas != sa.Horas
                                || asignatura.UnidadOrganizacional != sa.UnidadOrganizacional?.Trim()
                                || asignatura.NivelId != nivel.Id;
                            if (cambio)
                            {
                                asignatura.Nombre = sa.Nombre.Trim();
                                asignatura.Creditos = sa.Creditos;
                                asignatura.Horas = sa.Horas;
                                asignatura.UnidadOrganizacional = sa.UnidadOrganizacional?.Trim();
                                asignatura.NivelId = nivel.Id;
                                asigActualizadas++;
                            }
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new SeedResult(carrerasNuevas, mallasNuevas, nivelesNuevos, asigNuevas, asigActualizadas);
    }

    private static void Requerido(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidDataException($"Semilla invalida: falta {campo}.");
        }
    }
}
