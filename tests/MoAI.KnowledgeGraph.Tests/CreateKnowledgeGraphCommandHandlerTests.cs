using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class CreateKnowledgeGraphCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEnabledAndAdmin_CreatesGraph()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        var result = await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);

        Assert.True(result.Value > 0);
    }

    [Fact]
    public async Task Handle_WhenDisabled_Throws409()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = false });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WhenNameDuplicated_Throws409()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Owner);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    private static SqliteScope CreateContext()
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
        return new SqliteScope(context, connection);
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override bool ShouldApplySeedData() => false;
    }

    private sealed class SqliteScope : IDisposable
    {
        public SqliteScope(DatabaseContext context, SqliteConnection connection)
        {
            Context = context;
            Connection = connection;
        }

        public DatabaseContext Context { get; }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }
    }
}
