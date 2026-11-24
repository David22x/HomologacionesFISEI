using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using AcademicService.Contracts;
using AcademicService.Controllers;
using AcademicService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace AcademicService.Tests;

public sealed class PlanesMasivosTests
{
    [Theory]
    [InlineData("../plan.pdf")]
    [InlineData("/plan.pdf")]
    [InlineData("C:/plan.pdf")]
    [InlineData("sin-planes.txt")]
    public async Task Rechaza_archivos_sin_planes_o_rutas_invalidas(string nombre)
    {
        using var store = new AcademicTestDb(); using var db = store.CreateContext();
        var malla = CrearMalla(); db.Mallas.Add(malla); await db.SaveChangesAsync();
        var controller = Controller(db);
        var result = await controller.Analizar(malla.Id, Zip((nombre, new byte[] { 1, 2 })), default);
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.Asignaturas.ToListAsync());
    }

    [Fact]
    public async Task Importa_contenidos_de_varias_paginas_y_reintenta_sin_duplicar()
    {
        using var store = new AcademicTestDb(); using var db = store.CreateContext();
        var malla = CrearMalla(); db.Mallas.Add(malla); await db.SaveChangesAsync();
        var controller = Controller(db); var file = Zip(("planes/programacion.pdf", Plan()));
        var preview = Assert.IsType<OkObjectResult>(await controller.Analizar(malla.Id, file, default));
        var json = JsonSerializer.SerializeToElement(preview.Value);
        var entradas = json.GetProperty("entradas").Deserialize<PlanesMasivosController.Entrada[]>()!;
        var plan = Assert.Single(entradas); Assert.Null(plan.Error);
        Assert.Contains(plan.Datos!.Temas, x => x.Nombre.Contains("Clases"));
        Assert.Contains(plan.Datos.Temas, x => x.Nombre.Contains("Herencia"));
        Assert.DoesNotContain(plan.Datos.Temas, x => x.Nombre.Contains("texto de resumen"));
        Assert.Equal(1, plan.Datos.Nivel);
        Assert.IsType<OkObjectResult>(await controller.Confirmar(malla.Id, file, JsonSerializer.Serialize(entradas), default));
        Assert.Single(await db.Asignaturas.ToListAsync()); Assert.Single(await db.PlanesAnaliticos.ToListAsync());
        var again = Assert.IsType<OkObjectResult>(await controller.Analizar(malla.Id, file, default));
        var againJson = JsonSerializer.SerializeToElement(again.Value).GetProperty("entradas");
        Assert.IsType<OkObjectResult>(await controller.Confirmar(malla.Id, file, againJson.GetRawText(), default));
        Assert.Single(await db.Asignaturas.ToListAsync());
        // El reemplazo explícito no deja contenidos duplicados.
        var replace = againJson.Deserialize<PlanesMasivosController.Entrada[]>()!.Select(x => x with { Reemplazar = true }).ToArray();
        Assert.IsType<OkObjectResult>(await controller.Confirmar(malla.Id, file, JsonSerializer.Serialize(replace), default));
        Assert.Equal(2, await db.Temas.CountAsync());
    }

    [Fact]
    public async Task Malla_publicada_no_admite_cambios_y_carrera_distinta_no_se_selecciona()
    {
        using var store = new AcademicTestDb(); using var db = store.CreateContext();
        var malla = CrearMalla(); malla.Carrera!.Codigo = "SW"; db.Mallas.Add(malla); await db.SaveChangesAsync();
        var controller = Controller(db); var file = Zip(("programacion.pdf", Plan()));
        var preview = Assert.IsType<OkObjectResult>(await controller.Analizar(malla.Id, file, default));
        var row = Assert.Single(JsonSerializer.SerializeToElement(preview.Value).GetProperty("entradas").Deserialize<PlanesMasivosController.Entrada[]>()!);
        Assert.False(row.Seleccionado); Assert.NotNull(row.Error);
        malla.Estado = EstadoMalla.Publicada; await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await controller.Confirmar(malla.Id, file, "[]", default));
    }

    private static MallaVersion CrearMalla() => new() { Version = "2026", Carrera = new Carrera { Codigo = "TI", Nombre = "Tecnologías de la Información" } };
    private static PlanesMasivosController Controller(AcademicService.Data.AcademicDbContext db) => new(db) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")])) } } };
    private static IFormFile Zip(params (string Nombre, byte[] Datos)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var entry in entries) { using var output = zip.CreateEntry(entry.Nombre).Open(); output.Write(entry.Datos); }
        stream.Position = 0; return new FormFile(stream, 0, stream.Length, "archivo", "planes.zip");
    }
    private static byte[] Plan()
    {
        using var builder = new PdfDocumentBuilder(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(600, 842);
        void Text(PdfPageBuilder p, string value, double x, double y) => p.AddText(value, 9, new PdfPoint(x, y), font);
        Text(page, "Nombre de la asignatura Programacion Carrera TI", 35, 790);
        Text(page, "Codigo UTA-FISEI-TI-MP-UB-01-02", 35, 770);
        Text(page, "Unidad organizacional curricular Basica Creditos / Horas 3", 35, 750);
        Text(page, "Total de horas 144", 35, 730);
        Text(page, "III CONTENIDO", 35, 710);
        Text(page, "Unidades curriculares", 35, 690); Text(page, "Unidades tematicas", 140, 690); Text(page, "Contenidos minimos", 270, 690);
        Text(page, "U.1", 40, 660); Text(page, "Clases", 140, 660); Text(page, "Objetos y atributos", 270, 660);
        var next = builder.AddPage(600, 842);
        Text(next, "U.2", 40, 770); Text(next, "Herencia", 140, 770); Text(next, "Polimorfismo", 270, 770);
        Text(next, "Resultados del aprendizaje de la asignatura", 40, 700);
        Text(next, "texto de resumen", 140, 675); Text(next, "NO es contenido", 270, 675);
        return builder.Build();
    }
}
