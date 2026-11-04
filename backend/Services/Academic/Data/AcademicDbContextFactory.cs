using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AcademicService.Data;

// Solo lo usan las herramientas de EF (dotnet ef) para no depender de JWT ni del resto del host.
public class AcademicDbContextFactory : IDesignTimeDbContextFactory<AcademicDbContext>
{
    public AcademicDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<AcademicDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("AcademicConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:AcademicConnection.");

        var options = new DbContextOptionsBuilder<AcademicDbContext>().UseSqlServer(cs).Options;
        return new AcademicDbContext(options);
    }
}
