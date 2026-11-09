using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Jobs;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="ImportKnowledgeGraphFromFileCommand"/>
/// AI 导入文件生成图谱（B2 异步化）：快速校验后登记导入任务并交 Hangfire 执行，返回任务 id（D27）.
/// 实际抽取与幂等合并落库见 <see cref="KnowledgeGraphAiImportJob"/>（B1 证据链：重跑同文档合并不重复）.
/// </summary>
public class ImportKnowledgeGraphFromFileCommandHandler : IRequestHandler<ImportKnowledgeGraphFromFileCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportKnowledgeGraphFromFileCommandHandler"/> class.
    /// </summary>
    public ImportKnowledgeGraphFromFileCommandHandler(
        DatabaseContext databaseContext,
        IKnowledgeGraphAuthorizer authorizer,
        IKnowledgeGraphSettingsService settingsService,
        IBackgroundJobClient backgroundJobClient)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(ImportKnowledgeGraphFromFileCommand request, CancellationToken cancellationToken)
    {
        // 导入即写节点/边：仅托管图 Admin+（接入图只读 409 由授权器拦截）
        var (graph, _) = await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        // 快速失败校验（重校验在任务内，避免长请求占着连接）
        var hasTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.IsDeleted == 0, cancellationToken);
        if (!hasTypes)
        {
            throw new BusinessException("该图谱还没有实体类型，请先在「模型」页定义模型后再导入.") { StatusCode = 409 };
        }

        // D30 三级回退：请求级 aiModelId > 图谱默认抽取模型 > 400
        var extractModelId = request.AiModelId ?? graph.ExtractModelId
            ?? throw new BusinessException("未指定导入模型：请选择 AI 对话模型或在图谱设置中配置默认抽取模型.") { StatusCode = 400 };

        var job = new KgImportJobEntity
        {
            KnowledgeGraphId = request.KnowledgeGraphId,
            TeamId = graph.TeamId,
            Type = "file",
            FileName = request.FileName,
            ObjectKey = request.ObjectKey,
            AiModelId = extractModelId,
            Status = KnowledgeGraphImportJobStatus.Pending,
        };
        _databaseContext.KgImportJobs.Add(job);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<KnowledgeGraphAiImportJob>(x => x.RunAsync(job.Id, CancellationToken.None));
        return new SimpleString { Value = job.Id.ToString() };
    }
}
