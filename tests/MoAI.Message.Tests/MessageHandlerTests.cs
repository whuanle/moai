using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Infra.Models;
using MoAI.Message.Commands;
using MoAI.Message.Handlers;
using MoAI.Message.Queries;
using Moq;
using Xunit;

namespace MoAI.Message.Tests;

/// <summary>
/// 站内信读侧 Handler 单测：未读计数/列表隔离与过滤/已读/全部已读/软删.
/// </summary>
public class MessageHandlerTests
{
    [Fact]
    public async Task UnreadCount_ShouldCountOnlyMyUnread()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Messages.AddRange(
            MessageTestSeed.Message(1, isRead: false),
            MessageTestSeed.Message(1, isRead: true),
            MessageTestSeed.Message(2, isRead: false));
        await db.Context.SaveChangesAsync();

        var handler = new QueryMessageUnreadCountCommandHandler(db.Context);
        var result = await handler.Handle(new QueryMessageUnreadCountCommand { ContextUserId = 1 }, CancellationToken.None);

        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task QueryList_ShouldReturnOnlyMyMessages_WithFilterAndPaging()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Messages.AddRange(
            MessageTestSeed.Message(1, title: "a", isRead: false),
            MessageTestSeed.Message(1, title: "b", isRead: true),
            MessageTestSeed.Message(2, title: "c", isRead: false));
        await db.Context.SaveChangesAsync();

        var handler = CreateListHandler(db);
        var result = await handler.Handle(new QueryMessageListCommand
        {
            ContextUserId = 1,
            IsRead = false,
            PageNo = 1,
            PageSize = 10,
        }, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Equal("a", result.Items.Single().Title);
        Assert.False(result.Items.Single().IsRead);
    }

    [Fact]
    public async Task ReadMessage_ShouldMarkMyUnreadOnly()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Messages.AddRange(
            MessageTestSeed.Message(1, isRead: false),
            MessageTestSeed.Message(2, isRead: false));
        await db.Context.SaveChangesAsync();
        var myId = db.Context.Messages.AsNoTracking().First(x => x.RecipientUserId == 1).Id;

        var handler = new ReadMessageCommandHandler(db.Context);
        await handler.Handle(new ReadMessageCommand { ContextUserId = 1, MessageIds = new long[] { myId } }, CancellationToken.None);

        var rows = db.Context.Messages.AsNoTracking().IgnoreQueryFilters().ToList();
        Assert.True(rows.First(x => x.Id == myId).IsRead);
        Assert.False(rows.First(x => x.RecipientUserId == 2).IsRead);
    }

    [Fact]
    public async Task ReadAllMessage_ShouldMarkAllMyUnread()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Messages.AddRange(
            MessageTestSeed.Message(1, isRead: false),
            MessageTestSeed.Message(1, isRead: false),
            MessageTestSeed.Message(2, isRead: false));
        await db.Context.SaveChangesAsync();

        var handler = new ReadAllMessageCommandHandler(db.Context);
        await handler.Handle(new ReadAllMessageCommand { ContextUserId = 1 }, CancellationToken.None);

        var rows = db.Context.Messages.AsNoTracking().ToList();
        Assert.All(rows.Where(x => x.RecipientUserId == 1), x => Assert.True(x.IsRead));
        Assert.False(rows.First(x => x.RecipientUserId == 2).IsRead);
    }

    [Fact]
    public async Task DeleteMessage_ShouldSoftDelete_AndHideFromListAndCount()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Messages.AddRange(
            MessageTestSeed.Message(1, isRead: false),
            MessageTestSeed.Message(2, isRead: false));
        await db.Context.SaveChangesAsync();
        var myId = db.Context.Messages.AsNoTracking().First(x => x.RecipientUserId == 1).Id;

        var deleteHandler = new DeleteMessageCommandHandler(db.Context);
        await deleteHandler.Handle(new DeleteMessageCommand { ContextUserId = 1, MessageIds = new long[] { myId } }, CancellationToken.None);

        // 软删行 is_deleted 已写非零（默认过滤器下查询不可见）
        var raw = db.Context.Messages.AsNoTracking().IgnoreQueryFilters().ToList();
        Assert.NotEqual(0L, raw.First(x => x.Id == myId).IsDeleted);

        var listHandler = CreateListHandler(db);
        var list = await listHandler.Handle(new QueryMessageListCommand { ContextUserId = 1 }, CancellationToken.None);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.Total);

        var countHandler = new QueryMessageUnreadCountCommandHandler(db.Context);
        var count = await countHandler.Handle(new QueryMessageUnreadCountCommand { ContextUserId = 1 }, CancellationToken.None);
        Assert.Equal(0, count.Value);
    }

    [Fact]
    public async Task QueryList_ShouldThrow_WhenPageSizeOutOfRange()
    {
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(async () =>
        {
            var validate = new FluentValidation.InlineValidator<QueryMessageListCommand>();
            QueryMessageListCommand.Validate(validate);
            var result = await validate.ValidateAsync(new QueryMessageListCommand { PageSize = 500 });
            if (!result.IsValid)
            {
                throw new FluentValidation.ValidationException(result.Errors);
            }
        });
    }

    private static QueryMessageListCommandHandler CreateListHandler(TestSqliteContext db)
    {
        var fill = new Mock<IUserInfoFillService>();
        fill.Setup(x => x.FillAsync(It.IsAny<IEnumerable<AuditsInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new QueryMessageListCommandHandler(db.Context, fill.Object);
    }
}
