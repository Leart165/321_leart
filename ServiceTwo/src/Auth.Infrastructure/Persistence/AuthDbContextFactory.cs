using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auth.Infrastructure.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5434;Database=auth;Username=auth;Password=auth";

    public AuthDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__AuthDb")
            ?? LocalDevelopmentConnection;

        DbContextOptionsBuilder<AuthDbContext> builder = new DbContextOptionsBuilder<AuthDbContext>();
        builder.UseNpgsql(connectionString);

        return new AuthDbContext(builder.Options);
    }
}
