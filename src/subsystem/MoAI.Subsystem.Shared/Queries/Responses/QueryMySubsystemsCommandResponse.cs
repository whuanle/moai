namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 当前用户可进入的子系统列表查询结果.
/// </summary>
public class QueryMySubsystemsCommandResponse
{
    /// <summary>
    /// 可进入的子系统列表.
    /// </summary>
    public IReadOnlyList<QueryMySubsystemsCommandResponseItem> Items { get; init; } = [];
}

/// <summary>
/// 可进入的子系统列表项（用户面门户展示用）.
/// </summary>
public class QueryMySubsystemsCommandResponseItem
{
    /// <summary>
    /// 子系统 id.
    /// </summary>
    public Guid SubsystemId { get; init; }

    /// <summary>
    /// 子系统标识（appid，前端据此拼 /sso/authorize?client= 进入）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 子系统名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 子系统头像 objectKey，空串=未设置（前端拼装 /static 地址展示）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;
}
