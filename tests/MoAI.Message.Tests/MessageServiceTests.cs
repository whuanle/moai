using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Exceptions;
using MoAI.Message.Models;
using MoAI.Message.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.Message.Tests;

/// <summary>
/// <see cref="MessageService"/> 发送链路单测：落库行数/收件人隔离/团队与全员解析/事件触发.
/// </summary>
public class MessageServiceTests
{
    [Fact]
    public async Task SendToUsersAsync_ShouldInsertOneRowPerRecipient_AndPushEvent()
    {
        using var db = TestSqliteContext.Create();
        var (service, push) = CreateService(db);

        await service.SendToUsersAsync(Item(), new long[] { 1, 2, 3 });

        var rows = await db.Context.Messages.ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, x => Assert.False(x.IsRead));
        Assert.Equal((int)MessageType.System, rows[0].MessageType);
        Assert.Equal("test", rows[0].Source);

        push.Verify(x => x.PushToUsersAsync(
            It.Is<IReadOnlyCollection<long>>(ids => ids.Count == 3 && ids.Contains(1) && ids.Contains(3)),
            EventNames.MessageNew,
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendToUsersAsync_ShouldDeduplicateRecipients()
    {
        using var db = TestSqliteContext.Create();
        var (service, _) = CreateService(db);

        await service.SendToUsersAsync(Item(), new long[] { 1, 1, 2, 0, -3 });

        var rows = await db.Context.Messages.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new long[] { 1, 2 }, rows.Select(x => x.RecipientUserId).OrderBy(x => x));
    }

    [Fact]
    public async Task SendToUsersAsync_ShouldThrow_WhenRecipientsEmpty()
    {
        using var db = TestSqliteContext.Create();
        var (service, _) = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.SendToUsersAsync(Item(), Array.Empty<long>()));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SendToUsersAsync_ShouldThrow_WhenTitleTooLong()
    {
        using var db = TestSqliteContext.Create();
        var (service, _) = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.SendToUsersAsync(Item(title: new string('a', 300)), new long[] { 1 }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SendToTeamAsync_ShouldThrow404_WhenTeamNotExists()
    {
        using var db = TestSqliteContext.Create();
        var (service, _) = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.SendToTeamAsync(Item(), teamId: 99));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task SendToTeamAsync_ShouldInsertRowsForAllMembers()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Teams.Add(MessageTestSeed.Team(5, "团队"));
        db.Context.TeamUsers.AddRange(MessageTestSeed.TeamUser(5, 1), MessageTestSeed.TeamUser(5, 2));
        await db.Context.SaveChangesAsync();

        var teamService = new Mock<ITeamService>();
        teamService
            .Setup(x => x.GetMemberUserIdsAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new long[] { 1, 2 });
        var push = new Mock<IEventPushService>();
        var service = new MessageService(db.Context, teamService.Object, push.Object);

        await service.SendToTeamAsync(Item(messageType: MessageType.Team, source: "team"), 5);

        var rows = await db.Context.Messages.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal((int)MessageType.Team, x.MessageType));
        push.Verify(x => x.PushToUsersAsync(
            It.Is<IReadOnlyCollection<long>>(ids => ids.Count == 2),
            EventNames.MessageNew,
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendToAllUsersAsync_ShouldSkipDisabledUsers()
    {
        using var db = TestSqliteContext.Create();
        db.Context.Users.AddRange(
            MessageTestSeed.User(1, "a"),
            MessageTestSeed.User(2, "b", isDisable: true),
            MessageTestSeed.User(3, "c"));
        await db.Context.SaveChangesAsync();

        var (service, push) = CreateService(db);

        await service.SendToAllUsersAsync(Item());

        var rows = await db.Context.Messages.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, x => x.RecipientUserId == 2);
        push.Verify(x => x.PushToUsersAsync(
            It.Is<IReadOnlyCollection<long>>(ids => ids.Count == 2),
            EventNames.MessageNew,
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (MessageService Service, Mock<IEventPushService> Push) CreateService(TestSqliteContext db)
    {
        var teamService = new Mock<ITeamService>();
        var push = new Mock<IEventPushService>();
        var service = new MessageService(db.Context, teamService.Object, push.Object);
        return (service, push);
    }

    private static MessageSendItem Item(
        string title = "系统通知",
        string content = "你好",
        MessageType messageType = MessageType.System,
        string source = "test",
        long? senderUserId = null) => new()
        {
            Title = title,
            Content = content,
            MessageType = messageType,
            Source = source,
            SenderUserId = senderUserId,
        };
}
