using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReportsService.Data;

public sealed class ReportsDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder().AddUserSecrets<ReportsDbContextFactory>(optional: true)
            .AddEnvironmentVariables().Build();
        var connection = config.GetConnectionString("ReportsConnection")
            ?? throw new InvalidOperationException("Configura ConnectionStrings:ReportsConnection.");
        return new(new DbContextOptionsBuilder<ReportsDbContext>().UseSqlServer(connection).Options);
    }
}
