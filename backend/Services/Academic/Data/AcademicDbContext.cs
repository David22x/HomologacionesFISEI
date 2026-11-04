using AcademicService.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Data;

public class AcademicDbContext : DbContext
{
    public AcademicDbContext(DbContextOptions<AcademicDbContext> options) : base(options) { }

    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<MallaVersion> Mallas => Set<MallaVersion>();
    public DbSet<Nivel> Niveles => Set<Nivel>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
    public DbSet<Tema> Temas => Set<Tema>();
    public DbSet<Subtema> Subtemas => Set<Subtema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Carrera>(e =>
        {
            e.ToTable("Carreras");
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<MallaVersion>(e =>
        {
            e.ToTable("Mallas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.CarreraId, x.Version }).IsUnique();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(EstadoMalla.Borrador);
            e.Property(x => x.ModificadoPor).HasMaxLength(100);
            e.Property(x => x.ModificadoEn).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.CarreraId).IsUnique()
                .HasFilter("[Estado] = 'Publicada'")
                .HasDatabaseName("UX_Mallas_UnaPublicadaPorCarrera");
            e.HasOne(x => x.Carrera).WithMany(c => c.Mallas)
                .HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Nivel>(e =>
        {
            e.ToTable("Niveles", t => t.HasCheckConstraint("CK_Niveles_Numero_Positivo", "[Numero] > 0"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MallaVersionId, x.Numero }).IsUnique();
            e.HasOne(x => x.MallaVersion).WithMany(m => m.Niveles)
                .HasForeignKey(x => x.MallaVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Asignatura>(e =>
        {
            e.ToTable("Asignaturas", t => t.HasCheckConstraint("CK_Asignaturas_Creditos_Positivos", "[Creditos] > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Creditos).HasPrecision(5, 2);
            e.Property(x => x.UnidadOrganizacional).HasMaxLength(100);
            e.HasIndex(x => new { x.MallaVersionId, x.Codigo }).IsUnique();
            e.HasOne(x => x.MallaVersion).WithMany(m => m.Asignaturas)
                .HasForeignKey(x => x.MallaVersionId).OnDelete(DeleteBehavior.Cascade);
            // Sin cascada desde Nivel: SQL Server no permite multiples rutas de cascada
            e.HasOne(x => x.Nivel).WithMany(n => n.Asignaturas)
                .HasForeignKey(x => x.NivelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tema>(e =>
{
    e.ToTable("Temas", t =>
    {
        t.HasCheckConstraint("CK_Temas_Orden_Positivo", "[Orden] > 0");
        t.HasCheckConstraint("CK_Temas_Horas_Positivas", "[HorasEstimadas] IS NULL OR [HorasEstimadas] > 0");
    });
    e.HasKey(x => x.Id);
    e.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
    e.HasIndex(x => new { x.AsignaturaId, x.Nombre }).IsUnique();
    e.HasOne(x => x.Asignatura).WithMany(a => a.Temas)
        .HasForeignKey(x => x.AsignaturaId).OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<Subtema>(e =>
{
    e.ToTable("Subtemas", t =>
    {
        t.HasCheckConstraint("CK_Subtemas_Orden_Positivo", "[Orden] > 0");
        t.HasCheckConstraint("CK_Subtemas_Horas_Positivas", "[HorasEstimadas] IS NULL OR [HorasEstimadas] > 0");
    });
    e.HasKey(x => x.Id);
    e.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
    e.HasIndex(x => new { x.TemaId, x.Nombre }).IsUnique();
    e.HasOne(x => x.Tema).WithMany(t => t.Subtemas)
        .HasForeignKey(x => x.TemaId).OnDelete(DeleteBehavior.Cascade);
});
    }
}
