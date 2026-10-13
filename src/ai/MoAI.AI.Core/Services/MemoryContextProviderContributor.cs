using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Memory.Models;
using MoAI.Memory.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 长期记忆上下文贡献者：每轮对话按当前用户消息向量检索相关记忆（相关记忆按需激活），
/// 以指令块注入模型上下文；按用户隔离，用户关闭记忆或应用未开启时不注入，子代理不注入.
/// </summary>
[InjectOnScoped]
public sealed class MemoryContextProviderContributor : IAppContextProviderContributor
{
    private const int MaxInjectItems = 8;

    private const int MaxContentLength = 300;

    private readonly IMemoryService _memoryService;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryContextProviderContributor"/> class.
    /// </summary>
    /// <param name="memoryService">记忆领域服务.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public MemoryContextProviderContributor(IMemoryService memoryService, ILoggerFactory loggerFactory)
    {
        _memoryService = memoryService;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public int Order => 30;

    /// <inheritdoc/>
    public async Task<AIContextProvider?> CreateAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        // 子代理保持独立干净上下文（父代理委派时自带任务要素）
        if (context.IsSubAgent)
        {
            return null;
        }

        var resolution = await _memoryService.ResolveAsync(context.AppId, context.UserId, cancellationToken);
        if (resolution is not { IsEnabled: true })
        {
            return null;
        }

        return new MemoryContextProvider(
            _memoryService,
            _loggerFactory.CreateLogger<MemoryContextProvider>(),
            context.AppId,
            context.UserId,
            resolution);
    }

    /// <summary>
    /// 每轮检索并注入相关记忆的上下文提供者.
    /// </summary>
    internal sealed class MemoryContextProvider : AIContextProvider
    {
        private readonly IMemoryService _memoryService;
        private readonly ILogger _logger;
        private readonly Guid _appId;
        private readonly long _userId;
        private readonly MemoryResolution _resolution;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryContextProvider"/> class.
        /// </summary>
        public MemoryContextProvider(IMemoryService memoryService, ILogger logger, Guid appId, long userId, MemoryResolution resolution)
        {
            _memoryService = memoryService;
            _logger = logger;
            _appId = appId;
            _userId = userId;
            _resolution = resolution;
        }

        /// <inheritdoc/>
        protected override async ValueTask<AIContext?> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            var messages = context.AIContext.Messages;
            var lastUser = messages?
                .LastOrDefault(m => m.Role == ChatRole.User && !string.IsNullOrWhiteSpace(m.Text));
            var query = lastUser?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return context.AIContext;
            }

            try
            {
                var items = await _memoryService.SearchAsync(new MemorySearchQuery
                {
                    AppId = _appId,
                    UserId = _userId,
                    QueryText = query,
                    TopK = Math.Clamp(_resolution.Settings.RetrievalTopK, 1, 20),
                    MinScore = _resolution.Settings.MinScore,
                }, cancellationToken);
                if (items.Count == 0)
                {
                    return context.AIContext;
                }

                var builder = new StringBuilder();
                builder.AppendLine("以下是该用户的跨会话长期记忆（自动召回，与当前问题相关），回答时可自然运用，不要逐条复述：");
                foreach (var item in items.Take(MaxInjectItems))
                {
                    var content = item.Content.Length > MaxContentLength ? item.Content[..MaxContentLength] + "…" : item.Content;
                    builder.AppendLine($"- [{TypeName(item.MemoryType)}|{item.UpdateTime:yyyy-MM-dd}] {content}");
                }

                return new AIContext
                {
                    Instructions = builder.ToString(),
                };
            }
            catch (Exception ex)
            {
                // 记忆召回失败不影响对话
                _logger.LogWarning(ex, "长期记忆召回失败. AppId={AppId}, UserId={UserId}", _appId, _userId);
                return context.AIContext;
            }
        }

        private static string TypeName(int memoryType) => memoryType switch
        {
            AgentMemorySettings.TypeEpisodic => "经历",
            AgentMemorySettings.TypeProcedural => "方法",
            _ => "事实",
        };
    }
}
