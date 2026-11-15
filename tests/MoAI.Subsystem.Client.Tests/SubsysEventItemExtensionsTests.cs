using System.Text.Json;
using MoAI.Subsystem.Client.Events;
using Xunit;

namespace MoAI.Subsystem.Client.Tests;

/// <summary>
/// 事件载荷读取扩展：字符串/数字双形态 ID、数组、truncated 与控制事件原因。
/// </summary>
public class SubsysEventItemExtensionsTests
{
    private static SubsysEventItem Item(string eventName, string dataJson)
    {
        return new SubsysEventItem
        {
            Id = "20261115:7",
            Event = eventName,
            Data = JsonDocument.Parse(dataJson).RootElement.Clone(),
        };
    }

    [Fact]
    public void 读取字符串与数字形态主体ID()
    {
        var item = Item(SubsysEventNames.UserUpdated, """{"userId":"1001","teamCount":3,"disabled":true}""");

        Assert.Equal("1001", item.GetDataString("userId"));
        Assert.Equal(1001L, item.GetDataInt64("userId"));
        Assert.Equal(3, item.GetDataInt32("teamCount"));
        Assert.True(item.GetDataBool("disabled"));
        Assert.Null(item.GetDataString("missing"));
    }

    [Fact]
    public void 读取关联ID数组并忽略非法元素()
    {
        var item = Item(SubsysEventNames.TeamMemberAdded, """{"teamId":"12","userIds":["1001","1002",null,1003],"role":"member"}""");

        var ids = item.GetDataStrings("userIds");

        Assert.Equal(["1001", "1002", "1003"], ids);
        Assert.Equal("12", item.GetDataString("teamId"));
        Assert.False(item.IsTruncated());
    }

    [Fact]
    public void 识别截断标记与控制事件原因()
    {
        var truncated = Item(SubsysEventNames.TeamMemberAdded, """{"teamId":"12","userIdsCount":1200,"truncated":true}""");
        Assert.True(truncated.IsTruncated());
        Assert.True(truncated.HasDataProperty("userIdsCount"));
        Assert.False(truncated.HasDataProperty("userIds"));

        var resync = Item(SubsysEventNames.ResyncRequired, """{"reason":"cursor_expired"}""");
        Assert.True(resync.IsResyncRequired());
        Assert.Equal("cursor_expired", resync.GetResyncReason());
    }

    [Fact]
    public void 载荷为空或非对象时返回空值()
    {
        var item = new SubsysEventItem { Id = "1", Event = SubsysEventNames.UserCreated, Data = null };

        Assert.Null(item.GetDataString("userId"));
        Assert.Empty(item.GetDataStrings("roleIds"));
        Assert.False(item.IsTruncated());
    }
}
