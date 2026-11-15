namespace MoAI.Subsystem.Commands.Responses;

/// <summary>
/// 心跳响应：回显服务端时间、当前在线实例数与心跳窗口口径，便于接入方自适应心跳间隔.
/// </summary>
public class SubsystemHeartbeatCommandResponse
{
    /// <summary>在线判定窗口（秒）：超过该时长未续期的实例视为离线.</summary>
    public const int DefaultOnlineWindowSeconds = 60;

    /// <summary>建议心跳间隔（秒）：窗口的 1/3，可容忍两次丢失.</summary>
    public const int DefaultRecommendedIntervalSeconds = 20;

    /// <summary>服务端 UTC 时间.</summary>
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>本次心跳计入后的在线实例数（含当前实例）.</summary>
    public int OnlineInstances { get; init; }

    /// <summary>在线判定窗口（秒）.</summary>
    public int WindowSeconds { get; init; } = DefaultOnlineWindowSeconds;

    /// <summary>建议心跳间隔（秒）.</summary>
    public int RecommendedIntervalSeconds { get; init; } = DefaultRecommendedIntervalSeconds;
}
