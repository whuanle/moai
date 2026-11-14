using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Infra.Services;
using Moq;

namespace MoAI.Message.Tests;

/// <summary>
/// 为站内信单测提供基于 sqlite 内存库的 <see cref="DatabaseContext"/>（照 MoAI.Org.Tests 同款手法）.
/// </summary>
internal sealed class TestSqliteContext : IDisposable
{
    private TestSqliteContext(DatabaseContext context, SqliteConnection connection, Mock<IIdProvider> idProvider)
    {
        Context = context;
        Connection = connection;
        IdProvider = idProvider;
    }

    public DatabaseContext Context { get; }

    public SqliteConnection Connection { get; }

    /// <summary>
    /// 雪花 id mock（软删除写入值），默认返回非零 id.
    /// </summary>
    public Mock<IIdProvider> IdProvider { get; }

    public static TestSqliteContext Create(long currentUserId = 0)
    {
        var idProvider = new Mock<IIdProvider>();
        idProvider.Setup(x => x.NextId()).Returns((Environment.TickCount64 * 1000L) + 1);

        var userContextProvider = new Mock<IUserContextProvider>();
        userContextProvider
            .Setup(x => x.GetUserContext())
            .Returns(new MoAI.Infra.Defaults.DefaultUserContext { UserId = currentUserId });

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton(idProvider.Object);
        services.AddSingleton(userContextProvider.Object);
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(connection).Options;
        var context = new TestDatabaseContext(options, provider);
        context.Database.EnsureCreated();
        return new TestSqliteContext(context, connection, idProvider);
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
