namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件语义配置查询响应.
/// </summary>
public class QueryPluginSemanticCommandResponse
{
    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 插件 key.
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// 插件级使用须知/领域背景（随工具清单注入模型系统提示），≤2000 字符.
    /// </summary>
    public string UsageNotes { get; init; } = string.Empty;

    /// <summary>
    /// 语义条目列表.
    /// </summary>
    public List<PluginSemanticNoteItem> Notes { get; init; } = new();

    /// <summary>
    /// 工具元数据列表（仅已配置的工具）.
    /// </summary>
    public List<PluginToolMetaItem> ToolMetas { get; init; } = new();

    /// <summary>
    /// 插件的工具清单（语义配置的编辑参照：MCP/OpenAPI 为函数，动态插件为模板工具，静态插件为 run）.
    /// </summary>
    public List<QueryPluginToolItem> Tools { get; init; } = new();
}

/// <summary>
/// 插件语义条目项.
/// </summary>
public class PluginSemanticNoteItem
{
    /// <summary>
    /// 条目 id，新增时为空.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// 工具 key，空串=插件级条目.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <summary>
    /// 条目标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 条目内容（markdown）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 披露方式：always=常驻注入；onDemand=按需经 plugin_note 取回.
    /// </summary>
    public string Mode { get; init; } = "always";

    /// <summary>
    /// 排序.
    /// </summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// 插件工具元数据项.
/// </summary>
public class PluginToolMetaItem
{
    /// <summary>
    /// 工具 key.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <summary>
    /// 工具补充说明.
    /// </summary>
    public string ExtraNotes { get; init; } = string.Empty;

    /// <summary>
    /// 调用策略 JSON（denyPatterns + action），空串=未配置.
    /// </summary>
    public string CallPolicy { get; init; } = string.Empty;

    /// <summary>
    /// 只读提示.
    /// </summary>
    public bool ReadOnlyHint { get; init; }

    /// <summary>
    /// 破坏性提示.
    /// </summary>
    public bool DestructiveHint { get; init; }
}
