namespace MoAI.App.Queries.Responses;

/// <summary>
/// 访问点配置（内部管理视图，未保存过时返回默认值）.
/// </summary>
public class AppAccessPointConfigResponse
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 面板标题，空则用应用名.
    /// </summary>
    public string? Title { get; init; }

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
    /// 悬浮位置.
    /// </summary>
    public string Position { get; init; } = "bottom-right";

    /// <summary>
    /// 悬浮按钮文案，空则用图标.
    /// </summary>
    public string? LauncherText { get; init; }

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string? Avatar { get; init; }

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
    /// 是否启用访问点.
    /// </summary>
    public bool Enabled { get; init; }
}
