using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Audits;
using MoAI.Database.Entities;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class WorkerTaskModelTests
{
    [Fact]
    public void WorkerTaskEntity_DefinesExpectedPropertiesAndDefaults()
    {
        var entity = new WorkerTaskEntity();

        Assert.IsAssignableFrom<IFullAudited>(entity);
        Assert.Equal(typeof(Guid), typeof(WorkerTaskEntity).GetProperty(nameof(WorkerTaskEntity.Id))?.PropertyType);
        Assert.NotNull(entity.BindType);
        Assert.NotNull(entity.Message);
        Assert.Equal("{}", entity.Data);
    }

    [Fact]
    public void WorkerState_DefinesExpectedPersistedValues()
    {
        Assert.Equal(0, (int)WorkerState.None);
        Assert.Equal(1, (int)WorkerState.Wait);
        Assert.Equal(2, (int)WorkerState.Processing);
        Assert.Equal(3, (int)WorkerState.Cancal);
        Assert.Equal(4, (int)WorkerState.Successful);
        Assert.Equal(5, (int)WorkerState.Failed);
    }

    [Fact]
    public void DatabaseContext_ContainsWorkerTasksDbSet()
    {
        var property = typeof(DatabaseContext).GetProperty(nameof(DatabaseContext.WorkerTasks));

        Assert.NotNull(property);
        Assert.Equal(typeof(DbSet<WorkerTaskEntity>), property!.PropertyType);
    }

    [Fact]
    public void PostgresModel_WorkerTaskActiveUniqueIndex_IsConfiguredAsExpected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdProvider>(CreateIdProvider());
        services.AddSingleton<IUserContextProvider>(CreateUserContextProvider());
        var serviceProvider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<PostgresDatabaseContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=moai_model_only;Username=postgres;Password=postgres")
            .Options;

        using var context = new PostgresDatabaseContext(options, serviceProvider);
        var entityType = context.Model.FindEntityType(typeof(WorkerTaskEntity));

        Assert.NotNull(entityType);
        var uniqueIndex = Assert.Single(entityType!.GetIndexes(), x => x.GetDatabaseName() == "ux_worker_task_bind_active");
        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal(new[] { nameof(WorkerTaskEntity.BindType), nameof(WorkerTaskEntity.BindId) }, uniqueIndex.Properties.Select(x => x.Name));
        Assert.Equal("is_deleted = 0 AND state IN (1, 2)", uniqueIndex.GetFilter());
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
}
