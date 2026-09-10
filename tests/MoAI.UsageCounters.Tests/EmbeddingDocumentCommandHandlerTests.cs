using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Handlers;
using Moq;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class EmbeddingDocumentCommandHandlerTests
{
    private const int WikiId = 101;
    private const int DocumentId = 202;

    [Fact]
    public async Task Handle_WhenBothEmbeddingFlagsFalse_ThrowsBusinessException400()
    {
        using var db = CreateContext();
        var context = db.Context;
        var sut = CreateHandler(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = false,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WhenMetadataOnlyAndOnlyOrphanMetadata_ThrowsBusinessException409()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: false);

        await context.WikiDocumentChunkMetadata.AddAsync(new WikiDocumentChunkMetadatumEntity
        {
            Id = 1,
            WikiId = WikiId,
            DocumentId = DocumentId,
            ChunkId = 999999,
            MetadataType = 1,
            MetadataContent = "orphan metadata",
        });
        await context.SaveChangesAsync();

        var sut = CreateHandler(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = false,
            IsEmbedMetadata = true,
        }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("文档尚无可用元数据", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenSourceTextOnlyAndNoChunks_ThrowsBusinessException409_WithoutPublishingOrTaskCreation()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: false);

        var publisher = new Mock<IMessagePublisher>();
        var sut = CreateHandler(context, messagePublisher: publisher.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(context.WorkerTasks.ToList());
        publisher.Verify(x => x.AutoPublishAsync(
            It.IsAny<EmbeddingDocumentTaskMessage>(),
            It.IsAny<Action<BasicProperties>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActiveTaskExists_ThrowsBusinessException409()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: true);

        await context.WorkerTasks.AddAsync(new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = "embedding",
            BindId = DocumentId,
            State = (int)WorkerState.Wait,
            Message = "active",
            Data = "{}",
        });
        await context.SaveChangesAsync();

        var sut = CreateHandler(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("进行中的向量化任务", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_CreatesWorkerTaskAndPublishesSameTaskId()
    {
        using var db = CreateContext();
        var context = db.Context;
        var chunkId = await SeedValidWikiAndDocumentAsync(context, includeChunk: true);

        await context.WikiDocumentChunkMetadata.AddAsync(new WikiDocumentChunkMetadatumEntity
        {
            Id = 2,
            WikiId = WikiId,
            DocumentId = DocumentId,
            ChunkId = chunkId,
            MetadataType = 1,
            MetadataContent = "valid metadata",
        });
        await context.SaveChangesAsync();

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.AutoPublishAsync(
                It.IsAny<EmbeddingDocumentTaskMessage>(),
                It.IsAny<Action<BasicProperties>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateHandler(context, messagePublisher: publisher.Object);

        var response = await sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = true,
        }, CancellationToken.None);

        var task = Assert.Single(context.WorkerTasks.ToList());
        Assert.Equal(response.TaskId, task.Id);
        Assert.Equal((int)WorkerState.Wait, task.State);

        var data = JsonSerializer.Deserialize<WikiDocumentEmbeddingTaskData>(task.Data);
        Assert.NotNull(data);
        Assert.Equal(WikiId, data!.WikiId);
        Assert.Equal(DocumentId, data.DocumentId);
        Assert.True(data.IsEmbedSourceText);
        Assert.True(data.IsEmbedMetadata);

        var invocation = Assert.Single(publisher.Invocations, x => x.Method.Name == nameof(IMessagePublisher.AutoPublishAsync));
        var message = Assert.IsType<EmbeddingDocumentTaskMessage>(invocation.Arguments[0]);
        Assert.Equal(response.TaskId, message.TaskId);
    }

    [Fact]
    public async Task Handle_WhenPublishFails_MarksTaskAsFailedAndRethrows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var db = CreateContext(connection);
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: true);

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(x => x.AutoPublishAsync(
                It.IsAny<EmbeddingDocumentTaskMessage>(),
                It.IsAny<Action<BasicProperties>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publish failed"));

        var sut = CreateHandler(context, messagePublisher: publisher.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal("publish failed", ex.Message);

        using var readback = CreateContext(connection);
        var task = Assert.Single(await readback.Context.WorkerTasks.AsNoTracking().ToListAsync());
        Assert.Equal((int)WorkerState.Failed, task.State);
        Assert.Equal("publish failed", task.Message);
    }

    [Fact]
    public async Task Handle_WhenContentIsWhitespace_ThrowsBusinessException409()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: true, content: "   \r\n\t  ");

        var sut = CreateHandler(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("文档尚未提取内容", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenSaveThrowsActiveTaskUniqueViolation_MapsToBusinessException409()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: true);
        context.DbUpdateExceptionToThrowOnSave = CreateActiveTaskUniqueViolationException();

        var publisher = new Mock<IMessagePublisher>();
        var sut = CreateHandler(context, messagePublisher: publisher.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("进行中的向量化任务", ex.Message, StringComparison.Ordinal);
        publisher.Verify(x => x.AutoPublishAsync(
            It.IsAny<EmbeddingDocumentTaskMessage>(),
            It.IsAny<Action<BasicProperties>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveThrowsOtherDbUpdateException_BubblesSameExceptionEvenIfActiveTaskAppears()
    {
        using var db = CreateContext();
        var context = db.Context;
        await SeedValidWikiAndDocumentAsync(context, includeChunk: true);

        var expected = new DbUpdateException("other save failure", new InvalidOperationException("other"));
        context.DbUpdateExceptionToThrowOnSave = expected;
        context.InjectActiveTaskBeforeThrowOnSave = true;

        var publisher = new Mock<IMessagePublisher>();
        var sut = CreateHandler(context, messagePublisher: publisher.Object);

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() => sut.Handle(new EmbeddingDocumentCommand
        {
            WikiId = WikiId,
            DocumentId = DocumentId,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        }, CancellationToken.None));

        Assert.Same(expected, actual);
        publisher.Verify(x => x.AutoPublishAsync(
            It.IsAny<EmbeddingDocumentTaskMessage>(),
            It.IsAny<Action<BasicProperties>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DbUpdateException CreateActiveTaskUniqueViolationException()
    {
        var postgresException = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation,
            constraintName: "ux_worker_task_bind_active");

        return new DbUpdateException("save failed", new InvalidOperationException("wrapped", postgresException));
    }

    private static EmbeddingDocumentCommandHandler CreateHandler(
        DatabaseContext context,
        ITeamService? teamService = null,
        IUserContextProvider? userContextProvider = null,
        IMessagePublisher? messagePublisher = null)
    {
        var userProvider = userContextProvider ?? CreateUserContextProvider();
        return new EmbeddingDocumentCommandHandler(
            context,
            teamService ?? CreateTeamService(),
            userProvider,
            messagePublisher ?? Mock.Of<IMessagePublisher>());
    }

    private static SqliteContextScope CreateContext(SqliteConnection? sharedConnection = null)
    {
        var ownConnection = sharedConnection == null;
        var connection = sharedConnection ?? new SqliteConnection("Data Source=:memory:");
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<MoAI.Infra.Services.IIdProvider>(CreateIdProvider());
        serviceCollection.AddSingleton(CreateUserContextProvider());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDatabaseContext(options, serviceProvider);
        context.Database.EnsureCreated();
        return new SqliteContextScope(context, connection, ownConnection);
    }

    private static MoAI.Infra.Services.IIdProvider CreateIdProvider()
    {
        var provider = new Mock<MoAI.Infra.Services.IIdProvider>();
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

    private static async Task<long> SeedValidWikiAndDocumentAsync(DatabaseContext context, bool includeChunk, string content = "content")
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
            ObjectKey = "test.md",
            FileName = "test.md",
            FileType = ".md",
            SliceConfig = "{}",
        });

        await context.WikiDocumentContents.AddAsync(new WikiDocumentContentEntity
        {
            Id = 10,
            WikiId = WikiId,
            DocumentId = DocumentId,
            Content = content,
        });

        var chunkId = 0L;
        if (includeChunk)
        {
            chunkId = 20;
            await context.WikiDocumentChunkContents.AddAsync(new WikiDocumentChunkContentEntity
            {
                Id = chunkId,
                WikiId = WikiId,
                DocumentId = DocumentId,
                SliceContent = "chunk content",
                SliceOrder = 1,
                SliceLength = 12,
            });
        }

        await context.SaveChangesAsync();
        return chunkId;
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        public DbUpdateException? DbUpdateExceptionToThrowOnSave { get; set; }

        public bool InjectActiveTaskBeforeThrowOnSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (DbUpdateExceptionToThrowOnSave != null
                && ChangeTracker.Entries<WorkerTaskEntity>().Any(x => x.State == EntityState.Added && x.Entity.BindType == "embedding"))
            {
                if (InjectActiveTaskBeforeThrowOnSave)
                {
                    WorkerTasks.Add(new WorkerTaskEntity
                    {
                        Id = Guid.CreateVersion7(),
                        BindType = "embedding",
                        BindId = DocumentId,
                        State = (int)WorkerState.Wait,
                        Message = "concurrent active",
                        Data = "{}",
                    });
                }

                throw DbUpdateExceptionToThrowOnSave;
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override bool ShouldApplySeedData() => false;
    }

    private sealed class SqliteContextScope : IDisposable
    {
        private readonly bool _ownsConnection;

        public SqliteContextScope(TestDatabaseContext context, SqliteConnection connection, bool ownsConnection)
        {
            Context = context;
            Connection = connection;
            _ownsConnection = ownsConnection;
        }

        public TestDatabaseContext Context { get; }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Context.Dispose();
            if (_ownsConnection)
            {
                Connection.Dispose();
            }
        }
    }
}
