using AcademicService.Data;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Tests;

public class AcademicDbSeederTests
{
    private const string Basica = """
    {
      "version": 1,
      "carreras": [
        { "codigo": "TI", "nombre": "Tecnologías de la Información", "esDemo": false,
          "mallas": [ { "version": "2026", "vigente": true, "niveles": [
            { "numero": 1, "asignaturas": [
              { "codigo": "TI-01-01", "nombre": "ALGEBRA LINEAL", "creditos": 3, "horas": 144, "unidadOrganizacional": "Básica" },
              { "codigo": "TI-01-02", "nombre": "FUNDAMENTOS DE PROGRAMACION", "creditos": 4, "horas": 192, "unidadOrganizacional": "Básica" } ] },
            { "numero": 2, "asignaturas": [
              { "codigo": "TI-02-01", "nombre": "CALCULO INTEGRAL", "creditos": 3, "horas": 144, "unidadOrganizacional": "Básica" } ] } ] } ] },
        { "codigo": "IND", "nombre": "Industrial", "esDemo": true, "mallas": [] }
      ]
    }
    """;

    private const string SegundaCarreraConCreditosCero = """
    {
      "carreras": [
        { "codigo": "TI", "nombre": "TI", "mallas": [ { "version": "2026", "vigente": true, "niveles": [
          { "numero": 1, "asignaturas": [ { "codigo": "TI-01-01", "nombre": "A", "creditos": 3, "horas": 144 } ] } ] } ] },
        { "codigo": "SW", "nombre": "SW", "mallas": [ { "version": "2026", "vigente": true, "niveles": [
          { "numero": 1, "asignaturas": [ { "codigo": "SW-01-01", "nombre": "B", "creditos": 0, "horas": 0 } ] } ] } ] }
      ]
    }
    """;

    private const string CodigoRepetido = """
    {
      "carreras": [
        { "codigo": "TI", "nombre": "TI", "mallas": [ { "version": "2026", "vigente": true, "niveles": [
          { "numero": 1, "asignaturas": [ { "codigo": "TI-01-01", "nombre": "A", "creditos": 3, "horas": 144 } ] },
          { "numero": 2, "asignaturas": [ { "codigo": "TI-01-01", "nombre": "B", "creditos": 3, "horas": 144 } ] } ] } ] }
      ]
    }
    """;

    [Fact]
    public async Task Carga_crea_carreras_mallas_niveles_y_asignaturas()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();

        var r = await AcademicDbSeeder.SeedFromJsonAsync(db, Basica);

        Assert.Equal(new SeedResult(2, 1, 2, 3, 0), r);
        Assert.Equal(2, await db.Carreras.CountAsync());
        Assert.Equal(3, await db.Asignaturas.CountAsync());
        Assert.True((await db.Carreras.SingleAsync(c => c.Codigo == "IND")).EsDemo);
        Assert.False((await db.Carreras.SingleAsync(c => c.Codigo == "TI")).EsDemo);
        Assert.Equal(0, await db.Mallas.CountAsync(m => m.Carrera!.Codigo == "IND"));
    }

    [Fact]
    public async Task Correr_la_carga_dos_veces_no_duplica_nada()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();

        await AcademicDbSeeder.SeedFromJsonAsync(db, Basica);
        var segunda = await AcademicDbSeeder.SeedFromJsonAsync(db, Basica);

        Assert.Equal(new SeedResult(0, 0, 0, 0, 0), segunda);
        Assert.Equal(2, await db.Carreras.CountAsync());
        Assert.Equal(1, await db.Mallas.CountAsync());
        Assert.Equal(2, await db.Niveles.CountAsync());
        Assert.Equal(3, await db.Asignaturas.CountAsync());
    }

    [Fact]
    public async Task Una_asignatura_modificada_en_el_json_se_actualiza_sin_duplicarse()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        await AcademicDbSeeder.SeedFromJsonAsync(db, Basica);

        var r = await AcademicDbSeeder.SeedFromJsonAsync(db, Basica.Replace("\"creditos\": 4", "\"creditos\": 6"));

        Assert.Equal(1, r.AsignaturasActualizadas);
        Assert.Equal(0, r.AsignaturasNuevas);
        Assert.Equal(3, await db.Asignaturas.CountAsync());
        var cambiada = await db.Asignaturas.SingleAsync(a => a.Codigo == "TI-01-02");
        Assert.Equal(6m, cambiada.Creditos);
    }

    [Fact]
    public async Task Creditos_cero_en_el_json_lanza_error_y_no_deja_datos_a_medias()
    {
        using var tdb = new AcademicTestDb();
        await using (var db = tdb.CreateContext())
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => AcademicDbSeeder.SeedFromJsonAsync(db, SegundaCarreraConCreditosCero));
        }

        await using var verif = tdb.CreateContext();
        Assert.Equal(0, await verif.Carreras.CountAsync());
        Assert.Equal(0, await verif.Asignaturas.CountAsync());
    }

    [Fact]
    public async Task Codigo_repetido_en_la_misma_malla_hace_fallar_la_carga_completa()
    {
        using var tdb = new AcademicTestDb();
        await using (var db = tdb.CreateContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => AcademicDbSeeder.SeedFromJsonAsync(db, CodigoRepetido));
        }

        await using var verif = tdb.CreateContext();
        Assert.Equal(0, await verif.Asignaturas.CountAsync());
    }

    [Fact]
    public async Task Json_vacio_o_sin_formato_es_rechazado()
    {
        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();

        await Assert.ThrowsAsync<InvalidDataException>(() => AcademicDbSeeder.SeedFromJsonAsync(db, "null"));
    }

    [Fact]
    public async Task La_semilla_real_del_repositorio_carga_TI_y_SW_con_80_asignaturas()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Seed", "seed_academic_sprint1.json");
        Assert.True(File.Exists(ruta), $"No se encontro la semilla en {ruta}");
        var json = await File.ReadAllTextAsync(ruta);

        using var tdb = new AcademicTestDb();
        await using var db = tdb.CreateContext();
        var r = await AcademicDbSeeder.SeedFromJsonAsync(db, json);

        Assert.Equal(5, r.CarrerasNuevas);
        Assert.Equal(80, r.AsignaturasNuevas);
        Assert.Equal(40, await db.Asignaturas.CountAsync(a => a.MallaVersion!.Carrera!.Codigo == "TI"));
        Assert.Equal(40, await db.Asignaturas.CountAsync(a => a.MallaVersion!.Carrera!.Codigo == "SW"));

        var todas = await db.Asignaturas.ToListAsync();
        Assert.All(todas, a => Assert.True(a.Creditos > 0, $"Creditos invalidos en {a.Codigo}"));
        Assert.All(todas, a => Assert.False(string.IsNullOrWhiteSpace(a.UnidadOrganizacional)));
        Assert.Equal(80, todas.Select(a => a.Codigo).Distinct().Count());

        var demos = await db.Carreras.Where(c => c.EsDemo).ToListAsync();
        Assert.Equal(3, demos.Count);
        Assert.Equal(0, await db.Mallas.CountAsync(m => m.Carrera!.EsDemo));
        Assert.Equal(0, await db.Asignaturas.CountAsync(a => a.MallaVersion!.Carrera!.EsDemo));
    }
}
