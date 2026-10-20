namespace AcademicService.Data;

// Formato del archivo seed_academic_sprint1.json
public sealed class SeedFile
{
    public int Version { get; set; }
    public List<SeedCarrera> Carreras { get; set; } = new();
}

public sealed class SeedCarrera
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsDemo { get; set; }
    public List<SeedMalla> Mallas { get; set; } = new();
}

public sealed class SeedMalla
{
    public string Version { get; set; } = string.Empty;
    public bool Vigente { get; set; }
    public List<SeedNivel> Niveles { get; set; } = new();
}

public sealed class SeedNivel
{
    public int Numero { get; set; }
    public List<SeedAsignatura> Asignaturas { get; set; } = new();
}

public sealed class SeedAsignatura
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Creditos { get; set; }
    public int Horas { get; set; }
    public string? UnidadOrganizacional { get; set; }
}
