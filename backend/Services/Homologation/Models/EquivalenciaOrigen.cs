namespace HomologationService.Models;

// Asignatura cursada (de origen) que respalda una equivalencia.
public class EquivalenciaOrigen
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquivalenciaId { get; set; }
    public Equivalencia Equivalencia { get; set; } = null!;

    // Referencia logica a StudentsDB (AsignaturaCursada).
    public Guid AsignaturaCursadaId { get; set; }
}
