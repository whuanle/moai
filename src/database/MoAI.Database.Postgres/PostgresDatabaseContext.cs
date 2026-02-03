using Microsoft.EntityFrameworkCore;

namespace MoAI.Database;

/// <summary>
/// Mysql 数据库.
/// </summary>
public class PostgresDatabaseContext : DatabaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresDatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    public PostgresDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options, serviceProvider)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(PostgresDatabaseContext).Assembly)
            .ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);

        modelBuilder.HasPostgresExtension("vector");

        // postgres 需要开启此扩展，以便支持 uuid_generate_v4()
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder
            .UseCollation("UTF8");

        base.OnModelCreating(modelBuilder);
    }
}
