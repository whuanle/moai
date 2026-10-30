using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Models;
using MoAI.Memory.Models;
using MoAI.Memory.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 长期记忆工具来源：应用开启记忆且用户未关闭时，提供 <c>memory_search</c>/<c>memory_save</c>/<c>memory_forget</c> 三只工具，
/// 供 AI 显式检索、保存与遗忘用户记忆；用户记忆模式为关闭时不注册.
/// </summary>
[InjectOnScoped]
public sealed class MemoryAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 记忆检索工具名称.
    /// </summary>
    public const string SearchToolName = "memory_search";

    /// <summary>
    /// 记忆保存工具名称.
    /// </summary>
    public const string SaveToolName = "memory_save";

    /// <summary>
    /// 记忆遗忘工具名称.
    /// </summary>
    public const string ForgetToolName = "memory_forget";

    private readonly IMemoryService _memoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryAppToolProvider"/> class.
    /// </summary>
    /// <param name="memoryService">记忆领域服务.</param>
    public MemoryAppToolProvider(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <inheritdoc/>
    public int Order => 22;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        // 子代理不携带记忆工具（保持独立干净上下文）
        if (context.IsSubAgent)
        {
            return [];
        }

        var resolution = await _memoryService.ResolveAsync(context.AppId, context.UserId, cancellationToken);
        if (resolution is not { IsEnabled: true })
        {
            return [];
        }

        var settings = resolution.Settings;
        var userMode = resolution.UserMode;
        var teamId = context.TeamId;
        var appId = context.AppId;
        var userId = context.UserId;
        var sessionId = context.SessionId;

        // 记忆提示词：应用配置的 guidance（空则用平台默认），追加为每只记忆工具的描述，
        // 让模型在工具列表里直接看到如何管理与使用记忆
        var promptSuffix = "\n\n【记忆管理约定】\n"
            + (string.IsNullOrWhiteSpace(settings.Prompt) ? AgentMemorySettings.DefaultPrompt : settings.Prompt.Trim());

        var searchTool = new AppTool
        {
            Name = SearchToolName,
            Title = "记忆检索",
            Description = "检索当前用户的跨会话长期记忆（偏好/事实、事件经历、方法经验），适合回答「我之前说过什么/我们上次聊到哪」类问题；" +
                "每轮对话开头系统已自动注入相关记忆，只有当自动记忆不够时才需要主动调用。topK 可选（1-20，默认 5）。" + promptSuffix,
            Kind = "memory",
            ParametersExample = "{\"query\":\"要检索的记忆关键词或问题\",\"topK\":5}",
            InvokeAsync = (argsJson, ct) => SearchAsync(appId, userId, settings, argsJson, ct),
        };

        var globalUsableHint = resolution.GlobalAllowed
            ? "scope 可选 app（默认，仅本应用）/ global（个人全局记忆，在用户所有允许全局接入的应用中生效，用户要求「全局记住/所有应用都记住」时使用）。"
            : string.Empty;
        var saveTool = new AppTool
        {
            Name = SaveToolName,
            Title = "记忆保存",
            Description = "把值得长期记住的信息写入当前用户记忆（用户明确要求记住，或出现重要的稳定偏好/事实/约定）。" +
                "type：semantic=事实偏好（默认）、episodic=事件经历、procedural=方法经验；与既有记忆高度相似时自动合并更新。" + globalUsableHint + promptSuffix,
            Kind = "memory",
            ParametersExample = "{\"content\":\"要记住的一句话，包含具体细节\",\"type\":\"semantic\",\"scope\":\"app\"}",
            InvokeAsync = (argsJson, ct) => SaveAsync(appId, teamId, userId, sessionId, settings, resolution, argsJson, ct),
        };

        var forgetHint = resolution.GlobalAllowed ? "（应用内与全局记忆的 id 均可删除）" : string.Empty;
        var forgetTool = new AppTool
        {
            Name = ForgetToolName,
            Title = "记忆遗忘",
            Description = "删除当前用户的一条记忆（用户明确要求忘记/更正某条信息时使用）；先用 memory_search 找到记忆 id。" + forgetHint + promptSuffix,
            Kind = "memory",
            ParametersExample = "{\"memoryId\":\"要删除的记忆 id\"}",
            InvokeAsync = (argsJson, ct) => ForgetAsync(appId, userId, resolution, argsJson, ct),
        };

        return [searchTool, saveTool, forgetTool];
    }

    private async Task<AppToolResult> SearchAsync(Guid appId, long userId, AgentMemorySettings settings, string? argsJson, CancellationToken cancellationToken)
    {
        var (query, topK) = ParseSearchArgs(argsJson);
        if (string.IsNullOrWhiteSpace(query))
        {
            return AppToolResult.Fail("缺少参数 query（要检索的记忆关键词或问题）.");
        }

        var items = await _memoryService.SearchAsync(new MemorySearchQuery
        {
            AppId = appId,
            UserId = userId,
            QueryText = query,
            TopK = topK,
        }, cancellationToken);

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["query"] = query,
            ["count"] = items.Count,
            ["memories"] = new System.Text.Json.Nodes.JsonArray(items.Select(x => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = x.Id,
                ["type"] = TypeName(x.MemoryType),
                ["scope"] = x.Scope == MoAI.Memory.Models.MemoryItem.ScopeGlobal ? "global" : "app",
                ["content"] = x.Content,
                ["score"] = Math.Round(x.Score, 4),
                ["updateTime"] = x.UpdateTime.ToString("yyyy-MM-dd"),
            }).ToArray()),
        };

        return AppToolResult.Ok(payload.ToJsonString(JsonOptions));
    }

    private async Task<AppToolResult> SaveAsync(Guid appId, int teamId, long userId, Guid sessionId, AgentMemorySettings settings, MemoryResolution resolution, string? argsJson, CancellationToken cancellationToken)
    {
        var (content, type, scope) = ParseSaveArgs(argsJson);
        if (string.IsNullOrWhiteSpace(content))
        {
            return AppToolResult.Fail("缺少参数 content（要记住的内容）.");
        }

        // scope=global：写入个人全局记忆（应用允许接入且用户未关闭时可用），在用户所有允许全局的应用中生效
        if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            if (!resolution.GlobalAllowed)
            {
                return AppToolResult.Fail("当前应用未开放全局记忆接入（管理员未允许或用户已关闭），请改用默认应用内记忆.");
            }

            try
            {
                var globalItem = await _memoryService.SaveGlobalAsync(userId, content, type, cancellationToken);
                return AppToolResult.Ok($"已保存全局记忆 {globalItem.Id}（{TypeName(globalItem.MemoryType)}），将在用户所有允许全局接入的应用中生效.");
            }
            catch (MoAI.Infra.Exceptions.BusinessException ex)
            {
                return AppToolResult.Fail(ex.Message);
            }
        }

        var item = await _memoryService.SaveAsync(new MemorySaveRequest
        {
            AppId = appId,
            TeamId = teamId,
            UserId = userId,
            SessionId = sessionId,
            Content = content,
            MemoryType = type,
            Settings = settings,
        }, cancellationToken);

        return AppToolResult.Ok($"已保存记忆 {item.Id}（{TypeName(item.MemoryType)}）.");
    }

    private async Task<AppToolResult> ForgetAsync(Guid appId, long userId, MemoryResolution resolution, string? argsJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}"
            || !JsonDocument.Parse(argsJson).RootElement.TryGetProperty("memoryId", out var idNode)
            || !Guid.TryParse(idNode.GetString(), out var memoryId))
        {
            return AppToolResult.Fail("缺少参数 memoryId（要删除的记忆 id）.");
        }

        var deleted = await _memoryService.DeleteAsync(appId, memoryId, userId, cancellationToken);

        // 应用内未命中且该应用接入全局记忆时，按全局记忆兜底删除（memory_search 的全局结果带 scope=global）
        if (!deleted && resolution.GlobalAllowed)
        {
            deleted = await _memoryService.DeleteGlobalAsync(userId, memoryId, cancellationToken);
        }

        return deleted
            ? AppToolResult.Ok("已删除该记忆.")
            : AppToolResult.Fail("记忆不存在或不属于当前用户.");
    }

    private static (string? Query, int TopK) ParseSearchArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}")
        {
            return (null, AgentMemorySettings.DefaultRetrievalTopK);
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var root = document.RootElement;
            var query = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String
                ? q.GetString()
                : null;
            var topK = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("topK", out var k) && k.ValueKind == JsonValueKind.Number && k.TryGetInt32(out var parsed)
                ? Math.Clamp(parsed, 1, 20)
                : AgentMemorySettings.DefaultRetrievalTopK;
            return (query, topK);
        }
        catch (JsonException)
        {
            return (null, AgentMemorySettings.DefaultRetrievalTopK);
        }
    }

    private static (string? Content, int Type, string? Scope) ParseSaveArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson.Trim() == "{}")
        {
            return (null, AgentMemorySettings.TypeSemantic, null);
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var root = document.RootElement;
            var content = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            var type = AgentMemorySettings.TypeSemantic;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
            {
                type = t.GetString() switch
                {
                    "episodic" => AgentMemorySettings.TypeEpisodic,
                    "procedural" => AgentMemorySettings.TypeProcedural,
                    _ => AgentMemorySettings.TypeSemantic,
                };
            }

            var scope = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("scope", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;
            return (content, type, scope);
        }
        catch (JsonException)
        {
            return (null, AgentMemorySettings.TypeSemantic, null);
        }
    }

    private static string TypeName(int memoryType) => memoryType switch
    {
        AgentMemorySettings.TypeEpisodic => "episodic",
        AgentMemorySettings.TypeProcedural => "procedural",
        _ => "semantic",
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
