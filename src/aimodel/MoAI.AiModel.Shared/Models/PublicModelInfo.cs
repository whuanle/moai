using MoAI.AI.Models;

namespace MoAI.AiModel.Models;

/// <summary>
/// AI 模型.
/// </summary>
public class PublicModelInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 对用户显示名称.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 模型类型.
    /// </summary>
    public AiModelType AiModelType { get; init; } = default!;

    /// <summary>
    /// the context window (or input + output tokens limit).
    /// </summary>
    public int ContextWindowTokens { get; init; }

    /// <summary>
    /// additional parameters.
    /// </summary>
    public ModelAbilities Abilities { get; init; } = default!;

    /// <summary>
    /// 最大模型输出 tokens，TextOutput.
    /// </summary>
    public int TextOutput { get; init; }

    /// <summary>
    /// 向量模型的维度.
    /// </summary>
    public int MaxDimension { get; init; }
}