using System;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 调试会话注册表的键格式.
/// </summary>
public class DebugSessionRegistryTests
{
    [Fact]
    public void SessionKey_UsesNamespacedNFormat()
    {
        var id = Guid.Parse("0198f2c1-1111-7000-8000-000000000000");

        Assert.Equal("appagent:debug:0198f2c1111170008000000000000000", RedisDebugSessionRegistry.SessionKey(id));
    }
}
