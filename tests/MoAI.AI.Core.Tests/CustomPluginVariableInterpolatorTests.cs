using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Variable.Services;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// <see cref="CustomPluginVariableInterpolator"/> 测试.
/// </summary>
public class CustomPluginVariableInterpolatorTests
{
    private static PluginCustomEntity CreateCustom(string headersJson, string queriesJson) => new()
    {
        Id = System.Guid.NewGuid(),
        Server = "https://example.com",
        Headers = headersJson,
        Queries = queriesJson,
        Type = (int)PluginType.MCP,
        OpenapiFileId = 0,
        OpenapiFileName = string.Empty,
    };

    private static IVariableService MockService(Func<long, IReadOnlyCollection<KeyValueString>, IReadOnlyList<KeyValueString>> substitute)
    {
        var mock = new Mock<IVariableService>();
        mock
            .Setup(x => x.SubstituteAsync(It.IsAny<long>(), It.IsAny<IReadOnlyCollection<KeyValueString>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long teamId, IReadOnlyCollection<KeyValueString> values, CancellationToken _) => substitute(teamId, values));
        return mock.Object;
    }

    [Fact]
    public async Task InterpolateAsync_SystemPluginReturnsOriginalInstance()
    {
        var custom = CreateCustom("[]", "[]");
        var service = MockService((_, values) => throw new System.InvalidOperationException("系统插件不应插值"));
        var interpolator = new CustomPluginVariableInterpolator(service);

        var result = await interpolator.InterpolateAsync(custom, 0);

        Assert.Same(custom, result);
    }

    [Fact]
    public async Task InterpolateAsync_SubstitutesHeaderAndQueryValues()
    {
        var custom = CreateCustom(
            new[]
            {
                new KeyValueString { Key = "Authorization", Value = "Bearer {token}" },
            }.ToJsonString(),
            new[]
            {
                new KeyValueString { Key = "tenant", Value = "{tenantId}" },
            }.ToJsonString());

        var service = MockService((_, values) => values
            .Select(kv => new KeyValueString
            {
                Key = kv.Key,
                Value = kv.Value.Replace("{token}", "secret").Replace("{tenantId}", "t-1"),
            })
            .ToList());
        var interpolator = new CustomPluginVariableInterpolator(service);

        var result = await interpolator.InterpolateAsync(custom, 42);

        Assert.NotSame(custom, result);

        var headers = result.Headers.JsonToObject<List<KeyValueString>>()!;
        var queries = result.Queries.JsonToObject<List<KeyValueString>>()!;
        Assert.Single(headers);
        Assert.Single(queries);
        Assert.Equal("Bearer secret", headers[0].Value);
        Assert.Equal("t-1", queries[0].Value);

        // 数据库跟踪实体不被修改
        Assert.Contains("{token}", custom.Headers);
    }

    [Fact]
    public async Task InterpolateAsync_KeepsControlHeader()
    {
        var custom = CreateCustom(
            new[]
            {
                new KeyValueString { Key = ".HttpTransportMode", Value = "\"StreamableHttp\"" },
                new KeyValueString { Key = "Authorization", Value = "Bearer {token}" },
            }.ToJsonString(),
            "[]");

        var service = MockService((_, values) => values
            .Select(kv => new KeyValueString
            {
                Key = kv.Key,
                Value = kv.Value.Contains("{token}") ? kv.Value.Replace("{token}", "secret") : kv.Value,
            })
            .ToList());
        var interpolator = new CustomPluginVariableInterpolator(service);

        var result = await interpolator.InterpolateAsync(custom, 42);

        var headers = result.Headers.JsonToObject<List<KeyValueString>>()!;
        Assert.Equal(2, headers.Count);
        Assert.Equal("\"StreamableHttp\"", headers[0].Value);
        Assert.Equal("Bearer secret", headers[1].Value);
    }

    [Fact]
    public async Task InterpolateAsync_NoTeamVariablesStillReturnsCopy()
    {
        var custom = CreateCustom(
            new[] { new KeyValueString { Key = "Authorization", Value = "Bearer {token}" } }.ToJsonString(),
            "[]");

        var service = MockService((_, values) => values.ToList());
        var interpolator = new CustomPluginVariableInterpolator(service);

        var result = await interpolator.InterpolateAsync(custom, 42);

        Assert.Contains("{token}", result.Headers);
    }
}
