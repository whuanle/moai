using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateExternalEntityTypeCommand"/>
/// </summary>
public class UpdateExternalEntityTypeCommandHandler : IRequestHandler<UpdateExternalEntityTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExternalEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public UpdateExternalEntityTypeCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateExternalEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);
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
        entity.Properties = KnowledgeGraphPropertyJson.WriteDefinitions(request.Properties);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
