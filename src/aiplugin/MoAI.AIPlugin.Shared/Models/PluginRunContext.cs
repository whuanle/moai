using System.Text.Json.Serialization;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件执行上下文来源.
/// </summary>
public enum PluginRunSource
{
    /// <summary>
    /// 插件管理页（管理员运行）.
    /// </summary>
    [JsonPropertyName("admin")]
    Admin = 0,

    /// <summary>
    /// 团队插件面板（团队成员运行）.
    /// </summary>
    [JsonPropertyName("team")]
    Team = 1,

    /// <summary>
    /// 应用 AI 工具调用（Agent/流程应用对话）.
    /// </summary>
    [JsonPropertyName("agent")]
    Agent = 2,
}

/// <summary>
/// 插件执行上下文：由执行入口（管理页运行/团队运行/AI 工具调用）构建，经执行引擎注入到插件作用域，
/// 插件通过 <c>IPluginRunContextAccessor</c> 读取，无需自行解析调用方身份.
/// </summary>
public class PluginRunContext
{
    /// <summary>
    /// 调用方用户 id，未认证（如匿名/外部应用）时为 0.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 调用方用户类型.
    /// </summary>
    public UserType UserType { get; init; }

    /// <summary>
    /// 调用方所在团队 id，非团队维度调用时为 null.
    /// </summary>
    public long? TeamId { get; init; }

    /// <summary>
    /// 调用来源.
    /// </summary>
    public PluginRunSource Source { get; init; }

    /// <summary>
    /// 调用方是否为已认证的真实用户（id 大于 0）.
    /// </summary>
    public bool IsAuthenticated => UserId > 0;
}
