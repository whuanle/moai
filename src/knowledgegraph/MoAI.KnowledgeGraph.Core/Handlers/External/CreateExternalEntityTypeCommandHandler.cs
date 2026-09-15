using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalEntityTypeCommand"/>
/// </summary>
public class CreateExternalEntityTypeCommandHandler : IRequestHandler<CreateExternalEntityTypeCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateExternalEntityTypeCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateExternalEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("实体类型名称已存在.") { StatusCode = 409 };
        }

        var maxSort = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .Select(x => (int?)x.Sort)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = request.KnowledgeGraphId,
            Name = request.Name,
            Color = request.Color ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Properties = KnowledgeGraphPropertyJson.WriteDefinitions(request.Properties),
            Sort = maxSort + 1,
        };
        _databaseContext.KnowledgeGraphEntityTypes.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleLong { Value = entity.Id };
    }
}
