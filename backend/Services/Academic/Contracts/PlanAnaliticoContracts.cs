namespace AcademicService.Contracts;

public sealed record PlanAnaliticoPreview(
    string Nombre,
    string Codigo,
    decimal Creditos,
    int Horas,
    string UnidadOrganizacional,
    IReadOnlyList<PlanAnaliticoTema> Temas,
    IReadOnlyList<string> Advertencias);

public sealed record PlanAnaliticoTema(
    string Nombre,
    IReadOnlyList<string> Subtemas);

public sealed record ConfirmarPlanAnaliticoRequest(string Datos);
