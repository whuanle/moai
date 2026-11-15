namespace MoAI.Subsystem.Client;

/// <summary>
/// 心跳配置（<see cref="MoaiSubsysClientOptions.Heartbeat"/>）：
/// 多实例存活探针的实例标识、间隔与上报版本。
/// </summary>
public class SubsysHeartbeatOptions
{
    private readonly object _gate = new();
    private string? _generatedInstanceId;

    /// <summary>实例标识（进程内稳定，1~64 字符）；留空时按「机器名-进程 id-随机后缀」自动生成。</summary>
    public string? InstanceId { get; set; }

    /// <summary>心跳间隔（秒）；0=使用服务端响应回显的建议间隔（默认 20s，判定窗口 60s）。</summary>
    public int IntervalSeconds { get; set; }

    /// <summary>上报版本号，可空.</summary>
    public string? Version { get; set; }

    /// <summary>解析实际使用的实例标识（未配置时生成并保持稳定）.</summary>
    /// <returns>1~64 字符的实例标识.</returns>
    public string ResolveInstanceId()
    {
        if (!string.IsNullOrWhiteSpace(InstanceId))
        {
            return InstanceId.Length <= 64 ? InstanceId : InstanceId[..64];
        }

        lock (_gate)
        {
            if (_generatedInstanceId is not null)
            {
                return _generatedInstanceId;
            }

            var generated = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.CreateVersion7():N}";
            _generatedInstanceId = generated.Length <= 64 ? generated : generated[..64];
            return _generatedInstanceId;
        }
    }
}
