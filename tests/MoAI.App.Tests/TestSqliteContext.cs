using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using Moq;

namespace MoAI.App.Tests;

/// <summary>
/// 为应用单测提供基于 sqlite 内存库的 <see cref="DatabaseContext"/>（照 MoAI.KnowledgeGraph.Tests 同款手法）.
/// </summary>
internal sealed class TestSqliteContext : IDisposable
{
    private TestSqliteContext(DatabaseContext context, SqliteConnection connection)
    {
        Context = context;
        Connection = connection;
    }

    public DatabaseContext Context { get; }

    public SqliteConnection Connection { get; }

    public static TestSqliteContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<MoAI.Infra.Services.IIdProvider>());
        services.AddSingleton(Mock.Of<MoAI.Infra.Services.IUserContextProvider>());
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(connection).Options;
        var context = new TestDatabaseContext(options, provider);
        context.Database.EnsureCreated();
        return new TestSqliteContext(context, connection);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override bool ShouldApplySeedData() => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // sqlite 约定建表无 Postgres 列默认值；补齐保存入口建配置行时依赖 DB 默认的 JSON 列（prompts DEFAULT '[]'）
            modelBuilder.Entity<AppAgentConfigEntity>()
                .Property(x => x.Prompts)
                .HasDefaultValueSql("'[]'");
        }
    }
}
