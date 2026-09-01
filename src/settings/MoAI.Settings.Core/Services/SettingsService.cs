using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Queries.Responses;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 设置领域服务.
/// </summary>
public class SettingsService : ISettingsService
{
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
}
