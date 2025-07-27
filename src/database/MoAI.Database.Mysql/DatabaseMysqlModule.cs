// <copyright file="DatabaseMysqlModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra;
using MySqlConnector;

namespace MoAI.Database;

/// <summary>
/// DatabasePostgresModule.
/// </summary>
public class DatabaseMysqlModule : IModule
{
    private readonly ILogger<DatabaseMysqlModule> _logger;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMysqlModule"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="systemOptions"></param>
    public DatabaseMysqlModule(ILogger<DatabaseMysqlModule> logger, SystemOptions systemOptions)
    {
        _logger = logger;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if (!"mysql".Equals(_systemOptions.DBType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connectionString = new MySqlConnectionStringBuilder(_systemOptions.Database);
        connectionString.GuidFormat = MySqlGuidFormat.Binary16;

        Action<DbContextOptionsBuilder> contextOptionsBuilder = o =>
        {
            o.UseMySql(connectionString.ToString(), ServerVersion.AutoDetect(_systemOptions.Database))
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

        context.Services.AddDbContext<DatabaseContext, MysqlDatabaseContext>(contextOptionsBuilder);

        try
        {
            DbContextOptionsBuilder<MysqlDatabaseContext> options = new();
            contextOptionsBuilder.Invoke(options);

            using var ioc = context.Services.BuildServiceProvider();
            using MysqlDatabaseContext? dbContext = new MysqlDatabaseContext(options.Options, ioc);

            // 如果数据库不存在，则会创建数据库及其所有表。
            // dbContext.Database.Migrate();
            dbContext.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "The database initialization failed. Please check if the database connection string is correct.");
            throw;
        }
    }
}