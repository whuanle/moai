using MoAI.Infra.Models;

namespace MoAIPrompt.Models;

/// <summary>
/// 查询提示词详细信息.
/// </summary>
public class PromptItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 助手设定,markdown.
    /// </summary>
    public string? Content { get; set; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 提示词分类id.
    /// </summary>
    public int PromptClassId { get; set; }
}