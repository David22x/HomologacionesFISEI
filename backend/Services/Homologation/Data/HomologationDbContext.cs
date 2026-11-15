using HomologationService.Models;
using Microsoft.EntityFrameworkCore;

namespace HomologationService.Data;

public class HomologationDbContext : DbContext
{
    public HomologationDbContext(DbContextOptions<HomologationDbContext> options) : base(options)
    {
    }

    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<Equivalencia> Equivalencias => Set<Equivalencia>();
    public DbSet<EquivalenciaOrigen> EquivalenciasOrigen => Set<EquivalenciaOrigen>();

    public DbSet<ParHomologacion> Pares => Set<ParHomologacion>();
    public DbSet<EjecucionReglas> EjecucionesReglas => Set<EjecucionReglas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expediente>(e =>
        {
            e.ToTable("Expedientes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ActualizadoEn).IsConcurrencyToken();
            e.Property(x => x.ProfesorNombre).HasMaxLength(200).IsRequired();

            // Solo un expediente activo por estudiante y carrera de destino.
            e.HasIndex(x => new { x.EstudianteId, x.CarreraDestinoId })
                .IsUnique()
                .HasDatabaseName("UX_Expedientes_UnoActivoPorEstudianteYCarrera")
                .HasFilter("[Estado] IN ('Borrador','EnRevision')");

            e.HasIndex(x => x.ProfesorId);

            e.HasMany(x => x.Equivalencias)
                .WithOne(x => x.Expediente)
                .HasForeignKey(x => x.ExpedienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParHomologacion>(e =>
        {
            e.ToTable("ParesHomologacion");
            e.HasOne<Expediente>().WithMany().HasForeignKey(x => x.ExpedienteId);
            e.HasIndex(x => new { x.ExpedienteId, x.AsignaturaCursadaId, x.AsignaturaDestinoId }).IsUnique();
        });

        modelBuilder.Entity<EjecucionReglas>(e =>
        {
            e.ToTable("EjecucionesReglas");
            e.Property(x => x.ResultadosJson).IsRequired();
            e.HasOne<Expediente>().WithMany().HasForeignKey(x => x.ExpedienteId);
            e.HasIndex(x => new { x.ExpedienteId, x.EjecutadoEn });
        });

        modelBuilder.Entity<Equivalencia>(e =>
        {
            e.ToTable("Equivalencias");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ExpedienteId, x.AsignaturaDestinoId }).IsUnique();

            e.HasMany(x => x.Origenes)
                .WithOne(x => x.Equivalencia)
                .HasForeignKey(x => x.EquivalenciaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquivalenciaOrigen>(e =>
        {
            e.ToTable("EquivalenciasOrigen");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EquivalenciaId, x.AsignaturaCursadaId }).IsUnique();
        });
    }
}
