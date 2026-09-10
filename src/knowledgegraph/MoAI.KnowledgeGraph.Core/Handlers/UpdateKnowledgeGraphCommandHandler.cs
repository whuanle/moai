using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphCommand"/>
/// </summary>
public class UpdateKnowledgeGraphCommandHandler : IRequestHandler<UpdateKnowledgeGraphCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public UpdateKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);

        var nameExist = await _databaseContext.KnowledgeGraphs
            .AnyAsync(x => x.TeamId == graph.TeamId && x.Name == request.Name && x.Id != graph.Id, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        graph.Name = request.Name;
        graph.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
