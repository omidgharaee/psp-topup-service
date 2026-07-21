using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PSP.TopupService.Persistence.Context;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> can construct a
/// <see cref="TopupDbContext"/> without running the application's DI pipeline.
/// The connection string comes from the environment or defaults to localhost.
/// </summary>
public sealed class TopupDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TopupDbContext>
{
    public TopupDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TopupDbContext>()
            .UseNpgsql(
                Environment.GetEnvironmentVariable("ConnectionStrings__TopupDatabase")
                    ?? "Host=localhost;Port=5432;Database=topup;Username=postgres;Password=postgres",
                npg => npg.MigrationsAssembly(typeof(TopupDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TopupDbContext(options);
    }
}
