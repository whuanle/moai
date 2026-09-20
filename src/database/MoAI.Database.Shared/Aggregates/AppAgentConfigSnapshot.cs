using System;
using System.Text.Json;
using MoAI.Database.Entities;

namespace MoAI.Database.Aggregates;

/// <summary>
/// app_agent_config.published_config 发布配置快照契约：
/// Agent 应用（全字段）与流程应用（仅开场白有意义）发布时整行写入，正式会话按此快照执行；
/// 列表类字段沿用行内 JSON 文本原样透传，由消费方按现有方式解析.
/// </summary>
public sealed class AppAgentConfigSnapshot
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 系统提示词.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 对话使用的模型ID.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 绑定的知识库ID列表，JSON 数组文本.
    /// </summary>
    public string WikiIds { get; set; } = "[]";

    /// <summary>
    /// 绑定的插件ID列表，JSON 数组文本.
    /// </summary>
    public string Plugins { get; set; } = "[]";

    /// <summary>
    /// 绑的流程应用ID列表（作为工具使用），JSON 数组文本.
    /// </summary>
    public string WorkflowApps { get; set; } = "[]";

    /// <summary>
    /// 应用默认使用的技能ID列表，JSON 数组文本.
    /// </summary>
    public string Skills { get; set; } = "[]";

    /// <summary>
    /// 对话影响参数，JSON 对象文本.
    /// </summary>
    public string ExecutionSettings { get; set; } = "{}";

    /// <summary>
    /// 对话开场白.
    /// </summary>
    public string OpeningStatement { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用对话开场白.
    /// </summary>
    public bool OpeningStatementEnabled { get; set; }

    /// <summary>
    /// 快捷输入列表，JSON 数组文本.
    /// </summary>
    public string QuickInputs { get; set; } = "[]";

    /// <summary>
    /// 把配置行整行序列化为快照 JSON（发布时写入 published_config）.
    /// </summary>
    /// <param name="config">配置行.</param>
    /// <returns>快照 JSON 文本.</returns>
    public static string Serialize(AppAgentConfigEntity config)
    {
        var snapshot = new AppAgentConfigSnapshot
        {
            Prompt = config.Prompt ?? string.Empty,
            ModelId = config.ModelId,
            WikiIds = string.IsNullOrWhiteSpace(config.WikiIds) ? "[]" : config.WikiIds,
            Plugins = string.IsNullOrWhiteSpace(config.Plugins) ? "[]" : config.Plugins,
            WorkflowApps = string.IsNullOrWhiteSpace(config.WorkflowApps) ? "[]" : config.WorkflowApps,
            Skills = string.IsNullOrWhiteSpace(config.Skills) ? "[]" : config.Skills,
            ExecutionSettings = string.IsNullOrWhiteSpace(config.ExecutionSettings) ? "{}" : config.ExecutionSettings,
            OpeningStatement = config.OpeningStatement ?? string.Empty,
            OpeningStatementEnabled = config.OpeningStatementEnabled,
            QuickInputs = string.IsNullOrWhiteSpace(config.QuickInputs) ? "[]" : config.QuickInputs,
        };
        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    /// <summary>
    /// 解析快照 JSON；空白或非法 JSON 返回 null（调用方回退实时配置）.
    /// </summary>
    /// <param name="json">快照 JSON 文本.</param>
    /// <returns>快照对象.</returns>
    public static AppAgentConfigSnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppAgentConfigSnapshot>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析会话生效配置：应用已发布且要求使用发布版且快照有效时，返回按快照填充的脱管克隆行；
    /// 否则返回实时草稿行. 克隆行仅用于读取，不进入变更跟踪.
    /// </summary>
    /// <param name="app">应用行.</param>
    /// <param name="config">实时配置行（草稿）.</param>
    /// <param name="preferPublished">是否优先发布版（正式会话传 true，调试会话传 false）.</param>
    /// <returns>生效配置行.</returns>
    public static AppAgentConfigEntity ResolveEffectiveConfig(AppEntity app, AppAgentConfigEntity config, bool preferPublished)
    {
        if (!preferPublished || app.PublishStatus != 1 || string.IsNullOrWhiteSpace(config.PublishedConfig))
        {
            return config;
        }

        var snapshot = TryParse(config.PublishedConfig);
        if (snapshot == null)
        {
            return config;
        }

        return new AppAgentConfigEntity
        {
            Id = config.Id,
            TeamId = config.TeamId,
            AppId = config.AppId,
            Prompt = snapshot.Prompt,
            ModelId = snapshot.ModelId,
            WikiIds = snapshot.WikiIds,
            Plugins = snapshot.Plugins,
            WorkflowApps = snapshot.WorkflowApps,
            Skills = snapshot.Skills,
            ExecutionSettings = snapshot.ExecutionSettings,
            OpeningStatement = snapshot.OpeningStatement,
            OpeningStatementEnabled = snapshot.OpeningStatementEnabled,
            QuickInputs = snapshot.QuickInputs,
            PublishedConfig = config.PublishedConfig,
            Status = config.Status,
        };
    }
}
