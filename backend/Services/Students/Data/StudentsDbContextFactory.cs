using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentsService.Data;

// Solo lo usan las herramientas de EF (dotnet ef) para no depender de JWT ni del resto del host.
public class StudentsDbContextFactory : IDesignTimeDbContextFactory<StudentsDbContext>
{
    public StudentsDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<StudentsDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("StudentsConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:StudentsConnection.");

        var options = new DbContextOptionsBuilder<StudentsDbContext>().UseSqlServer(cs).Options;
        return new StudentsDbContext(options);
    }
}
