using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MoAI.AI.Services;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppSecurityAgentMiddleware（MAF AIAgentBuilder.Use 官方拦截点）：
/// 工具结果在源头脱敏（模型第二轮请求可见已脱敏）、工具执行参数不被污染、模型正文/参数在输出层脱敏.
/// </summary>
public class AppSecurityAgentMiddlewareTests
{
    private const string RawPhone = "13812345678";

    private static AppSecurityPolicy CreatePolicy(bool toolResult = true, bool toolArgs = true, bool modelOutput = true) =>
        AppSecurityPolicy.Parse(new AppSecurityConfigEntity
        {
            Enabled = true,
            MaskToolResult = toolResult,
            MaskToolArgs = toolArgs,
            MaskModelOutput = modelOutput,
            Rules = """[{"name":"手机号","type":"phone"}]""",
            ModelOutputRules = """[{"name":"手机号","type":"phone"}]""",
        });

    /// <summary>
    /// 脚本化对话客户端：第 1 轮返回工具调用，第 2 轮返回带敏感信息的正文；记录每轮请求消息用于断言模型可见内容.
    /// </summary>
    private sealed class ScriptedChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            if (Requests.Count == 1)
            {
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call1", "query_order", new Dictionary<string, object?> { ["phone"] = RawPhone })]))
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                });
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"下单人手机号 {RawPhone}")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            if (Requests.Count == 1)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call1", "query_order", new Dictionary<string, object?> { ["phone"] = RawPhone })])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
            }
            else
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, $"下单人手机号 {RawPhone}");
            }

            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey) => null;

        public void Dispose()
        {
        }
    }

    /// <summary>FunctionResultContent.Result 在第二轮请求中为 JsonElement（字符串结果被 FICC 序列化），统一取文本断言.</summary>
    private static string ExtractResultText(FunctionResultContent content) => content.Result switch
    {
        string text => text,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je => je.GetString() ?? string.Empty,
        System.Text.Json.JsonElement je => je.GetRawText(),
        _ => content.ToString(),
    };

    private static (ScriptedChatClient Client, AIFunction Tool, List<string> InvokedArgs) BuildTools()
    {
        var client = new ScriptedChatClient();
        var invokedArgs = new List<string>();
        var tool = AIFunctionFactory.Create((string phone) =>
        {
            invokedArgs.Add(phone);
            return $"{{\"order\":\"A1\",\"phone\":\"{phone}\",\"name\":\"张三\"}}";
        }, name: "query_order");
        return (client, tool, invokedArgs);
    }

    [Fact]
    public async Task RunAsync_MasksToolResultBeforeModelSeesItAndKeepsExecutionArgsRaw()
    {
        var (client, tool, invokedArgs) = BuildTools();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Tools = [tool] },
        });
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreatePolicy());

        var response = await wrapped.RunAsync($"查询订单，手机号 {RawPhone}");

        // 两轮模型调用：第 1 轮工具调用 → 执行 → 第 2 轮携带工具结果
        Assert.Equal(2, client.Requests.Count);

        // 工具拿到的是原始参数（脱敏不得污染执行）
        Assert.Equal([RawPhone], invokedArgs);

        // 第 2 轮请求中的工具结果已被脱敏（模型不可见原文）
        var secondRound = client.Requests[1];
        var resultContent = secondRound.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        var resultText = ExtractResultText(resultContent);
        Assert.Contains("***", resultText);
        Assert.DoesNotContain(RawPhone, resultText);

        // 模型回复正文（run 级中间件改写）
        Assert.Contains("***", response.Text);
        Assert.DoesNotContain(RawPhone, response.Text);
    }

    [Fact]
    public async Task RunStreamingAsync_ClonesUpdatesForMaskingWithoutTouchingExecution()
    {
        var (client, tool, invokedArgs) = BuildTools();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Tools = [tool] },
        });
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreatePolicy());

        var text = string.Empty;
        await foreach (var update in wrapped.RunStreamingAsync($"查询订单，手机号 {RawPhone}"))
        {
            text += update.Text;
        }

        Assert.Equal([RawPhone], invokedArgs);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains("***", text);
        Assert.DoesNotContain(RawPhone, text);

        // 流式路径下模型第 2 轮请求同样只见脱敏结果
        var resultContent = client.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.DoesNotContain(RawPhone, ExtractResultText(resultContent));
    }

    [Fact]
    public async Task RunAsync_ToolResultScopeOff_DoesNotMaskResultButStillMasksModelText()
    {
        var (client, tool, _) = BuildTools();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Tools = [tool] },
        });
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreatePolicy(toolResult: false));

        var response = await wrapped.RunAsync("查询订单");

        var resultContent = client.Requests[1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        Assert.Contains(RawPhone, ExtractResultText(resultContent));
        Assert.DoesNotContain(RawPhone, response.Text);
    }

    [Fact]
    public void Wrap_DisabledPolicy_ReturnsSameAgentInstance()
    {
        var (client, tool, _) = BuildTools();
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { Tools = [tool] },
        });
        Assert.Same(agent, AppSecurityAgentMiddleware.Wrap(agent, AppSecurityPolicy.Disabled));
    }
}

