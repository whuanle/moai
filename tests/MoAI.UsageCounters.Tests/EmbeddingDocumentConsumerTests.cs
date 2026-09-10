using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Wiki.Consumers;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class EmbeddingDocumentConsumerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTaskNotFound_DoesNothingAndDoesNotCallProcessor()
    {
        using var db = CreateContext();
        var processor = new Mock<IWikiEmbeddingProcessor>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = Guid.CreateVersion7() });

        processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskStateIsNotWait_DoesNothingAndDoesNotCallProcessor()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Successful, "{}", "done");

        var processor = new Mock<IWikiEmbeddingProcessor>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Successful, reloaded.State);
        Assert.Equal("done", reloaded.Message);
        processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskDataInvalid_MarksFailed()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, "{ bad-json", "created");

        var processor = new Mock<IWikiEmbeddingProcessor>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Failed, reloaded.State);
        Assert.Contains("任务数据", reloaded.Message, StringComparison.Ordinal);
        processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskDataHasInvalidIds_MarksFailed()
    {
        using var db = CreateContext();
        var data = JsonSerializer.Serialize(new WikiDocumentEmbeddingTaskData
        {
            WikiId = 0,
            DocumentId = -1,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        });
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, data, "created");

        var processor = new Mock<IWikiEmbeddingProcessor>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Failed, reloaded.State);
        Assert.Contains("任务数据", reloaded.Message, StringComparison.Ordinal);
        processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessSucceeds_MarksSuccessful()
    {
        using var db = CreateContext();
        var data = JsonSerializer.Serialize(new WikiDocumentEmbeddingTaskData
        {
            WikiId = 11,
            DocumentId = 22,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        });
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, data, "created");

        var processor = new Mock<IWikiEmbeddingProcessor>();
        processor
            .Setup(x => x.ProcessAsync(11, 22, true, false, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Successful, reloaded.State);
        Assert.Equal("任务已完成", reloaded.Message);
        processor.Verify();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessThrows_MarksFailedAndRethrows()
    {
        using var db = CreateContext();
        var data = JsonSerializer.Serialize(new WikiDocumentEmbeddingTaskData
        {
            WikiId = 33,
            DocumentId = 44,
            IsEmbedSourceText = true,
            IsEmbedMetadata = true,
        });
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, data, "created");

        var processor = new Mock<IWikiEmbeddingProcessor>();
        processor
            .Setup(x => x.ProcessAsync(33, 44, true, true, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("process failed"));

        var sut = CreateSut(db.Context, processor.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id }));

        Assert.Equal("process failed", ex.Message);

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Failed, reloaded.State);
        Assert.Equal("process failed", reloaded.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessThrowsAndMarkFailedThrows_RethrowsOriginalProcessException()
    {
        using var db = CreateContext();
        var data = JsonSerializer.Serialize(new WikiDocumentEmbeddingTaskData
        {
            WikiId = 33,
            DocumentId = 44,
            IsEmbedSourceText = true,
            IsEmbedMetadata = true,
        });
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, data, "created");

        var processor = new Mock<IWikiEmbeddingProcessor>();
        processor
            .Setup(x => x.ProcessAsync(33, 44, true, true, CancellationToken.None))
            .Returns(async () =>
            {
                db.Context.Dispose();
                throw new InvalidOperationException("process failed");
            });

        var sut = CreateSut(db.Context, processor.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id }));

        Assert.Equal("process failed", ex.Message);

        using var readback = CreateContext(db.Connection);
        var reloaded = await readback.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Processing, reloaded.State);
    }

    [Fact]
    public async Task FaildAsync_WhenMarkFailedPersistenceThrows_DoesNotThrow()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, "{}", "created");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());
        db.Context.Dispose();

        await sut.FaildAsync(default!, new InvalidOperationException("retry failed"), retryCount: 1, new EmbeddingDocumentTaskMessage { TaskId = task.Id });
    }

    [Fact]
    public async Task FallbackAsync_WhenMarkFailedPersistenceThrows_StillReturnsAck()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, "{}", "created");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());
        db.Context.Dispose();

        var state = await sut.FallbackAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id }, new InvalidOperationException("dead-letter"));

        Assert.Equal(ConsumerState.Ack, state);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateMessageArrives_DoesNotReprocess()
    {
        using var db = CreateContext();
        var data = JsonSerializer.Serialize(new WikiDocumentEmbeddingTaskData
        {
            WikiId = 55,
            DocumentId = 66,
            IsEmbedSourceText = true,
            IsEmbedMetadata = false,
        });
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, data, "created");

        var processor = new Mock<IWikiEmbeddingProcessor>();
        processor
            .Setup(x => x.ProcessAsync(55, 66, true, false, CancellationToken.None))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db.Context, processor.Object);

        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });
        await sut.ExecuteAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        processor.Verify(x => x.ProcessAsync(55, 66, true, false, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(WorkerState.Wait)]
    [InlineData(WorkerState.Processing)]
    public async Task FaildAsync_WhenTaskIsNonTerminal_MarksFailed(WorkerState state)
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, state, "{}", "running");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());

        await sut.FaildAsync(default!, new InvalidOperationException("retry failed"), retryCount: 2, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Failed, reloaded.State);
        Assert.Equal("retry failed", reloaded.Message);
    }

    [Theory]
    [InlineData(WorkerState.Successful)]
    [InlineData(WorkerState.Cancal)]
    [InlineData(WorkerState.Failed)]
    public async Task FaildAsync_WhenTaskIsTerminal_DoesNotOverwriteTerminal(WorkerState state)
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, state, "{}", "terminal");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());

        await sut.FaildAsync(default!, new InvalidOperationException("retry failed"), retryCount: 1, new EmbeddingDocumentTaskMessage { TaskId = task.Id });

        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)state, reloaded.State);
        Assert.Equal("terminal", reloaded.Message);
    }

    [Fact]
    public async Task FallbackAsync_WhenTaskIsNonTerminal_MarksFailedAndReturnsAck()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Wait, "{}", "created");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());

        var state = await sut.FallbackAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id }, new InvalidOperationException("dead-letter"));

        Assert.Equal(ConsumerState.Ack, state);
        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Failed, reloaded.State);
        Assert.Equal("dead-letter", reloaded.Message);
    }

    [Fact]
    public async Task FallbackAsync_WhenTaskIsTerminal_DoesNotOverwriteAndReturnsAck()
    {
        using var db = CreateContext();
        var task = await AddTaskAsync(db.Context, WorkerState.Successful, "{}", "done");

        var sut = CreateSut(db.Context, Mock.Of<IWikiEmbeddingProcessor>());

        var state = await sut.FallbackAsync(default!, new EmbeddingDocumentTaskMessage { TaskId = task.Id }, new InvalidOperationException("dead-letter"));

        Assert.Equal(ConsumerState.Ack, state);
        var reloaded = await db.Context.WorkerTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        Assert.Equal((int)WorkerState.Successful, reloaded.State);
        Assert.Equal("done", reloaded.Message);
    }

    private static EmbeddingDocumentConsumer CreateSut(DatabaseContext context, IWikiEmbeddingProcessor processor)
    {
        return new EmbeddingDocumentConsumer(context, processor, NullLogger<EmbeddingDocumentConsumer>.Instance);
    }

    private static async Task<WorkerTaskEntity> AddTaskAsync(DatabaseContext context, WorkerState state, string data, string message)
    {
        var task = new WorkerTaskEntity
        {
            Id = Guid.CreateVersion7(),
            BindType = "embedding",
            BindId = 1,
            State = (int)state,
            Message = message,
            Data = data,
            CreateTime = DateTimeOffset.UtcNow,
            UpdateTime = DateTimeOffset.UtcNow,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        };

        await context.WorkerTasks.AddAsync(task);
        await context.SaveChangesAsync();
        return task;
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
        private readonly bool _ownsConnection;

        public SqliteContextScope(DatabaseContext context, SqliteConnection connection, bool ownsConnection)
        {
            Context = context;
            Connection = connection;
            _ownsConnection = ownsConnection;
        }

        public DatabaseContext Context { get; }

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
