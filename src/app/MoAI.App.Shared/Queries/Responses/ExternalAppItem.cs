namespace MoAI.App.Queries.Responses;

/// <summary>
/// 外部可见的应用信息.
/// </summary>
public class ExternalAppItem
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 应用描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 应用类型，普通应用=0,流程编排=1.
    /// </summary>
    public int AppType { get; init; }

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string Avatar { get; init; } = default!;
}