/// <summary>
/// 二期扩展：模型回复 block 的流式缓冲窗拦截与非流式整条替换.
/// </summary>
public class AppSecurityAgentMiddlewarePhase2Tests
{
    /// <summary>可控分片的纯文本对话客户端.</summary>
    private sealed class ScriptedTextChatClient : IChatClient
    {
        private readonly string[] _chunks;

        public ScriptedTextChatClient(params string[] chunks)
        {
            _chunks = chunks;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(_chunks))));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in _chunks)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey) => null;

        public void Dispose()
        {
        }
    }

    private static AppSecurityPolicy CreateModelBlockPolicy() =>
        AppSecurityPolicy.Parse(new AppSecurityConfigEntity
        {
            Enabled = true,
            MaskToolResult = true,
            MaskToolArgs = true,
            MaskModelOutput = true,
            Rules = "[]",
            ModelOutputRules = """[{"name":"敏感词","type":"custom","pattern":"机密","strategy":"block"}]""",
        });

    [Fact]
    public async Task RunStreamingAsync_ModelBlock_TruncatesAndEmitsNotice()
    {
        // 敏感词出现在首屏（>缓冲阈值）之后：释放首段后尾段命中被截断
        var client = new ScriptedTextChatClient(new string('甲', 150) + "机密" + new string('乙', 60));
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions());
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreateModelBlockPolicy());

        var text = string.Empty;
        await foreach (var update in wrapped.RunStreamingAsync("讲个故事"))
        {
            text += update.Text;
        }

        // MAF ChatClientAgent 可能把相邻文本 chunk 合并为单个 update：整段进缓冲、检测命中后整体截断
        Assert.DoesNotContain("机密", text);
        Assert.Equal(AppSecurityAgentMiddleware.BlockedModelOutputNotice, text);
    }

    [Fact]
    public async Task RunStreamingAsync_ModelBlock_MultiUpdateTextStillCaught()
    {
        // 敏感词分片在多个 update 中（中间件按累积缓冲整段检测，跨 update 边界同样命中）
        var client = new ScriptedTextChatClient(new string('甲', 130) + "机", "密" + new string('乙', 40));
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions());
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreateModelBlockPolicy());

        var text = string.Empty;
        await foreach (var update in wrapped.RunStreamingAsync("讲个故事"))
        {
            text += update.Text;
        }

        Assert.DoesNotContain("机密", text);
        Assert.Contains(AppSecurityAgentMiddleware.BlockedModelOutputNotice, text);
    }

    [Fact]
    public async Task RunStreamingAsync_ModelBlockCleanText_FlushesFully()
    {
        // 无命中：缓冲全量释放，不追加拦截提示
        var client = new ScriptedTextChatClient(new string('甲', 200), "结尾");
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions());
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, CreateModelBlockPolicy());

        var text = string.Empty;
        await foreach (var update in wrapped.RunStreamingAsync("讲个故事"))
        {
            text += update.Text;
        }

        Assert.Equal(new string('甲', 200) + "结尾", text);
        Assert.DoesNotContain(AppSecurityAgentMiddleware.BlockedModelOutputNotice, text);
    }

    [Fact]
    public async Task RunAsync_ModelBlock_ReplacesWholeResponse()
    {
        var client = new ScriptedTextChatClient("手机号 13812345678，机密资料");
        var agent = new ChatClientAgent(client, new ChatClientAgentOptions());
        var wrapped = AppSecurityAgentMiddleware.Wrap(agent, AppSecurityPolicy.Parse(new AppSecurityConfigEntity
        {
            Enabled = true,
            MaskModelOutput = true,
            Rules = "[]",
            ModelOutputRules = """[{"name":"敏感词","type":"custom","pattern":"机密","strategy":"block"},{"name":"手机","type":"phone","strategy":"mask"}]""",
        }));

        var response = await wrapped.RunAsync("讲个故事");

        Assert.Equal(AppSecurityPolicy.BlockPlaceholder, response.Text);
    }
}
