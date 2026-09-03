using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiDocumentCommand"/>
/// </summary>
public class QueryWikiDocumentCommandHandler : IRequestHandler<QueryWikiDocumentCommand, QueryWikiDocumentCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryWikiDocumentCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiDocumentCommandResponse> Handle(QueryWikiDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _databaseContext.WikiDocuments
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId, cancellationToken);

        if (document == null)
        {
            throw new BusinessException("文档不存在.") { StatusCode = 404 };
        }

        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == document.WikiId, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        return new QueryWikiDocumentCommandResponse
        {
            DocumentId = document.Id,
            WikiId = document.WikiId,
            Title = document.Title,
            Content = document.Content,
            MyRole = (int)myRole.Value,
            CreateTime = document.CreateTime,
            UpdateTime = document.UpdateTime
        };
    }
}
