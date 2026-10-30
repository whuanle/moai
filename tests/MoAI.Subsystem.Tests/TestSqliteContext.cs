using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using Moq;

namespace MoAI.Subsystem.Tests;

/// <summary>
/// 为子系统单测提供基于 sqlite 内存库的 <see cref="DatabaseContext"/>（照 MoAI.App.Tests 同款手法）.
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
        services.AddSingleton(Mock.Of<MoAI.Infra.Services.IIdProvider>(x => x.NextId() == 1700000000000000001L)); // 软删标记位需非零 id
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
    }
}
