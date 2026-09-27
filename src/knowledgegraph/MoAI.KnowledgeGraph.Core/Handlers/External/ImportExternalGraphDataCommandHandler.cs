using MediatR;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="ImportExternalGraphDataCommand"/>
/// 外部授权后委托 <see cref="KnowledgeGraphDataImportService"/> 执行导入管线（与内部 /import-json 共用）.
/// </summary>
public class ImportExternalGraphDataCommandHandler : IRequestHandler<ImportExternalGraphDataCommand, ExternalImportResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly KnowledgeGraphDataImportService _importService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportExternalGraphDataCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="importService">结构化导入服务.</param>
    public ImportExternalGraphDataCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, KnowledgeGraphDataImportService importService)
    {
        _externalAuthorizer = externalAuthorizer;
        _importService = importService;
    }

    /// <inheritdoc/>
    public async Task<ExternalImportResponse> Handle(ImportExternalGraphDataCommand request, CancellationToken cancellationToken)
    {
        // 导入即写图：非本团队 404、接入图只读 409 由授权器拦截
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);
        return await _importService.ImportAsync(request.KnowledgeGraphId, new KnowledgeGraphImportPayload
        {
            Mode = request.Mode,
            AutoCreateTypes = request.AutoCreateTypes,
            ValidateOnly = request.ValidateOnly,
            DetectDuplicates = request.DetectDuplicates,
            Nodes = request.Nodes,
            Edges = request.Edges,
        }, cancellationToken);
    }
}
