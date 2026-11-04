using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable MAAI001 // 压缩策略库为实验性 API（与 MoAI.AI.Core 的 NoWarn 一致）
using MoAI.AI.Models;
using MoAI.AI.Services;
using MoAI.AIChannel.Services;
using MoAI.Database.Entities;
using Moq;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 应用上下文压缩策略工厂：auto 模式按模型窗口比例推导阈值、custom 模式用绝对值、
/// 模型窗口元数据缺失回落 legacy 绝对值；滑动窗口策略已移除.
/// </summary>
public class AppCompactionStrategyFactoryTests
{
    private const int OneMillion = 1_000_000;

    [Fact]
    public async Task BuildAsync_AutoModeLargeWindow_ToolResultFoldFiresAtRatio()
    {
        // 窗口 1M/输出 100k → budget 900k → 工具折叠触发 450k token；旧工具结果 2.4M 字符 ≈ 600k token 触发折叠
        var factory = CreateFactory(window: OneMillion, maxOutput: 100_000);
        var config = CreateConfig(null);
        var bigResult = new string('x', 2_400_000);
        var messages = BuildToolConversation(bigResultGroups: 1, smallGroups: 11);

        var compacted = await CompactAsync(factory, config, messages);

        // 旧工具组被折叠（调用+结果换成摘要消息），原始大文本不再出现在任何工具结果里
        Assert.True(compacted.Count < messages.Count, $"expected tool fold, got {messages.Count}->{compacted.Count}");
        Assert.DoesNotContain(
            compacted.SelectMany(m => m.Contents).OfType<FunctionResultContent>(),
            c => c.Result?.ToString()?.Length > 1_000_000);
    }

    [Fact]
    public async Task BuildAsync_AutoModeSmallWindow_TruncationSafetyNetFires()
    {
        // 窗口 10k/输出 2k → budget 8k → 紧急截断触发 7.6k token；30 轮 × 1500 字符 ≈ 11.25k token 触发截断
        var factory = CreateFactory(window: 10_000, maxOutput: 2_000);
        var config = CreateConfig(null);
        var messages = BuildPlainConversation(turns: 30, charsPerTurn: 1_500);

        var compacted = await CompactAsync(factory, config, messages);

        // 超出预算的最老轮次被截掉（最近若干组由库内 MinimumPreservedGroups 保护）
        Assert.True(compacted.Count < messages.Count);
        Assert.True(TotalLength(compacted) < TotalLength(messages));
    }

    [Fact]
    public async Task BuildAsync_CustomMode_UsesStoredAbsoluteValues_NotWindowRatios()
    {
        // custom 模式：模型窗口 1M 也不按比例，hardTokenLimit=200 即刻触发截断；
        // 40 轮（81 条消息）超过库内默认保护窗（最近 32 个消息组），最老的组被截掉
        var factory = CreateFactory(window: OneMillion, maxOutput: 100_000);
        var config = CreateConfig("""{"compactionMode":"custom","hardTokenLimit":200}""");
        var messages = BuildPlainConversation(turns: 40, charsPerTurn: 600);

        var compacted = await CompactAsync(factory, config, messages);

        Assert.True(compacted.Count < messages.Count, $"expected truncation, got {messages.Count}->{compacted.Count}");
    }

    [Fact]
    public async Task BuildAsync_AutoModeModelWindowMissing_FallsBackToLegacyAbsoluteValues()
    {
        // auto + 模型窗口元数据缺失（ContextWindow=0）→ 回落 settings 绝对值：hardTokenLimit=200 触发截断
        var factory = CreateFactory(window: 0, maxOutput: 0);
        var config = CreateConfig("""{"hardTokenLimit":200}""");
        var messages = BuildPlainConversation(turns: 40, charsPerTurn: 600);

        var compacted = await CompactAsync(factory, config, messages);

        Assert.True(compacted.Count < messages.Count, $"expected truncation, got {messages.Count}->{compacted.Count}");
    }

    [Fact]
    public async Task BuildAsync_AutoModeLegacyDefaults_SmallContextDoesNotCompact()
    {
        // auto + 窗口缺失 + 未显式配置 → legacy 兜底 hardTokenLimit=64000，小上下文不压缩
        var factory = CreateFactory(window: 0, maxOutput: 0);
        var config = CreateConfig(null);
        var messages = BuildPlainConversation(turns: 10, charsPerTurn: 600);

        var compacted = await CompactAsync(factory, config, messages);

        Assert.Equal(messages.Count, compacted.Count);
    }

    [Fact]
    public async Task BuildAsync_SlidingWindowRemoved_PreserveTurnsLegacyFieldIgnored()
    {
        // 旧 preserveTurns 字段不再产生滑动窗口：轮数远超 8 但 token 远低于阈值 → 不压缩
        var factory = CreateFactory(window: 0, maxOutput: 0);
        var config = CreateConfig("""{"preserveTurns":2}""");
        var messages = BuildPlainConversation(turns: 20, charsPerTurn: 100);

        var compacted = await CompactAsync(factory, config, messages);

        Assert.Equal(messages.Count, compacted.Count);
    }

    private static AppCompactionStrategyFactory CreateFactory(int window, int maxOutput)
    {
        var resolver = new Mock<IAiModelResolver>(MockBehavior.Strict);
        resolver
            .Setup(x => x.ResolveByIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid modelId, int _, CancellationToken _) =>
                modelId == Guid.Empty ? null : (new AiModelEntity { Id = modelId, ContextWindow = window, MaxOutput = maxOutput }, new AiChannelEntity()));
        var chatClientProvider = new Mock<IChatClientProvider>(MockBehavior.Loose);
        return new AppCompactionStrategyFactory(resolver.Object, chatClientProvider.Object);
    }

    private static AppAgentConfigEntity CreateConfig(string? executionSettings) => new()
    {
        TeamId = 1,
        ModelId = Guid.CreateVersion7(),
        ExecutionSettings = executionSettings ?? string.Empty,
    };

    /// <summary>构造系统 + N 组「用户提问 → 助手工具调用 → 工具结果」对话.</summary>
    private static List<ChatMessage> BuildToolConversation(int bigResultGroups, int smallGroups)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "You are a helpful assistant.") };
        for (var i = 0; i < bigResultGroups + smallGroups; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"question {i}"));
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"call{i}", "search", new Dictionary<string, object?> { ["q"] = $"term{i}" })]));
            var result = i < bigResultGroups ? new string('x', 2_400_000) : $"result {i}";
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"call{i}", result)]));
        }

        return messages;
    }

    /// <summary>构造 N 轮「用户提问 → 助手回答」普通对话.</summary>
    private static List<ChatMessage> BuildPlainConversation(int turns, int charsPerTurn)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, "You are a helpful assistant.") };
        for (var i = 0; i < turns; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"question {i}: " + new string('q', charsPerTurn)));
            messages.Add(new ChatMessage(ChatRole.Assistant, $"answer {i}: " + new string('a', charsPerTurn)));
        }

        return messages;
    }

    private static async Task<IReadOnlyList<ChatMessage>> CompactAsync(AppCompactionStrategyFactory factory, AppAgentConfigEntity config, List<ChatMessage> messages)
    {
        var strategy = await factory.BuildAsync(config, CancellationToken.None);
        return (await CompactionProvider.CompactAsync(strategy, messages, NullLogger.Instance, CancellationToken.None)).ToList();
    }

    private static int TotalLength(IEnumerable<ChatMessage> messages) =>
        messages.Sum(m => m.Text?.Length ?? 0);
}
