using System;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;

namespace MoAI.TeamAgent.Services;

/// <summary>
/// <inheritdoc cref="ITeamAgentSeeder"/>
/// </summary>
[InjectOnScoped]
public class TeamAgentSeeder : ITeamAgentSeeder
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamAgentSeeder"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public TeamAgentSeeder(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<int> EnsureSeededAsync(long teamId, CancellationToken cancellationToken = default)
    {
        var definitions = BuiltinTeamAgents.All();
        var keys = definitions.Select(x => x.Key).ToList();
        var names = definitions.Select(x => x.Name).ToList();

        // 按 key 幂等；旧版本种子行只有 name 没有 key，按 name 领养并补写 key（团队定制过名称的行不受影响，会作为新行种入）
        var existing = await _databaseContext.TeamAgents
            .Where(x => x.TeamId == teamId && (keys.Contains(x.Key) || names.Contains(x.Name)))
            .ToListAsync(cancellationToken);

        var byKey = existing.Where(x => !string.IsNullOrEmpty(x.Key)).ToDictionary(x => x.Key);
        var byName = existing.ToDictionary(x => x.Name);
        var adopted = 0;
        var seeded = 0;
        foreach (var definition in definitions)
        {
            if (byKey.ContainsKey(definition.Key))
            {
                continue;
            }

            if (byName.TryGetValue(definition.Name, out var legacy))
            {
                // 领养旧行：补写 key，不重复插入
                legacy.Key = definition.Key;
                adopted++;
                continue;
            }

            _databaseContext.TeamAgents.Add(new TeamAgentEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = teamId,
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                AvatarObjectKey = string.Empty,
                Enabled = true,
                IsSystem = true,
                MainPrompt = definition.MainPrompt,
                MainPluginIds = TeamAgentJson.SerializePluginIds(Array.Empty<Guid>()),
                MainWikiIds = TeamAgentJson.SerializeWikiIds(Array.Empty<long>()),
                MainGraphIds = TeamAgentJson.SerializeWikiIds(Array.Empty<long>()),
                Subs = TeamAgentJson.SerializeSubs(definition.Subs),
            });

            seeded++;
        }

        if (seeded > 0 || adopted > 0)
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return seeded;
    }
}
