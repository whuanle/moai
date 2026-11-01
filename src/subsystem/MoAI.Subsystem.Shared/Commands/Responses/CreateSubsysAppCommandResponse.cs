namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 创建系统级接入结果：appid 与密钥原文一次性返回（此后仅可凭前缀识别）.
/// </summary>
public class CreateSubsysAppCommandResponse
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
    /// 接入类型：subsystem=子系统 app=应用接入.
    /// </summary>
    public MoAI.Database.Enums.AccessAppType AppType { get; init; }

    /// <summary>
    /// 接入 appid 原文（仅此一次返回，请妥善保存）.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 密钥展示掩码（前 4 + **** + 后 4，便于核对；原文可随时经 reveal-secret 端点信封加密查看）.
    /// </summary>
    public string SecretMask { get; init; } = string.Empty;

    /// <summary>
    /// key 展示前缀.
    /// </summary>
    public string KeyPrefix { get; init; } = default!;
}
