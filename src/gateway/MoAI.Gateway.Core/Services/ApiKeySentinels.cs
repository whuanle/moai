namespace MoAI.Gateway.Services;

/// <summary>
/// team_api_key 非空时间列的哨兵值约定：
/// ExpireTime=FarFuture 表示永不过期，LastUsedTime=Epoch 表示从未使用.
/// </summary>
public static class ApiKeySentinels
{
    /// <summary>
    /// 永不过期哨兵：写入 DateTimeOffset.MaxValue（ Npgsql 精度截断到微秒），比较用 9999-01-01 边界避免精度误差.
    /// </summary>
    public static readonly DateTimeOffset NeverExpireBoundary = new(9999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 从未使用哨兵：写入 DateTimeOffset.MinValue.
    /// </summary>
    public static readonly DateTimeOffset NeverUsedBoundary = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 永不过期的写入值.
    /// </summary>
    public static DateTimeOffset NeverExpire => DateTimeOffset.MaxValue;

    /// <summary>
    /// 从未使用的写入值.
    /// </summary>
    public static DateTimeOffset NeverUsed => DateTimeOffset.MinValue;
}
