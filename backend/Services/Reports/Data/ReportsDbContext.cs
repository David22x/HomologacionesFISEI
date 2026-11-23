using Microsoft.EntityFrameworkCore;
using ReportsService.Models;

namespace ReportsService.Data;

public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : DbContext(options)
{
    public DbSet<Informe> Informes => Set<Informe>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Informe>(e =>
        {
            e.Property(x => x.HuellaSha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.HuellaFuente).HasMaxLength(64).IsRequired();
            e.Property(x => x.DatosJson).IsRequired();
            e.Property(x => x.Pdf).IsRequired();
            e.HasIndex(x => new { x.ExpedienteId, x.Version }).IsUnique();
            e.HasIndex(x => new { x.ExpedienteId, x.HuellaFuente }).IsUnique();
        });
    }
}
