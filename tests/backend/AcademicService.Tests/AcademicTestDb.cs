using AcademicService.Data;
using AcademicService.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AcademicService.Tests;

/// <summary>Base SQLite en memoria: aplica unicidad, CHECK y claves foraneas como SQL Server.</summary>
public sealed class AcademicTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public AcademicTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Options = new DbContextOptionsBuilder<AcademicDbContext>().UseSqlite(_connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public DbContextOptions<AcademicDbContext> Options { get; }

    public AcademicDbContext CreateContext() => new SqliteAcademicDbContext(Options);

    public void Dispose() => _connection.Dispose();

    // SQLite guarda decimal como TEXT y el CHECK (Creditos > 0) compararia texto con numero.
    // Solo en pruebas se guardan como REAL para que el CHECK se comporte como en SQL Server.
    private sealed class SqliteAcademicDbContext : AcademicDbContext
    {
        public SqliteAcademicDbContext(DbContextOptions<AcademicDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Asignatura>().Property(a => a.Creditos).HasConversion<double>();
        }
    }
}
