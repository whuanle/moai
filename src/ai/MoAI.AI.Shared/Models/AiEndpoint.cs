namespace MoAI.AI.Models;

/// <summary>
/// AI 模型.
/// </summary>
public class AiEndpoint
{
    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 部署名称，适配 Azure Open AI ，非 Azure Open AI 跟 Name 同名.
    /// </summary>
    public string DeploymentName { get; init; } = default!;

    /// <summary>
    /// 对用户显示名称.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 模型类型.
    /// </summary>
    public AiModelType AiModelType { get; init; } = default!;

    /// <summary>
    /// AI 服务商.
    /// </summary>
    public AiProvider Provider { get; init; }

    /// <summary>
    /// the context window (or input + output tokens limit).
    /// </summary>
    public int ContextWindowTokens { get; init; }

    /// <summary>
    /// 请求端点.
    /// </summary>
    public string Endpoint { get; init; } = default!;

    /// <summary>
    /// key.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// additional parameters.
    /// </summary>
    public ModelAbilities? Abilities { get; init; } = new ModelAbilities();

    /// <summary>
    /// 最大模型输出 tokens，也可表示嵌入向量等最大输出数量.
    /// </summary>
    public int TextOutput { get; init; }

    /// <summary>
    /// 向量模型的维度.
    /// </summary>
    public int MaxDimension { get; init; }
}
