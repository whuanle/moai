using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEntityTypeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEntityTypeCommandHandler : IRequestHandler<UpdateKnowledgeGraphEntityTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public UpdateKnowledgeGraphEntityTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entity = await _databaseContext.KnowledgeGraphEntityTypes
            .FirstOrDefaultAsync(x => x.Id == request.EntityTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, cancellationToken)
            ?? throw new BusinessException("实体类型不存在.") { StatusCode = 404 };

        var nameExist = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.Name == request.Name && x.Id != entity.Id, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("实体类型名称已存在.") { StatusCode = 409 };
        }

        entity.Name = request.Name;
        entity.Color = request.Color ?? string.Empty;
        entity.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
