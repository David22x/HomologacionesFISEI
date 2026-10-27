using Microsoft.EntityFrameworkCore;
using StudentsService.Models;

namespace StudentsService.Data;

public class StudentsDbContext : DbContext
{
    public StudentsDbContext(DbContextOptions<StudentsDbContext> options) : base(options) { }

    public DbSet<Estudiante> Estudiantes => Set<Estudiante>();
    public DbSet<HistorialAcademico> Historiales => Set<HistorialAcademico>();
    public DbSet<AsignaturaCursada> AsignaturasCursadas => Set<AsignaturaCursada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Estudiante>(e =>
        {
            e.ToTable("Estudiantes", t => t.HasCheckConstraint(
                "CK_Estudiantes_Cedula_Formato",
                "LEN([Cedula]) = 10 AND [Cedula] NOT LIKE '%[^0-9]%'"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Cedula).HasMaxLength(10).IsRequired();
            e.Property(x => x.Nombres).HasMaxLength(150).IsRequired();
            e.Property(x => x.Apellidos).HasMaxLength(150).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.CreadoPor).HasMaxLength(100);
            e.Property(x => x.ModificadoPor).HasMaxLength(100);
            e.HasIndex(x => x.Cedula).IsUnique();
        });

        modelBuilder.Entity<HistorialAcademico>(e =>
        {
            e.ToTable("HistorialesAcademicos", t => t.HasCheckConstraint(
                "CK_Historiales_NivelActual_Positivo",
                "[NivelActual] IS NULL OR [NivelActual] > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.CarreraOrigenNombre).HasMaxLength(200);
            e.Property(x => x.InstitucionOrigen).HasMaxLength(200);
            // Un historial por estudiante: una nueva confirmacion lo reemplaza.
            e.HasIndex(x => x.EstudianteId).IsUnique();
            e.HasOne(x => x.Estudiante).WithOne(s => s.Historial)
                .HasForeignKey<HistorialAcademico>(x => x.EstudianteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AsignaturaCursada>(e =>
        {
            e.ToTable("AsignaturasCursadas", t =>
            {
                t.HasCheckConstraint("CK_AsignaturasCursadas_Nota_Rango", "[Nota] >= 0 AND [Nota] <= 10");
                t.HasCheckConstraint("CK_AsignaturasCursadas_Creditos_Positivos", "[Creditos] IS NULL OR [Creditos] > 0");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(30);
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Creditos).HasPrecision(5, 2);
            e.Property(x => x.Nota).HasPrecision(5, 2);
            e.Property(x => x.Periodo).HasMaxLength(30).IsRequired();
            // Una misma asignatura del catalogo no se repite en el historial.
            e.HasIndex(x => new { x.HistorialAcademicoId, x.AsignaturaId })
                .IsUnique()
                .HasFilter("[AsignaturaId] IS NOT NULL");
            e.HasOne(x => x.Historial).WithMany(h => h.Asignaturas)
                .HasForeignKey(x => x.HistorialAcademicoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
