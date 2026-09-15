using MoAI.Infra.Models;

namespace MoAI.Prompt.Queries.Responses;

/// <summary>
/// 提示词列表项，不含内容；个人/团队/市场列表共用.
/// </summary>
public class PromptItem : AuditsInfo
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 头像地址.
    /// </summary>
    public string AvatarPath { get; set; } = string.Empty;

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int PromptClassId { get; set; }

    /// <summary>
    /// 是否已公开到提示词市场.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 计数器，市场提示词被他人查看一次加一.
    /// </summary>
    public int Counter { get; set; }

    /// <summary>
    /// 所属团队 id，0 表示个人提示词.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 待审核的上架申请 id，无待审核申请时为 null.
    /// </summary>
    public long? PendingPublicationId { get; set; }
}
