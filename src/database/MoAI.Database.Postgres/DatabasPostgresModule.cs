using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace MoAI.Database;

/// <summary>
/// DatabasPostgresModule.
/// </summary>
public class DatabasPostgresModule : IModule
{
    private readonly ILogger<DatabasPostgresModule> _logger;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabasPostgresModule"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="systemOptions"></param>
    public DatabasPostgresModule(ILogger<DatabasPostgresModule> logger, SystemOptions systemOptions)
    {
        _logger = logger;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if (!"postgres".Equals(_systemOptions.DBType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

        var connectionString = new NpgsqlDataSourceBuilder(_systemOptions.Database);

        Action<DbContextOptionsBuilder> contextOptionsBuilder = o =>
        {
            o.UseNpgsql(_systemOptions.Database)
                .ConfigureWarnings(
                    b => b.Ignore([
                        CoreEventId.ServiceProviderCreated,
                        CoreEventId.ContextInitialized,
                        CoreEventId.ContextDisposed,
                        CoreEventId.LazyLoadOnDisposedContextWarning,
                        CoreEventId.QueryCompilationStarting,
                        CoreEventId.StateChanged,
                        CoreEventId.SaveChangesCanceled,
                        CoreEventId.SaveChangesCompleted,
                        CoreEventId.SensitiveDataLoggingEnabledWarning,
                        CoreEventId.QueryExecutionPlanned,
                        CoreEventId.StartedTracking,
                        RelationalEventId.ConnectionOpening,
                        RelationalEventId.ConnectionCreating,
                        RelationalEventId.ConnectionCreated,
                        RelationalEventId.ConnectionClosing,
                        RelationalEventId.ConnectionClosed,
                        RelationalEventId.DataReaderClosing,
                        RelationalEventId.DataReaderDisposing,
                        RelationalEventId.CommandCanceled,
                        RelationalEventId.CommandCreated,
                        RelationalEventId.CommandCreating,
                        RelationalEventId.CommandInitialized,
                        RelationalEventId.BoolWithDefaultWarning,
                        RelationalEventId.ModelValidationKeyDefaultValueWarning
                    ]))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors();
        };

        context.Services.AddDbContext<DatabaseContext, PostgresDatabaseContext>(contextOptionsBuilder);

        try
        {
            DbContextOptionsBuilder<PostgresDatabaseContext> options = new();
            contextOptionsBuilder.Invoke(options);

            using var ioc = context.Services.BuildServiceProvider();
            using PostgresDatabaseContext? dbContext = new PostgresDatabaseContext(options.Options, ioc);

            // 如果数据库不存在，则会创建数据库及其所有表。
            //dbContext.Database.Migrate();
            dbContext.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "The database initialization failed. Please check if the database connection string is correct.");
            throw;
        }
    }
}