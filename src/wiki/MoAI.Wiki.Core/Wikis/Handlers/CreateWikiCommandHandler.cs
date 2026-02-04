using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommandHandler : IRequestHandler<CreateWikiCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateWikiCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateWikiCommand request, CancellationToken cancellationToken)
    {
        // 只需要判断个人或团队下的知识库不能同名即可
        if (request.TeamId != null && request.TeamId > 0)
        {
            var existInTeam =
                await _databaseContext.Wikis.AnyAsync(x => x.Name == request.Name && x.TeamId == request.TeamId,
                    cancellationToken);
            if (existInTeam)
            {
                throw new BusinessException("团队已存在同名知识库") { StatusCode = 409 };
            }
        }
        else
        {
            var existInTeam = await _databaseContext.Wikis.AnyAsync(
                x => x.CreateUserId == request.ContextUserId && x.Name == request.Name, cancellationToken);
            if (existInTeam)
            {
                throw new BusinessException("已存在个人同名知识库") { StatusCode = 409 };
            }
        }

        var wikiEntity = new WikiEntity
        {
            Name = request.Name,
            Description = request.Description,
            Avatar = string.Empty, // Provide default empty avatar
            TeamId = request.TeamId ?? 0,
            EmbeddingModelId = default,
            IsPublic = false,
            IsLock = false
        };

        await _databaseContext.Wikis.AddAsync(wikiEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt
        {
            Value = wikiEntity.Id
        };
    }
}