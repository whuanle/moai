using Microsoft.EntityFrameworkCore;

namespace MoAI.Database;

public class MysqlDatabaseContext : DatabaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MysqlDatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="contextOptions"></param>
    public MysqlDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider, DatabaseOptions contextOptions)
        : base(options, serviceProvider, contextOptions)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(_contextOptions.ConfigurationAssembly)
            .ApplyConfigurationsFromAssembly(_contextOptions.EntityAssembly);
        OnModelCreatingPartial(modelBuilder);

        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        base.OnModelCreating(modelBuilder);
    }
}
