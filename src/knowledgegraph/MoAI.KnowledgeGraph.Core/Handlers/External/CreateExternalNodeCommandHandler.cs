using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalNodeCommand"/>
/// </summary>
public class CreateExternalNodeCommandHandler : IRequestHandler<CreateExternalNodeCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public CreateExternalNodeCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(CreateExternalNodeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);

        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        }

        var propsJson = KnowledgeGraphPropertyJson.WriteValues(request.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal));
        var node = await _store.CreateNodeAsync(request.KnowledgeGraphId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, propsJson, cancellationToken);
        return new SimpleString { Value = node.Id };
    }
}
