using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Models;
using MoAI.Settings.Queries.Responses;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 设置领域服务.
/// </summary>
public class SettingsService : ISettingsService
{
    /// <summary>
    /// 网站名称最大长度（去首尾空白后）.
    /// </summary>
    public const int MaxSystemNameLength = 50;

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public SettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySettingsCommandResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var entities = await _databaseContext.Settings.ToListAsync(cancellationToken);
        var entityMap = entities.ToDictionary(e => e.Key);

        var items = SettingDefinitions.All
            .Select(definition =>
            {
                entityMap.TryGetValue(definition.Key, out var entity);
                return new SettingItemResponse
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    Value = entity?.Value ?? definition.DefaultValue
                };
            })
            .ToArray();

        return new QuerySettingsCommandResponse
        {
            Items = items
        };
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> SaveSettingAsync(SaveSettingCommand command, CancellationToken cancellationToken)
    {
        var definition = SettingDefinitions.Find(command.Key);
        if (definition == null)
        {
            throw new BusinessException("无效的配置项.") { StatusCode = 400 };
        }

        ValidateValue(command.Key, command.Value);

        var entity = await _databaseContext.Settings.FirstOrDefaultAsync(s => s.Key == command.Key, cancellationToken);
        if (entity == null)
        {
            entity = new SettingEntity
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Value = command.Value
            };
            await _databaseContext.Settings.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Value = command.Value;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 校验带格式约束的设置值（沙箱上限三项、网站名称），非法值直接拒绝，避免脏上限卡住全部应用的沙箱配置保存.
    /// </summary>
    private static void ValidateValue(string key, string value)
    {
        switch (key)
        {
            case SettingDefinitions.SystemNameKey:
                if (value.Trim().Length > MaxSystemNameLength)
                {
                    throw new BusinessException($"网站名称长度不能超过 {MaxSystemNameLength} 个字符.") { StatusCode = 400 };
                }

                break;
            case SettingDefinitions.SandboxMaxTtlKey:
                if (!int.TryParse(value, out var ttl) || ttl < SandboxSettingsService.MinTtlSeconds || ttl > SandboxSettingsService.MaxTtlSeconds)
                {
                    throw new BusinessException($"沙箱存活时间上限必须是 {SandboxSettingsService.MinTtlSeconds}~{SandboxSettingsService.MaxTtlSeconds} 之间的整数（秒）.") { StatusCode = 400 };
                }

                break;

            case SettingDefinitions.SandboxMaxCpuKey:
                if (!SandboxQuantity.TryParseCpu(value, out var millicores) || millicores <= 0)
                {
                    throw new BusinessException("沙箱 CPU 上限格式无效，例如 4 或 2000m.") { StatusCode = 400 };
                }

                break;

            case SettingDefinitions.SandboxMaxMemoryKey:
                if (!SandboxQuantity.TryParseMemory(value, out var bytes) || bytes <= 0)
                {
                    throw new BusinessException("沙箱内存上限格式无效，例如 8Gi 或 512Mi.") { StatusCode = 400 };
                }

                break;
        }
    }
}
