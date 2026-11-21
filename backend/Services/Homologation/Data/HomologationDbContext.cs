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

    public DbSet<Decision> Decisiones => Set<Decision>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var expedientes = ChangeTracker.Entries<Expediente>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach (var expediente in expedientes)
        {
            var id = expediente.Entity.Id;
            var cambios = ChangeTracker.Entries()
                .Where(e => e.Entity is not EventoAuditoria
                    && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity == expediente.Entity
                    || e.Metadata.FindProperty("ExpedienteId") is not null
                    && (Guid)e.Property("ExpedienteId").CurrentValue! == id)
                .Select(e => new
                {
                    entidad = e.Metadata.ClrType.Name,
                    operacion = e.State.ToString(),
                    antes = e.State == EntityState.Added ? null :
                        e.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue),
                    despues = e.State == EntityState.Deleted ? null :
                        e.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)
                }).ToArray();
            // Solo se serializan propiedades escalares para evitar ciclos de navegación.
            var opciones = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
            EventosAuditoria.Add(new EventoAuditoria
            {
                ExpedienteId = id,
                ActorId = expediente.Entity.ProfesorId,
                CambiosJson = System.Text.Json.JsonSerializer.Serialize(cambios, opciones)
            });
        }
        return base.SaveChangesAsync(cancellationToken);
    }

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
                .HasFilter("[Estado] IN ('Borrador','EnRevision','AnalizadoConIa')");

            e.HasIndex(x => x.ProfesorId);

            e.HasMany(x => x.Equivalencias)
                .WithOne(x => x.Expediente)
                .HasForeignKey(x => x.ExpedienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Decision>(e =>
        {
            e.HasOne<Expediente>().WithMany().HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Justificacion).HasMaxLength(2000);
            e.HasIndex(x => new { x.ExpedienteId, x.AsignaturaDestinoId });
        });
        modelBuilder.Entity<EventoAuditoria>(e =>
        {
            e.HasOne<Expediente>().WithMany().HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExpedienteId, x.Fecha });
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
            e.HasIndex(x => x.ExpedienteId).IsUnique().HasFilter("[EsAnalisisIa] = 1");
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
