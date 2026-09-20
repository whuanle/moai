using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 沙箱上限设置读取服务.
/// </summary>
public class SandboxSettingsService : ISandboxSettingsService
{
    /// <summary>
    /// 存活时间下限（秒），与前端应用配置页的最小值一致.
    /// </summary>
    public const int MinTtlSeconds = 60;

    /// <summary>
    /// 存活时间上限（秒，7 天）.
    /// </summary>
    public const int MaxTtlSeconds = 604800;

    private static readonly string[] Keys =
    {
        SettingDefinitions.SandboxMaxTtlKey,
        SettingDefinitions.SandboxMaxCpuKey,
        SettingDefinitions.SandboxMaxMemoryKey
    };

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public SandboxSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SandboxLimitsSettings> GetLimitsAsync(CancellationToken cancellationToken)
    {
        var values = await _databaseContext.Settings
            .Where(s => Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Resolve(string key)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
        }

        if (!int.TryParse(Resolve(SettingDefinitions.SandboxMaxTtlKey), out var maxTtlSeconds)
            || maxTtlSeconds < MinTtlSeconds)
        {
            maxTtlSeconds = int.TryParse(SettingDefinitions.Find(SettingDefinitions.SandboxMaxTtlKey)?.DefaultValue, out var ttl)
                ? Math.Clamp(ttl, MinTtlSeconds, MaxTtlSeconds)
                : MaxTtlSeconds;
        }

        maxTtlSeconds = Math.Clamp(maxTtlSeconds, MinTtlSeconds, MaxTtlSeconds);

        var maxCpu = Resolve(SettingDefinitions.SandboxMaxCpuKey);
        if (!SandboxQuantity.TryParseCpu(maxCpu, out var maxCpuMillicores) || maxCpuMillicores <= 0)
        {
            maxCpu = SettingDefinitions.Find(SettingDefinitions.SandboxMaxCpuKey)?.DefaultValue ?? "4";
            SandboxQuantity.TryParseCpu(maxCpu, out maxCpuMillicores);
        }

        var maxMemory = Resolve(SettingDefinitions.SandboxMaxMemoryKey);
        if (!SandboxQuantity.TryParseMemory(maxMemory, out var maxMemoryBytes) || maxMemoryBytes <= 0)
        {
            maxMemory = SettingDefinitions.Find(SettingDefinitions.SandboxMaxMemoryKey)?.DefaultValue ?? "8Gi";
            SandboxQuantity.TryParseMemory(maxMemory, out maxMemoryBytes);
        }

        return new SandboxLimitsSettings
        {
            MaxTtlSeconds = maxTtlSeconds,
            MaxCpu = maxCpu,
            MaxMemory = maxMemory,
            MaxCpuMillicores = maxCpuMillicores,
            MaxMemoryBytes = maxMemoryBytes
        };
    }
}
