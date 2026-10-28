namespace HomologationService.Models;

// Una asignatura de destino dentro del expediente. Puede tener varias asignaturas
// de origen (homologacion compuesta). Se usa a partir del Sprint 3.
public class Equivalencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public Expediente Expediente { get; set; } = null!;

    // Referencia logica a AcademicDB.
    public Guid AsignaturaDestinoId { get; set; }

    public List<EquivalenciaOrigen> Origenes { get; set; } = new();
}
