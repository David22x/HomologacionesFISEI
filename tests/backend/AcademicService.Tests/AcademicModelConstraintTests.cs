using AcademicService.Data;
using AcademicService.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Tests;

public class AcademicModelConstraintTests
{
    private static async Task<(Carrera Carrera, MallaVersion Malla, Nivel Nivel)> CrearBaseAsync(
        AcademicDbContext db, string codigoCarrera = "TI", string version = "2026")
    {
        var carrera = new Carrera { Codigo = codigoCarrera, Nombre = "Carrera " + codigoCarrera };
        var malla = new MallaVersion { CarreraId = carrera.Id, Version = version, Vigente = true };
        var nivel = new Nivel { MallaVersionId = malla.Id, Numero = 1 };
        db.AddRange(carrera, malla, nivel);
        await db.SaveChangesAsync();
        return (carrera, malla, nivel);
    }

    private static Asignatura NuevaAsignatura(MallaVersion malla, Nivel nivel, string codigo = "A-1", decimal creditos = 3) => new()
    {
        MallaVersionId = malla.Id,
        NivelId = nivel.Id,
        Codigo = codigo,
        Nombre = "Asignatura " + codigo,
        Creditos = creditos,
        Horas = 144
    };

    [Fact]
    public async Task Carrera_con_codigo_repetido_es_rechazada()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        await CrearBaseAsync(db, "TI");

        db.Carreras.Add(new Carrera { Codigo = "TI", Nombre = "Otra" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Version_de_malla_repetida_en_la_misma_carrera_es_rechazada_pero_en_otra_carrera_se_permite()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (carrera, _, _) = await CrearBaseAsync(db, "TI", "2026");

        db.Mallas.Add(new MallaVersion { CarreraId = carrera.Id, Version = "2026" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await using var db2 = tdb.CreateContext();
        var otra = new Carrera { Codigo = "SW", Nombre = "Software" };
        db2.Carreras.Add(otra);
        db2.Mallas.Add(new MallaVersion { CarreraId = otra.Id, Version = "2026" });
        await db2.SaveChangesAsync();
    }

    [Fact]
    public async Task Numero_de_nivel_repetido_en_la_misma_malla_es_rechazado()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (_, malla, _) = await CrearBaseAsync(db);

        db.Niveles.Add(new Nivel { MallaVersionId = malla.Id, Numero = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Numero_de_nivel_cero_es_rechazado()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (_, malla, _) = await CrearBaseAsync(db);

        db.Niveles.Add(new Nivel { MallaVersionId = malla.Id, Numero = 0 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Codigo_de_asignatura_repetido_en_la_misma_malla_es_rechazado()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (_, malla, nivel) = await CrearBaseAsync(db);
        db.Asignaturas.Add(NuevaAsignatura(malla, nivel, "A-1"));
        await db.SaveChangesAsync();

        db.Asignaturas.Add(NuevaAsignatura(malla, nivel, "A-1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Mismo_codigo_de_asignatura_se_permite_en_otra_version_de_malla()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (carrera, malla, nivel) = await CrearBaseAsync(db);
        db.Asignaturas.Add(NuevaAsignatura(malla, nivel, "A-1"));

        var malla2 = new MallaVersion { CarreraId = carrera.Id, Version = "2027" };
        var nivel2 = new Nivel { MallaVersionId = malla2.Id, Numero = 1 };
        db.AddRange(malla2, nivel2);
        db.Asignaturas.Add(NuevaAsignatura(malla2, nivel2, "A-1"));

        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Asignaturas.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Creditos_cero_o_negativos_son_rechazados(int creditos)
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (_, malla, nivel) = await CrearBaseAsync(db);

        db.Asignaturas.Add(NuevaAsignatura(malla, nivel, "A-1", creditos));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Creditos_positivos_son_aceptados()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var (_, malla, nivel) = await CrearBaseAsync(db);

        db.Asignaturas.Add(NuevaAsignatura(malla, nivel, "A-1", 4));
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.Asignaturas.CountAsync());
    }

    [Fact]
    public async Task No_se_puede_borrar_un_nivel_que_tiene_asignaturas()
    {
        using var tdb = new AcademicTestDb();
        await using (var db = tdb.CreateContext())
        {
            var (_, malla, nivel) = await CrearBaseAsync(db);
            db.Asignaturas.Add(NuevaAsignatura(malla, nivel));
            await db.SaveChangesAsync();
        }

        await using var db2 = tdb.CreateContext();
        db2.Niveles.Remove(await db2.Niveles.FirstAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }
}
