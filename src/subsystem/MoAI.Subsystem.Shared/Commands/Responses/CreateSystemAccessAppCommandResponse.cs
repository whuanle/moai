namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 创建系统应用接入结果：appid 与密钥原文一次性返回（此后仅可凭前缀识别）.
/// </summary>
public class CreateSystemAccessAppCommandResponse
{
    /// <summary>
    /// 接入 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 接入 appid 原文（moai-sa- 前缀，仅此一次返回，请妥善保存）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 接入密钥原文（仅此一次返回，请妥善保存；经 /api/subsys/system/token 换取 access token 时使用）.
    /// </summary>
    public string Secret { get; init; } = default!;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; init; } = default!;
}
