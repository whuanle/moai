using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Services;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppToolContextProvider 的渐进式工具披露行为.
/// </summary>
public class AppToolContextProviderTests
{
    private static AppTool MakeTool(string name, string? example = "{}", System.Func<string?, CancellationToken, Task<AppToolResult>>? invoke = null)
        => new()
        {
            Name = name,
            Title = name + "-title",
            Description = name + "-desc",
            Kind = "static",
            ParametersExample = example,
            InvokeAsync = invoke ?? ((_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}"))),
        };

    [Fact]
    public async Task BuildList_ListsAllTools()
    {
        var provider = new AppToolContextProvider([MakeTool("echo"), MakeTool("time")]);

        var json = await provider.BuildListJsonAsync(null);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        var names = doc.RootElement.GetProperty("tools").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
        Assert.Contains("echo", names);
        Assert.Contains("time", names);
    }

    [Fact]
    public async Task BuildList_FiltersByQuery()
    {
        var provider = new AppToolContextProvider([MakeTool("echo"), MakeTool("weather")]);

        var json = await provider.BuildListJsonAsync("weather");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("weather", doc.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task BuildList_ResolvesLazyParametersExample()
    {
        var tool = new AppTool
        {
            Name = "mcp_tool",
            Title = "mcp",
            Description = "d",
            Kind = "mcp",
            ParametersExample = null,
            ResolveParametersExampleAsync = _ => Task.FromResult<string?>("{\"a\":1}"),
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        };
        var provider = new AppToolContextProvider([tool]);

        var json = await provider.BuildListJsonAsync(null);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("{\"a\":1}", doc.RootElement.GetProperty("tools")[0].GetProperty("parametersExample").GetString());
    }

    [Fact]
    public async Task Invoke_CallsToolAndReturnsData()
    {
        string? received = null;
        var tool = MakeTool("echo", invoke: (args, _) => { received = args; return Task.FromResult(AppToolResult.Ok("{\"Message\":\"hi\"}")); });
        var provider = new AppToolContextProvider([tool]);

        var result = await provider.InvokeJsonAsync("echo", "{\"Name\":\"MoAI\"}");

        Assert.Equal("{\"Name\":\"MoAI\"}", received);
        Assert.Equal("{\"Message\":\"hi\"}", result);
    }

    [Fact]
    public async Task Invoke_UnknownTool_ReturnsErrorJson()
    {
        var provider = new AppToolContextProvider([MakeTool("echo")]);

        var result = await provider.InvokeJsonAsync("missing", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("missing", doc.RootElement.GetProperty("error").GetString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_ToolFailure_ReturnsErrorJson()
    {
        var tool = MakeTool("bad", invoke: (_, _) => Task.FromResult(AppToolResult.Fail("boom")));
        var provider = new AppToolContextProvider([tool]);

        var result = await provider.InvokeJsonAsync("bad", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("boom", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invoke_ApprovalMode_ToolWithoutSourceId_WaitsForDecision()
    {
        // 无来源插件且沙箱未开自动放行：进入人工等待（Redis 桩未配置，等待创建记录失败按超时返回，
        // 工具本体不执行；真实等待/批准路径由 E2E 覆盖）
        var gate = new AppToolApprovalGate
        {
            Mode = MoAI.AI.AppToolApprovalContract.ModeApproval,
            AppId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = 1,
        };
        var service = new AppToolApprovalService(new Moq.Mock<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>(Moq.MockBehavior.Strict).Object);
        var provider = new AppToolContextProvider([MakeTool("weather")], service, gate);

        var result = await provider.InvokeJsonAsync("weather", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("超时", doc.RootElement.GetProperty("error").GetString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_ApprovalMode_PluginInWhitelist_ExecutesWithoutWaiting()
    {
        var pluginId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tool = new AppTool
        {
            Name = "weather",
            Title = "天气",
            Description = "d",
            Kind = "static",
            SourceId = pluginId,
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}")),
        };
        var gate = new AppToolApprovalGate
        {
            Mode = MoAI.AI.AppToolApprovalContract.ModeApproval,
            AppId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = 1,
            Policy = new MoAI.AI.AppToolApprovalPolicy { AutoApprovePlugins = [pluginId] },
        };
        // 严格桩：白名单工具直接执行，任何 Redis 审批写入都应失败用例
        var service = new AppToolApprovalService(new Moq.Mock<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>(Moq.MockBehavior.Strict).Object);
        var provider = new AppToolContextProvider([tool], service, gate);

        var result = await provider.InvokeJsonAsync("weather", "{}");

        Assert.Equal("{\"ok\":true}", result);
    }

    [Fact]
    public async Task Invoke_ApprovalMode_SandboxAutoApproved_ExecutesWithoutWaiting()
    {
        var tool = new AppTool
        {
            Name = "sandbox_run_code",
            Title = "运行代码",
            Description = "d",
            Kind = "sandbox",
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}")),
        };
        var gate = new AppToolApprovalGate
        {
            Mode = MoAI.AI.AppToolApprovalContract.ModeApproval,
            AppId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = 1,
            Policy = new MoAI.AI.AppToolApprovalPolicy { SandboxAutoApproved = true },
        };
        var service = new AppToolApprovalService(new Moq.Mock<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>(Moq.MockBehavior.Strict).Object);
        var provider = new AppToolContextProvider([tool], service, gate);

        var result = await provider.InvokeJsonAsync("sandbox_run_code", "{}");

        Assert.Equal("{\"ok\":true}", result);
    }

    [Fact]
    public async Task Invoke_ApprovalMode_PluginNotInWhitelist_WaitsForDecision()
    {
        var tool = new AppTool
        {
            Name = "weather",
            Title = "天气",
            Description = "d",
            Kind = "static",
            SourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}")),
        };
        var gate = new AppToolApprovalGate
        {
            Mode = MoAI.AI.AppToolApprovalContract.ModeApproval,
            AppId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            UserId = 1,
            Policy = new MoAI.AI.AppToolApprovalPolicy
            {
                AutoApprovePlugins = [Guid.Parse("11111111-1111-1111-1111-111111111111")],
                SandboxAutoApproved = true,
            },
        };
        var service = new AppToolApprovalService(new Moq.Mock<StackExchange.Redis.Extensions.Core.Abstractions.IRedisDatabase>(Moq.MockBehavior.Strict).Object);
        var provider = new AppToolContextProvider([tool], service, gate);

        // 非白名单插件：进入人工等待（桩未配置按超时返回），工具本体不执行
        var result = await provider.InvokeJsonAsync("weather", "{}");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("超时", doc.RootElement.GetProperty("error").GetString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetaCallTool_AcceptsObjectFormArguments()
    {
        // 部分模型（如 DeepSeek）调用 call_tool 时把 argumentsJson 传成 JSON 对象而非字符串，
        // string 形参会在参数编组时抛 JsonException 中断整轮对话；元工具需兼容两种形态.
        string? receivedArgs = null;
        var provider = new AppToolContextProvider([MakeTool("echo", invoke: (args, _) =>
        {
            receivedArgs = args;
            return Task.FromResult(AppToolResult.Ok("{\"ok\":true}"));
        })]);

        var aiContext = await provider.BuildAIContextAsync();
        var callTool = aiContext.Tools.OfType<Microsoft.Extensions.AI.AIFunction>().First(f => f.Name == "call_tool");

        // 对象形态
        var objectForm = await callTool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
        {
            ["toolName"] = "echo",
            ["argumentsJson"] = JsonSerializer.SerializeToElement(new { code = "print(1)" }),
        }));
        Assert.Contains("\"ok\":true", objectForm?.ToString());
        Assert.NotNull(receivedArgs);
        using (var doc = JsonDocument.Parse(receivedArgs!))
        {
            Assert.Equal("print(1)", doc.RootElement.GetProperty("code").GetString());
        }

        // 字符串形态（按 schema 传 JSON 文本的模型）同样可用
        receivedArgs = null;
        await callTool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>
        {
            ["toolName"] = "echo",
            ["argumentsJson"] = JsonSerializer.Serialize(new { code = "print(2)" }),
        }));
        Assert.NotNull(receivedArgs);
        using (var doc2 = JsonDocument.Parse(receivedArgs!))
        {
            Assert.Equal("print(2)", doc2.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Instructions_ContainsUsageNotesSection()
    {
        var provider = new AppToolContextProvider([new AppTool
        {
            Name = "db",
            Title = "db-title",
            Description = "d",
            Kind = "static",
            UsageNotes = "orders 表存订单主单",
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        }]);

        var aiContext = await provider.BuildAIContextAsync();

        Assert.Contains("插件使用须知", aiContext.Instructions, System.StringComparison.Ordinal);
        Assert.Contains("orders 表存订单主单", aiContext.Instructions, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Instructions_UsageNotes_DeduplicatedAcrossToolsOfSamePlugin()
    {
        AppTool Make(string name) => new()
        {
            Name = name,
            Title = "plugin-title",
            Description = "d",
            Kind = "dynamic",
            UsageNotes = "同一段须知",
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        };
        var provider = new AppToolContextProvider([Make("p__a"), Make("p__b")]);

        var aiContext = await provider.BuildAIContextAsync();

        // 同一插件的多工具共享一份须知文本，注入时只出现一次
        var count = aiContext.Instructions!.Split("同一段须知").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SemanticNotes_RegisteredAsPluginNoteTool_AndFetchContent()
    {
        var noteId = System.Guid.CreateVersion7();
        var tool = new AppTool
        {
            Name = "db",
            Title = "db",
            Description = "d",
            Kind = "static",
            SemanticNotes = [new AppPluginSemanticNote(noteId, "orders 表", "订单主单表结构…")],
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        };
        var provider = new AppToolContextProvider([tool]);

        var aiContext = await provider.BuildAIContextAsync();

        // 目录注入 instructions + plugin_note 元工具已注册
        Assert.Contains("语义资料目录", aiContext.Instructions, System.StringComparison.Ordinal);
        Assert.Contains(noteId.ToString("N"), aiContext.Instructions, System.StringComparison.Ordinal);
        var pluginNote = aiContext.Tools.OfType<Microsoft.Extensions.AI.AIFunction>().FirstOrDefault(f => f.Name == "plugin_note");
        Assert.NotNull(pluginNote);

        var result = await provider.InvokePluginNoteAsync(noteId.ToString());
        using var doc = JsonDocument.Parse(result);
        Assert.Equal("orders 表", doc.RootElement.GetProperty("title").GetString());
        Assert.Contains("订单主单表结构", doc.RootElement.GetProperty("content").GetString(), System.StringComparison.Ordinal);

        var missing = await provider.InvokePluginNoteAsync(System.Guid.CreateVersion7().ToString());
        using var missingDoc = JsonDocument.Parse(missing);
        Assert.False(missingDoc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void NoSemanticNotes_PluginNoteToolNotRegistered()
    {
        var provider = new AppToolContextProvider([MakeTool("echo")]);

        var aiContext = provider.BuildAIContextAsync().Result;

        Assert.DoesNotContain("语义资料目录", aiContext.Instructions, System.StringComparison.Ordinal);
        Assert.DoesNotContain(aiContext.Tools.OfType<Microsoft.Extensions.AI.AIFunction>(), f => f.Name == "plugin_note");
    }

    [Fact]
    public async Task CallPolicy_DenyPatternMatch_BlocksInvocation()
    {
        var invoked = false;
        var tool = MakeTool("sql", invoke: (_, _) => { invoked = true; return Task.FromResult(AppToolResult.Ok("{}")); });
        var withPolicy = new AppTool
        {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            Kind = tool.Kind,
            CallPolicy = "{\"denyPatterns\":[\"delete\\\\s+from\"],\"action\":\"block\"}",
            InvokeAsync = tool.InvokeAsync,
        };
        var provider = new AppToolContextProvider([withPolicy]);

        var result = await provider.InvokeJsonAsync("sql", "{\"sql\":\"DELETE FROM orders\"}");

        Assert.False(invoked);
        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("插件调用策略", doc.RootElement.GetProperty("error").GetString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallPolicy_NoMatch_PassesThrough()
    {
        var tool = new AppTool
        {
            Name = "sql",
            Title = "sql",
            Description = "d",
            Kind = "static",
            CallPolicy = "{\"denyPatterns\":[\"delete\\\\s+from\"],\"action\":\"block\"}",
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{\"ok\":true}")),
        };
        var provider = new AppToolContextProvider([tool]);

        var result = await provider.InvokeJsonAsync("sql", "{\"sql\":\"SELECT 1\"}");

        Assert.Contains("\"ok\":true", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtraNotes_AppendedIntoToolDescription()
    {
        var tool = new AppTool
        {
            Name = "sql",
            Title = "sql",
            Description = "原始描述",
            Kind = "static",
            ExtraNotes = "只允许查询 orders 表",
            InvokeAsync = (_, _) => Task.FromResult(AppToolResult.Ok("{}")),
        };
        var provider = new AppToolContextProvider([tool]);

        var json = await provider.BuildListJsonAsync(null);

        // 序列化会把非 ASCII 转义，解析后按字段断言（与 E2E 内容断言同一口径）
        using var doc = JsonDocument.Parse(json);
        var description = doc.RootElement.GetProperty("tools")[0].GetProperty("description").GetString();
        Assert.Contains("原始描述", description, System.StringComparison.Ordinal);
        Assert.Contains("只允许查询 orders 表", description, System.StringComparison.Ordinal);
    }
}
