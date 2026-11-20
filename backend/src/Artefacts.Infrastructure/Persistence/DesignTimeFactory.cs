using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Artefacts.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only. Override the connection with the ARTEFACTS_DB environment variable.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CollectionDbContext>
{
    public CollectionDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ARTEFACTS_DB")
                         ?? "Host=localhost;Port=5432;Database=artefacts;Username=artefacts;Password=artefacts";
        var options = new DbContextOptionsBuilder<CollectionDbContext>()
            .UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CollectionDbContext(options);
    }
}
