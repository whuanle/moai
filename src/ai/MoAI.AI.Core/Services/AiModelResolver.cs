using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;

namespace MoAI.AI.Services;

/// <summary>
/// <see cref="IAiModelResolver"/> 默认实现：与 <c>SaveAppAgentConfigCommandHandler.ValidateModelIdAsync</c> 口径一致.
/// </summary>
[InjectOnScoped]
public class AiModelResolver : IAiModelResolver
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelResolver"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public AiModelResolver(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveByIdAsync(Guid modelId, int teamId, CancellationToken cancellationToken = default)
    {
        if (modelId == Guid.Empty)
        {
            return null;
        }

        var candidates = await (from m in _databaseContext.AiModels
                                join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                                where m.Id == modelId && m.Enabled && c.Enabled
                                select new { m, c })
            .ToListAsync(cancellationToken);

        var pair = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (pair == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (!authorized)
            {
                return null;
            }

            pair = candidates.FirstOrDefault();
        }

        return pair == null ? null : (pair.m, pair.c);
    }
}
