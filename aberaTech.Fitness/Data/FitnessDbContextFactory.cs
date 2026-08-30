using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace aberaTech.Fitness.Data;

/// <summary>
/// Lets `dotnet ef` build the model without starting the web application.
/// </summary>
/// <remarks>
/// Design time only. The connection string here is never opened: generating a
/// migration needs a provider so it knows what SQL to write, not a database to
/// write it to. Pointing the tooling at this library rather than at the server
/// also keeps migrations buildable without the Node toolchain the server's
/// client project reference drags in.
/// </remarks>
public sealed class FitnessDbContextFactory : IDesignTimeDbContextFactory<FitnessDbContext>
{
    public FitnessDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FitnessDbContext>()
            .UseNpgsql("Host=localhost;Database=fitness", npgsql => npgsql.UseNodaTime())
            .Options;

        return new FitnessDbContext(options);
    }
}
