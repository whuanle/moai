using MediatR;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="ImportKnowledgeGraphJsonCommand"/>
/// JSON 结构化导入：容错解析导入页提交的 JSON 文本 → 校验载荷 → 委托 <see cref="KnowledgeGraphDataImportService"/> 落库
/// （与外部 /import 共用管线：类型名引用/自动建类型/业务 key 幂等 upsert/端点引用/逐条失败报告/validateOnly 预检）.
/// </summary>
public class ImportKnowledgeGraphJsonCommandHandler : IRequestHandler<ImportKnowledgeGraphJsonCommand, External.ExternalImportResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly KnowledgeGraphDataImportService _importService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportKnowledgeGraphJsonCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">图谱授权器.</param>
    /// <param name="importService">结构化导入服务.</param>
    public ImportKnowledgeGraphJsonCommandHandler(IKnowledgeGraphAuthorizer authorizer, KnowledgeGraphDataImportService importService)
    {
        _authorizer = authorizer;
        _importService = importService;
    }

    /// <inheritdoc/>
    public async Task<External.ExternalImportResponse> Handle(ImportKnowledgeGraphJsonCommand request, CancellationToken cancellationToken)
    {
        // 导入即写节点/边：仅托管图 Admin+（接入图只读 409 由授权器拦截），与 AI 导入同权限口径
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var parsed = KnowledgeGraphJsonImportParser.Parse(request.Content);
        // 页面控件（模式/自动建类型/预检）优先于 JSON 内同名字段
        var payload = new KnowledgeGraphImportPayload
        {
            Mode = request.Mode ?? parsed.Mode,
            AutoCreateTypes = request.AutoCreateTypes ?? parsed.AutoCreateTypes,
            ValidateOnly = request.ValidateOnly ?? parsed.ValidateOnly,
            DetectDuplicates = request.DetectDuplicates ?? parsed.DetectDuplicates,
            Nodes = parsed.Nodes,
            Edges = parsed.Edges,
        };
        return await _importService.ImportAsync(request.KnowledgeGraphId, payload, cancellationToken);
    }
}
