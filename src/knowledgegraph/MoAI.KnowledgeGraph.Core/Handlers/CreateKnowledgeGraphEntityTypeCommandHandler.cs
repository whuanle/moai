using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphEntityTypeCommand"/>
/// </summary>
public class CreateKnowledgeGraphEntityTypeCommandHandler : IRequestHandler<CreateKnowledgeGraphEntityTypeCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateKnowledgeGraphEntityTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateKnowledgeGraphEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KgId == request.KgId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("实体类型名称已存在.") { StatusCode = 409 };
        }

        var maxSort = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KgId == request.KgId)
            .Select(x => (int?)x.Sort)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new KnowledgeGraphEntityTypeEntity
        {
            KgId = request.KgId,
            Name = request.Name,
            Color = request.Color ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Sort = maxSort + 1,
        };
        _databaseContext.KnowledgeGraphEntityTypes.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleLong { Value = entity.Id };
    }
}
