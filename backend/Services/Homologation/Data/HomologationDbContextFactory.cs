using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomologationService.Data;

// Solo lo usan las herramientas de EF (dotnet ef) para no depender de JWT ni del resto del host.
public class HomologationDbContextFactory : IDesignTimeDbContextFactory<HomologationDbContext>
{
    public HomologationDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<HomologationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("HomologationConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:HomologationConnection.");

        var options = new DbContextOptionsBuilder<HomologationDbContext>().UseSqlServer(cs).Options;
        return new HomologationDbContext(options);
    }
}
