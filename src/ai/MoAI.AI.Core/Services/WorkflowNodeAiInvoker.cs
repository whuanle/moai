using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Services;

namespace MoAI.AI.Services;

/// <summary>
/// <inheritdoc cref="IWorkflowNodeAiInvoker"/>
/// 工作流 AI 节点的统一模型接入实现（纯对话）：模型解析（<see cref="IAiModelResolver"/>，公共/团队授权口径）
/// → 协议客户端（<see cref="IChatClientProvider"/>，按渠道协议族构建）→ 消息组装 → 流式补全；
/// 图片理解节点经 <see cref="VisionAsync"/> 走多模态消息（仅支持视觉的模型）；Agent 应用节点经
/// <see cref="AgentAppAsync"/> 按生效配置装配工具链（技能/插件/知识库/流程工具）执行.
/// </summary>
[InjectOnScoped]
public sealed class WorkflowNodeAiInvoker : IWorkflowNodeAiInvoker
{
    /// <summary>图片内联大小上限（与对话附件图片内联上限一致）.</summary>
    internal const long MaxInlineImageSize = 20 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
    };

    private static readonly Dictionary<string, string> ImageExtensionMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
    };

    private readonly IAiModelResolver _modelResolver;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly AppContextProviderFactory _contextProviderFactory;
    private readonly AppSecurityService _appSecurityService;
    private readonly AppSecurityAuditWriter _appSecurityAuditWriter;
    private readonly IStorageService _storageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SystemOptions _systemOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowNodeAiInvoker"/> class.
    /// </summary>
    /// <param name="modelResolver">团队模型解析器.</param>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    /// <param name="contextProviderFactory">Agent 上下文提供者工厂（Agent 应用节点的工具链装配）.</param>
    /// <param name="appSecurityService">应用内容脱敏策略读取服务.</param>
    /// <param name="appSecurityAuditWriter">应用安全事件写入器.</param>
    /// <param name="storageService">存储服务（图片理解节点的平台文件 objectKey 读取）.</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂（图片理解节点的扩展名缺失图片地址下载）.</param>
    /// <param name="systemOptions">系统配置（Debug=true 时挂 OpenTelemetry 调试埋点）.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public WorkflowNodeAiInvoker(
        IAiModelResolver modelResolver,
        IChatClientProvider chatClientProvider,
        AppContextProviderFactory contextProviderFactory,
        AppSecurityService appSecurityService,
        AppSecurityAuditWriter appSecurityAuditWriter,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        SystemOptions systemOptions,
        ILoggerFactory loggerFactory)
    {
        _modelResolver = modelResolver;
        _chatClientProvider = chatClientProvider;
        _contextProviderFactory = contextProviderFactory;
        _appSecurityService = appSecurityService;
        _appSecurityAuditWriter = appSecurityAuditWriter;
        _storageService = storageService;
        _httpClientFactory = httpClientFactory;
        _systemOptions = systemOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WorkflowNodeAiInvoker>();
    }

    /// <inheritdoc/>
    public async Task<string> ChatAsync(WorkflowNodeChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveByIdAsync(request.ModelId, request.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException($"模型 {request.ModelId} 不存在或该团队不可用") { StatusCode = 400 };

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);

        // Debug 埋点：每次模型调用产生携带正文的 Span 与 gen_ai 指标
        if (_systemOptions.Debug)
        {
            chatClient = AiObservabilityMiddleware.WrapChatClient(chatClient);
        }

        var options = request.Temperature.HasValue ? new ChatOptions { Temperature = request.Temperature.Value } : null;
        var result = new StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync(BuildMessages(request), options, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta).ConfigureAwait(false);
            }
        }

        return result.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> AgentAppAsync(WorkflowNodeAgentAppRequest request, CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveByIdAsync(request.Config.ModelId, request.App.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("Agent 应用的对话模型不可用，请重新配置") { StatusCode = 400 };
        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);
        if (_systemOptions.Debug)
        {
            chatClient = AiObservabilityMiddleware.WrapChatClient(chatClient);
        }

        var buildContext = new AppAgentBuildContext
        {
            App = request.App,
            Config = request.Config,
            AppId = request.App.Id,
            TeamId = request.App.TeamId,
            UserId = request.UserId,
            SessionId = Guid.CreateVersion7(),
            WikiIds = ParseWikiIds(request.Config.WikiIds),
            GraphIds = ParseGraphIds(request.Config.GraphIds),
            PluginIds = ParseGuidList(request.Config.Plugins),
            WorkflowAppIds = ParseGuidList(request.Config.WorkflowApps),
            AgentAppIds = ParseGuidList(request.Config.AgentApps),
            SkillIds = ParseGuidList(request.Config.Skills),
            ToolApprovalMode = AppToolApprovalContract.ModeAuto,
        };
        var contextProviders = await _contextProviderFactory.BuildAsync(buildContext, cancellationToken).ConfigureAwait(false);

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = $"workflow-agent-{request.App.Id:N}",
                Name = string.IsNullOrWhiteSpace(request.App.Name) ? "agent" : request.App.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = string.IsNullOrWhiteSpace(request.Config.Prompt) ? null : request.Config.Prompt,
                },
                ChatHistoryProvider = new InlineChatHistoryProvider(request.History),
                AIContextProviders = contextProviders,
            },
            _loggerFactory);

        // 应用安全策略：Agent 应用节点内的工具调用结果/参数同样脱敏（流程节点级输入输出脱敏见引擎净化器）
        var policy = await _appSecurityService.GetPolicyAsync(request.App.Id, cancellationToken).ConfigureAwait(false);
        var securedAgent = AppSecurityAgentMiddleware.Wrap(agent, policy, _appSecurityAuditWriter);
        // Debug 埋点包在脱敏中间件之外，Span 只记录脱敏后内容
        if (_systemOptions.Debug)
        {
            securedAgent = AiObservabilityMiddleware.WrapAgent(securedAgent);
        }

        var result = new StringBuilder();
        await foreach (var update in securedAgent.RunStreamingAsync(request.Prompt, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
        }

        return result.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> VisionAsync(WorkflowNodeVisionRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveByIdAsync(request.ModelId, request.TeamId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException($"模型 {request.ModelId} 不存在或该团队不可用") { StatusCode = 400 };

        if (!resolved.Model.SupportsVision)
        {
            throw new BusinessException($"模型 {resolved.Model.Name} 不支持视觉输入，请选择支持图片理解的视觉模型") { StatusCode = 400 };
        }

        var chatClient = await _chatClientProvider.GetChatClientAsync(resolved.Model, resolved.Channel, cancellationToken).ConfigureAwait(false);

        // Debug 埋点：每次模型调用产生携带正文的 Span 与 gen_ai 指标
        if (_systemOptions.Debug)
        {
            chatClient = AiObservabilityMiddleware.WrapChatClient(chatClient);
        }

        var image = await ResolveImageContentAsync(request.Image, cancellationToken).ConfigureAwait(false);
        var message = new ChatMessage(ChatRole.User, [new TextContent(request.Prompt), image]);

        var options = request.Temperature.HasValue ? new ChatOptions { Temperature = request.Temperature.Value } : null;
        var result = new StringBuilder();
        await foreach (var update in chatClient.GetStreamingResponseAsync([message], options, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            result.Append(delta);
            if (onProgress != null)
            {
                await onProgress(delta).ConfigureAwait(false);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 解析图片来源为多模态内容：data: URI 与带图片扩展名的 http(s) 地址直传（由模型服务自行拉取），
    /// 其余 http(s) 地址服务端下载内联（≤20MB，image/*），平台文件 objectKey 走存储读取（仅 public/ 目录，防越权读取私有对象）.
    /// </summary>
    private async Task<AIContent> ResolveImageContentAsync(string image, CancellationToken cancellationToken)
    {
        var trimmed = image.Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return new DataContent(trimmed);
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // 远程引用必须用 UriContent（DataContent 仅接受 data: URI）；带图片扩展名的地址直传 URL 由模型服务拉取，
            // 扩展名无法推断时回退为服务端下载内联
            var urlMediaType = ImageExtensionMediaTypeOf(trimmed);
            if (urlMediaType != null)
            {
                return new UriContent(trimmed, urlMediaType);
            }

            return await DownloadImageAsync(trimmed, cancellationToken).ConfigureAwait(false);
        }

        // 平台文件 objectKey：仅允许公开目录（公开对象本就可匿名读取，无越权面），与对话附件同款 ".." 防护
        var objectKey = trimmed.TrimEnd('/');
        if (objectKey.StartsWith("public/", StringComparison.OrdinalIgnoreCase) && !objectKey.Contains(".."))
        {
            var content = await ChatAttachmentImageRewriter.TryLoadPublicImageAsync(objectKey, _storageService, _logger, cancellationToken).ConfigureAwait(false);
            if (content != null)
            {
                return content;
            }

            throw new BusinessException($"图片读取失败：{objectKey}") { StatusCode = 400 };
        }

        throw new BusinessException("图片来源无法识别：仅支持 http(s) 图片地址、data: URI 或平台公开文件 objectKey") { StatusCode = 400 };
    }

    private async Task<AIContent> DownloadImageAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("MoAIWorkflowVisionImage");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessException($"图片下载失败：HTTP {(int)response.StatusCode}") { StatusCode = 400 };
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType == null || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("图片地址返回的内容不是图片（Content-Type 非 image/*）") { StatusCode = 400 };
        }

        var length = response.Content.Headers.ContentLength;
        if (length is > MaxInlineImageSize)
        {
            throw new BusinessException($"图片超过内联上限（{MaxInlineImageSize / 1024 / 1024}MB）") { StatusCode = 400 };
        }

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (data.Length > MaxInlineImageSize)
        {
            throw new BusinessException($"图片超过内联上限（{MaxInlineImageSize / 1024 / 1024}MB）") { StatusCode = 400 };
        }

        return new DataContent(data, mediaType);
    }

    /// <summary>按 URL 路径扩展名推断图片媒体类型（不含扩展名或非图片返回 null）.</summary>
    private static string? ImageExtensionMediaTypeOf(string url)
    {
        var path = url;
        var queryMark = path.IndexOfAny(['?', '#']);
        if (queryMark >= 0)
        {
            path = path[..queryMark];
        }

        var dot = path.LastIndexOf('.');
        return dot >= 0 && ImageExtensions.Contains(path[dot..]) ? ImageExtensionMediaTypes[path[dot..]] : null;
    }

    private static List<ChatMessage> BuildMessages(WorkflowNodeChatRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, request.SystemPrompt));
        }

        if (request.History != null)
        {
            messages.AddRange(request.History);
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Prompt));
        return messages;
    }

    /// <summary>解析 JSON long 数组（知识库 id，非法/非正数过滤）.</summary>
    private static IReadOnlyList<long> ParseWikiIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<long>>(json) ?? [];
            return raw.Where(id => id > 0).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>解析 JSON long 数组（知识图谱 id，非法/非正数过滤）.</summary>
    private static IReadOnlyList<long> ParseGraphIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<long>>(json) ?? [];
            return raw.Where(id => id > 0).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>解析 JSON Guid 数组（插件/技能/流程工具 id，非法值过滤）.</summary>
    private static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
