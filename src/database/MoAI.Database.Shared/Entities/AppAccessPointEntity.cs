using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 外部应用访问点配置，与外部应用 1:1.
/// </summary>
public partial class AppAccessPointEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 归属团队id，冗余团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 外部应用id，1:1（partial 唯一）.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 面板标题，空则用应用名.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 欢迎语/副标题.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// 输入框占位文案.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 主题色，#RRGGBB.
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// 悬浮位置：bottom-right / bottom-left.
    /// </summary>
    public string Position { get; set; } = default!;

    /// <summary>
    /// 悬浮按钮文案，空则用图标.
    /// </summary>
    public string? LauncherText { get; set; }

    /// <summary>
    /// 头像 objectKey（走存储）.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// 面板宽度 px.
    /// </summary>
    public int PanelWidth { get; set; }

    /// <summary>
    /// 面板高度 px.
    /// </summary>
    public int PanelHeight { get; set; }

    /// <summary>
    /// 是否默认展开.
    /// </summary>
    public bool DefaultOpen { get; set; }

    /// <summary>
    /// 是否启用访问点.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
