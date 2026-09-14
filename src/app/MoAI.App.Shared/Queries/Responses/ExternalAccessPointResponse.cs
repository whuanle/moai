namespace MoAI.App.Queries.Responses;

/// <summary>
/// 访问点公开配置（悬浮组件用，匿名可访问，仅只读字段）.
/// </summary>
public class ExternalAccessPointResponse
{
    /// <summary>
    /// 应用名.
    /// </summary>
    public string AppName { get; init; } = default!;

    /// <summary>
    /// 头像完整 URL（未设置时为空串）.
    /// </summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>
    /// 面板标题（未配置时用应用名）.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 欢迎语/副标题.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// 输入框占位文案.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// 主题色，#RRGGBB.
    /// </summary>
    public string? PrimaryColor { get; init; }

    /// <summary>
    /// 悬浮位置：bottomRight / bottomLeft（全局 CamelCase 枚举策略）.
    /// </summary>
    public string Position { get; init; } = "bottomRight";

    /// <summary>
    /// 悬浮按钮文案，空则用图标.
    /// </summary>
    public string? LauncherText { get; init; }

    /// <summary>
    /// 面板宽度 px.
    /// </summary>
    public int PanelWidth { get; init; }

    /// <summary>
    /// 面板高度 px.
    /// </summary>
    public int PanelHeight { get; init; }

    /// <summary>
    /// 是否默认展开.
    /// </summary>
    public bool DefaultOpen { get; init; }

    /// <summary>
    /// 应用是否需要授权访问（is_auth=true 时组件必须提供接入 key）.
    /// </summary>
    public bool IsAuth { get; init; }

    /// <summary>
    /// 是否启用访问点（应用已发布未禁用 且 配置启用）.
    /// </summary>
    public bool Enabled { get; init; }
}
