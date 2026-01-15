using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Storage.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库详细信息.
/// </summary>
public class QueryWikiDetailInfoCommandHandler : IRequestHandler<QueryWikiDetailInfoCommand, QueryWikiInfoResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDetailInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiDetailInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiInfoResponse> Handle(QueryWikiDetailInfoCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId)
            .Select(x => new QueryWikiInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                EmbeddingDimensions = x.EmbeddingDimensions,
                EmbeddingModelId = x.EmbeddingModelId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsLock = x.IsLock,
                AvatarKey = x.Avatar,
                TeamId = x.TeamId,
                Counter = x.Counter,
                Role = Team.Models.TeamRole.Owner,
                DocumentCount = _databaseContext.WikiDocuments.Where(x => x.WikiId == request.WikiId).Count()
            }).FirstOrDefaultAsync();

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        await _mediator.Send(new QueryAvatarUrlCommand
        {
            Items = new[] { wiki }
        });

        if (wiki.TeamId != 0)
        {
            var teamRole = await _mediator.Send(
                new Team.Queries.QueryUserTeamRoleCommand
                {
                    TeamId = wiki.TeamId,
                    ContextUserId = request.ContextUserId
                },
                cancellationToken);

            wiki.Role = teamRole.Role;
        }

        return wiki;
    }
}
