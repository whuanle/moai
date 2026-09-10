using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Handlers;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class QueryWikiDocumentEmbeddingCommandHandlerTests
{
    private const int WikiId = 701;
    private const int DocumentId = 702;

    [Fact]
    public async Task Handle_WhenActiveTaskExists_PrefersActiveEvenIfTerminalIsNewer()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("11111111-1111-1111-1111-111111111111"), (int)WorkerState.Successful, "terminal newer", DateTimeOffset.Parse("2026-09-09T12:00:00+00:00")),
            new WorkerTaskSeed(Guid.Parse("22222222-2222-2222-2222-222222222222"), (int)WorkerState.Wait, "active older", DateTimeOffset.Parse("2026-09-09T11:59:00+00:00")));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), response.TaskId);
        Assert.Equal((int)WorkerState.Wait, response.TaskState);
        Assert.Equal("active older", response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenMultipleActiveTasksExist_SelectsNewestActiveTask()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("33333333-3333-3333-3333-333333333333"), (int)WorkerState.Wait, "active old", DateTimeOffset.Parse("2026-09-09T11:00:00+00:00")),
            new WorkerTaskSeed(Guid.Parse("44444444-4444-4444-4444-444444444444"), (int)WorkerState.Processing, "active new", DateTimeOffset.Parse("2026-09-09T11:01:00+00:00")));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), response.TaskId);
        Assert.Equal((int)WorkerState.Processing, response.TaskState);
        Assert.Equal("active new", response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenNoActiveTaskExists_SelectsNewestTerminalTask()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("55555555-5555-5555-5555-555555555555"), (int)WorkerState.Failed, "terminal old", DateTimeOffset.Parse("2026-09-09T08:00:00+00:00")),
            new WorkerTaskSeed(Guid.Parse("66666666-6666-6666-6666-666666666666"), (int)WorkerState.Successful, "terminal new", DateTimeOffset.Parse("2026-09-09T09:00:00+00:00")));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("66666666-6666-6666-6666-666666666666"), response.TaskId);
        Assert.Equal((int)WorkerState.Successful, response.TaskState);
        Assert.Equal("terminal new", response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenOtherBindTypeTaskExists_IgnoresIt()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("77777777-7777-7777-7777-777777777777"), (int)WorkerState.Wait, "wrong bind type", DateTimeOffset.Parse("2026-09-09T10:00:00+00:00"), BindType: "partition"),
            new WorkerTaskSeed(Guid.Parse("88888888-8888-8888-8888-888888888888"), (int)WorkerState.Failed, "embedding terminal", DateTimeOffset.Parse("2026-09-09T09:59:00+00:00")));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("88888888-8888-8888-8888-888888888888"), response.TaskId);
        Assert.Equal((int)WorkerState.Failed, response.TaskState);
        Assert.Equal("embedding terminal", response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenOtherDocumentTaskExists_IgnoresIt()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("12121212-1212-1212-1212-121212121212"), (int)WorkerState.Processing, "other document active newer", DateTimeOffset.Parse("2026-09-09T10:10:00+00:00"), BindId: DocumentId + 1),
            new WorkerTaskSeed(Guid.Parse("13131313-1313-1313-1313-131313131313"), (int)WorkerState.Failed, "target document terminal", DateTimeOffset.Parse("2026-09-09T10:00:00+00:00")));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("13131313-1313-1313-1313-131313131313"), response.TaskId);
        Assert.Equal((int)WorkerState.Failed, response.TaskState);
        Assert.Equal("target document terminal", response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenNoTaskExists_ReturnsNullTaskFields()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Null(response.TaskId);
        Assert.Null(response.TaskState);
        Assert.Null(response.TaskMessage);
    }

    [Fact]
    public async Task Handle_WhenCreateTimeSame_UsesStableIdOrdering()
    {
        using var db = CreateContext();
        await SeedBaseDataAsync(db.Context);
        var sameTime = DateTimeOffset.Parse("2026-09-09T14:00:00+00:00");
        await SeedTasksAsync(
            db.Context,
            new WorkerTaskSeed(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), (int)WorkerState.Wait, "id later", sameTime),
            new WorkerTaskSeed(Guid.Parse("99999999-9999-9999-9999-999999999999"), (int)WorkerState.Wait, "id earlier", sameTime));

        var sut = CreateHandler(db.Context);
        var response = await sut.Handle(CreateRequest(), CancellationToken.None);

        Assert.Equal(Guid.Parse("99999999-9999-9999-9999-999999999999"), response.TaskId);
        Assert.Equal("id earlier", response.TaskMessage);
    }

    private static QueryWikiDocumentEmbeddingCommand CreateRequest()
    {
        return new QueryWikiDocumentEmbeddingCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
        };
    }

    private static QueryWikiDocumentEmbeddingCommandHandler CreateHandler(
        DatabaseContext context,
        ITeamService? teamService = null,
        IUserContextProvider? userContextProvider = null)
    {
        return new QueryWikiDocumentEmbeddingCommandHandler(
            context,
            teamService ?? CreateTeamService(),
            userContextProvider ?? CreateUserContextProvider(),
            CreateVectorStore());
    }

    private static IWikiEmbeddingVectorStore CreateVectorStore()
    {
        var store = new Mock<IWikiEmbeddingVectorStore>();
        store
            .Setup(x => x.CountDocumentVectorsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return store.Object;
    }

    private static SqliteContextScope CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdProvider>(CreateIdProvider());
        serviceCollection.AddSingleton(CreateUserContextProvider());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDatabaseContext(options, serviceProvider);
        context.Database.EnsureCreated();

        return new SqliteContextScope(context, connection);
    }

    private static async Task SeedBaseDataAsync(DatabaseContext context)
    {
        await context.Wikis.AddAsync(new WikiEntity
        {
            Id = WikiId,
            TeamId = 300,
            Name = "wiki",
            Description = "desc",
            AvatarPath = string.Empty,
            EmbeddingModelId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            EmbeddingDimensions = 1024,
        });

        await context.WikiDocuments.AddAsync(new WikiDocumentEntity
        {
            Id = DocumentId,
            WikiId = WikiId,
            FileId = 1,
            ObjectKey = "doc.md",
            FileName = "doc.md",
            FileType = ".md",
            SliceConfig = "{}",
        });

        await context.WikiDocumentContents.AddAsync(new WikiDocumentContentEntity
        {
            Id = 900,
            WikiId = WikiId,
            DocumentId = DocumentId,
            Content = "seeded content",
        });

        await context.WikiDocumentChunkContents.AddAsync(new WikiDocumentChunkContentEntity
        {
            Id = 901,
            WikiId = WikiId,
            DocumentId = DocumentId,
            SliceContent = "chunk",
            SliceOrder = 1,
            SliceLength = 5,
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedTasksAsync(TestDatabaseContext context, params WorkerTaskSeed[] tasks)
    {
        if (tasks.Length == 0)
        {
            return;
        }

        var entities = tasks.Select(x => new WorkerTaskEntity
        {
            Id = x.Id,
            BindType = x.BindType,
            BindId = x.BindId,
            State = x.State,
            Message = x.Message,
            Data = "{}",
            IsDeleted = x.IsDeleted,
        }).ToArray();

        await context.WorkerTasks.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        for (var i = 0; i < entities.Length; i++)
        {
            entities[i].CreateTime = tasks[i].CreateTime;
        }

        await context.SaveChangesAsync();
    }

    private static IIdProvider CreateIdProvider()
    {
        var provider = new Mock<IIdProvider>();
        provider.Setup(x => x.NextId()).Returns(1);
        provider.Setup(x => x.GeneratorId(out It.Ref<string>.IsAny)).Returns(1);
        provider.Setup(x => x.GeneratorKey()).Returns("k");
        return provider.Object;
    }

    private static IUserContextProvider CreateUserContextProvider()
    {
        var provider = new Mock<IUserContextProvider>();
        provider.Setup(x => x.GetUserContext()).Returns(new DefaultUserContext
        {
            IsAuthenticated = true,
            UserId = 1001,
            UserName = "tester",
            NickName = "tester",
            Email = "tester@example.com",
        });
        return provider.Object;
    }

    private static ITeamService CreateTeamService()
    {
        var service = new Mock<ITeamService>();
        service
            .Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Member);
        return service.Object;
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override bool ShouldApplySeedData() => false;
    }

    private sealed class SqliteContextScope : IDisposable
    {
        public SqliteContextScope(TestDatabaseContext context, SqliteConnection connection)
        {
            Context = context;
            Connection = connection;
        }

        public TestDatabaseContext Context { get; }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }
    }

    private sealed record WorkerTaskSeed(
        Guid Id,
        int State,
        string Message,
        DateTimeOffset CreateTime,
        string BindType = "embedding",
        int BindId = DocumentId,
        long IsDeleted = 0);
}
