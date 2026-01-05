namespace MoAI.AiModel.Authorization.Queries.Responses;

/// <summary>
/// 授权模型信息项.
/// </summary>
public class AuthorizedModelItem
{
    /// <summary>
    /// 模型ID.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 显示名称.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 授权ID.
    /// </summary>
    public int AuthorizationId { get; init; }
}
