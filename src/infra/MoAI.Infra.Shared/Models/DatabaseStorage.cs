namespace MoAI.Infra.Models;

/// <summary>
/// DatabaseStore.
/// </summary>
public class DatabaseStorage
{
    /// <summary>
    /// 系统数据库类型.
    /// </summary>
    public string DBType { get; init; } = string.Empty;

    /// <summary>
    /// 系统数据库连接字符串.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}