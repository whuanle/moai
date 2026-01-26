using MoAI.App.Models;
using MoAI.Infra.Models;
using MoAI.Storage.Queries;

namespace MoAI.App.Chat.Manager.Models;

/// <summary>
/// 应用详细信息响应.
/// </summary>
public class QueryChatAppConfigCommandResponse : IAvatarPath
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Avatar { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string AvatarKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; set; }

    /// <summary>
    /// 应用类型.
    /// </summary>
    public AppType AppType { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 模型id.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <summary>
    /// 知识库id列表.
    /// </summary>
    public IReadOnlyCollection<int> WikiIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<string> Plugins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; set; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// 是否开启授权验证.
    /// </summary>
    public bool IsAuth { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
