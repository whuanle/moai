using System.Text;
using System.Text.Json;
using Hangfire;
using Maomi.MQ;
using Maomi.ToMarkdown;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;
using MoAI.Storage.Services;
using MoAI.Storage.TextExtract;

namespace MoAI.KnowledgeGraph.Jobs;

/// <summary>
/// AI 文档导入任务（Hangfire 一次性任务，B2 导入状态机）：提取 → 抽取 → 幂等合并落库（B1 证据链），
/// 阶段与进度回写 kg_import_job；失败置 failed 由用户经重试端点重发（AutomaticRetry=0 禁用 Hangfire 自动重试，状态机自管）.
/// 重跑安全：节点按（类型+名称）命中即合并（描述去重拼接/属性目标优先/证据 FIFO 追加），边按三元组已存在跳过.
/// </summary>
[AutomaticRetry(Attempts = 0)]
public class KnowledgeGraphAiImportJob
{
    /// <summary>
    /// 提取文本截断上限（发给模型的字符预算，防止超长文档撑爆上下文）.
    /// </summary>
    public const int MaxPromptChars = 12_000;

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;
    private readonly IStorageService _storageService;
    private readonly TextExtractionService _textExtractionService;
    private readonly IAiChatCompletionService _chatCompletionService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<KnowledgeGraphAiImportJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphAiImportJob"/> class.
    /// </summary>
    public KnowledgeGraphAiImportJob(
        DatabaseContext databaseContext,
        IKnowledgeGraphSettingsService settingsService,
        IKnowledgeGraphStore store,
        IStorageService storageService,
        TextExtractionService textExtractionService,
        IAiChatCompletionService chatCompletionService,
        IMessagePublisher messagePublisher,
        ILogger<KnowledgeGraphAiImportJob> logger)
    {
        _databaseContext = databaseContext;
        _settingsService = settingsService;
        _store = store;
        _storageService = storageService;
        _textExtractionService = textExtractionService;
        _chatCompletionService = chatCompletionService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <summary>
    /// 执行导入任务.
    /// </summary>
    /// <param name="jobId">任务 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>任务.</returns>
    public async Task RunAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await _databaseContext.KgImportJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job == null || job.Status == KnowledgeGraphImportJobStatus.Done || job.Status == KnowledgeGraphImportJobStatus.Cancelled)
        {
            return;
        }

        var batchId = Guid.CreateVersion7();
        try
        {
            var settings = await _settingsService.GetAsync(cancellationToken);
            if (!settings.Enabled)
            {
                throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
            }

            var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
                .Where(x => x.KnowledgeGraphId == job.KnowledgeGraphId && x.IsDeleted == 0)
                .OrderBy(x => x.Sort)
                .ToListAsync(cancellationToken);
            var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
                .Where(x => x.KnowledgeGraphId == job.KnowledgeGraphId && x.IsDeleted == 0)
                .OrderBy(x => x.Sort)
                .ToListAsync(cancellationToken);
            if (entityTypes.Count == 0)
            {
                throw new BusinessException("该图谱还没有实体类型，请先在「模型」页定义模型后再导入.") { StatusCode = 409 };
            }

            var (model, channel) = await ResolveChatModelAsync(job.AiModelId, cancellationToken);

            // ===== 阶段一：提取 =====
            await TransitionAsync(job, KnowledgeGraphImportJobStatus.Extracting, cancellationToken);
            string markdown;
            var truncated = false;
            try
            {
                var file = await _storageService.ReadAsync(job.ObjectKey, cancellationToken);
                await using (file.FileStream)
                {
                    markdown = DocumentTextExtract.IsNativeSupported(job.FileName)
                        ? await _textExtractionService.ExtractAsync(file.FileStream, job.FileName, cancellationToken)
                        : await DocumentTextExtract.ExtractAsync(file.FileStream, job.FileName, cancellationToken);
                }
            }
            catch (BusinessException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new BusinessException("文件不存在或内容提取失败，请确认文件格式是否受支持.") { StatusCode = 400 };
            }

            var contentLength = markdown.Length;
            if (contentLength == 0)
            {
                throw new BusinessException("文件内容为空，无法导入.") { StatusCode = 400 };
            }

            if (contentLength > MaxPromptChars)
            {
                markdown = markdown[..MaxPromptChars];
                truncated = true;
            }

            // ===== 阶段二：抽取 =====
            await ThrowIfCancelledAsync(job, cancellationToken);
            await TransitionAsync(job, KnowledgeGraphImportJobStatus.Parsing, cancellationToken);
            var prompt = BuildPrompt(entityTypes, relationTypes, markdown);
            var output = await _chatCompletionService.CompleteTextAsync(
                model,
                channel,
                prompt,
                new AiChatCompletionOptions
                {
                    DisableThinking = true,
                    MaxOutputTokens = 8192,
                    Temperature = 0.2f,
                    PreferJsonResponse = true,
                },
                cancellationToken);

            var (nodeDrafts, edgeDrafts) = KnowledgeGraphImportParser.Parse(output);
            job.TotalRows = nodeDrafts.Count + edgeDrafts.Count;
            await _databaseContext.SaveChangesAsync(cancellationToken);

            if (nodeDrafts.Count == 0)
            {
                await TransitionAsync(job, KnowledgeGraphImportJobStatus.Done, cancellationToken, resultJson: Serialize(new ImportKnowledgeGraphFromFileResponse
                {
                    ContentLength = contentLength,
                    Truncated = truncated,
                    Message = "AI 未从文件中抽取到符合当前模型的实体，请检查模型是否匹配文件内容.",
                }));
                return;
            }

            // ===== 阶段三：幂等合并落库 =====
            await ThrowIfCancelledAsync(job, cancellationToken);
            await TransitionAsync(job, KnowledgeGraphImportJobStatus.Merging, cancellationToken);
            var result = await WriteAsync(job, entityTypes, relationTypes, nodeDrafts, edgeDrafts, batchId, contentLength, truncated, cancellationToken);

            await TransitionAsync(job, KnowledgeGraphImportJobStatus.Done, cancellationToken, resultJson: Serialize(result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkCancelledAsync(job);
        }
        catch (BusinessException ex)
        {
            await MarkFailedAsync(job, ex.Message, ex.StatusCode == 0 ? 400 : ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 导入任务执行失败. JobId={JobId}", jobId);
            await MarkFailedAsync(job, ex.Message, 500);
        }
    }

    /// <summary>
    /// 幂等合并落库：节点按（类型+名称）命中合并（描述去重拼接、属性目标优先、证据追加），未命中新建带证据；边按三元组去重跳过已存在.
    /// </summary>
    private async Task<ImportKnowledgeGraphFromFileResponse> WriteAsync(
        KgImportJobEntity job,
        List<KnowledgeGraphEntityTypeEntity> entityTypes,
        List<KnowledgeGraphRelationTypeEntity> relationTypes,
        IReadOnlyList<KnowledgeGraphImportNodeDraft> nodeDrafts,
        IReadOnlyList<KnowledgeGraphImportEdgeDraft> edgeDrafts,
        Guid batchId,
        int contentLength,
        bool truncated,
        CancellationToken cancellationToken)
    {
        var typeNameToId = entityTypes.ToDictionary(x => x.Name, x => x.Id);
        var relationNameToEntity = relationTypes.ToDictionary(x => x.Name, x => x);
        var propertyDefs = entityTypes.ToDictionary(
            x => x.Name,
            x => KnowledgeGraphPropertyJson.ParseDefinitions(x.Properties));

        // 预筛合法草稿（类型存在 + 名称去重）
        var validDrafts = new List<(KnowledgeGraphImportNodeDraft Draft, long EntityTypeId)>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var skippedNodes = 0;
        foreach (var draft in nodeDrafts)
        {
            if (!seenNames.Add(draft.Name) || !typeNameToId.TryGetValue(draft.EntityType, out var entityTypeId))
            {
                skippedNodes++;
                continue;
            }

            validDrafts.Add((draft, entityTypeId));
        }

        // 批量匹配已有节点（类型+名称）：命中合并、未命中新建
        var existing = await _store.GetNodesByTypeAndNamesAsync(
            job.KnowledgeGraphId,
            validDrafts.Select(x => (x.EntityTypeId, x.Draft.Name)).ToList(),
            cancellationToken);

        var nameToId = new Dictionary<string, string>(StringComparer.Ordinal);
        var nodeIdToTypeId = new Dictionary<string, long>();
        var nodesCreated = 0;
        var nodesMerged = 0;
        var touchedNodeIds = new List<string>();
        foreach (var (draft, entityTypeId) in validDrafts)
        {
            // 属性仅保留模型已定义的键，避免 AI 编造属性名
            var allowed = propertyDefs[draft.EntityType];
            var draftValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var def in allowed)
            {
                if (draft.Properties.TryGetValue(def.Name, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    draftValues[def.Name] = value;
                }
            }

            if (existing.TryGetValue((entityTypeId, draft.Name), out var hit))
            {
                // B1 幂等合并：描述去重拼接、属性目标优先（缺失键补齐）、证据 FIFO 追加
                var mergedProps = KnowledgeGraphPropertyJson.ParseValues(hit.PropsJson);
                foreach (var pair in draftValues)
                {
                    mergedProps.TryAdd(pair.Key, pair.Value);
                }

                await _store.UpdateNodeEvidenceAsync(
                    job.KnowledgeGraphId,
                    hit.Id,
                    KnowledgeGraphSourceEvidence.MergeDescription(hit.Description, draft.Description),
                    mergedProps.Count == 0 ? null : KnowledgeGraphPropertyJson.WriteValues(mergedProps),
                    KnowledgeGraphSourceEvidence.Append(hit.SrcJson, batchId, job.FileName),
                    cancellationToken);
                nameToId[draft.Name] = hit.Id;
                nodeIdToTypeId[hit.Id] = entityTypeId;
                nodesMerged++;
                touchedNodeIds.Add(hit.Id);
            }
            else
            {
                var record = await _store.CreateNodeAsync(
                    job.KnowledgeGraphId,
                    entityTypeId,
                    draft.Name,
                    draft.Description ?? string.Empty,
                    KnowledgeGraphPropertyJson.WriteValues(draftValues),
                    key: null,
                    cancellationToken,
                    srcJson: KnowledgeGraphSourceEvidence.Append(null, batchId, job.FileName));
                nameToId[draft.Name] = record.Id;
                nodeIdToTypeId[record.Id] = entityTypeId;
                nodesCreated++;
                touchedNodeIds.Add(record.Id);
            }

            job.DoneRows++;
            if (job.DoneRows % 10 == 0)
            {
                await ThrowIfCancelledAsync(job, cancellationToken);
                await _databaseContext.SaveChangesAsync(cancellationToken);
            }
        }

        // 边：批内三元组去重 + 已存在跳过（幂等），起止约束与建边接口同规则
        var validTriples = new HashSet<(string SourceNodeId, long RelationTypeId, string TargetNodeId)>();
        var skippedEdges = 0;
        foreach (var draft in edgeDrafts)
        {
            if (!relationNameToEntity.TryGetValue(draft.RelationType, out var relationType)
                || !nameToId.TryGetValue(draft.Source, out var sourceNodeId)
                || !nameToId.TryGetValue(draft.Target, out var targetNodeId))
            {
                skippedEdges++;
                continue;
            }

            if ((relationType.SourceTypeId != null && relationType.SourceTypeId != nodeIdToTypeId[sourceNodeId])
                || (relationType.TargetTypeId != null && relationType.TargetTypeId != nodeIdToTypeId[targetNodeId]))
            {
                skippedEdges++;
                continue;
            }

            validTriples.Add((sourceNodeId, relationType.Id, targetNodeId));
        }

        var existingEdgeKeys = await _store.GetExistingEdgeKeysAsync(
            job.KnowledgeGraphId,
            validTriples.Select(x => (x.SourceNodeId, x.RelationTypeId, x.TargetNodeId)).ToList(),
            cancellationToken);

        var edgesCreated = 0;
        foreach (var (sourceNodeId, relationTypeId, targetNodeId) in validTriples)
        {
            if (existingEdgeKeys.Contains($"{sourceNodeId}|{relationTypeId}|{targetNodeId}"))
            {
                // 同三元组已存在：幂等跳过（重跑不重复建边）
                continue;
            }

            await _store.CreateEdgeAsync(job.KnowledgeGraphId, relationTypeId, sourceNodeId, targetNodeId, cancellationToken);
            edgesCreated++;
            job.DoneRows++;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 新建 + 合并节点都重嵌（合并改变了描述）
        if (touchedNodeIds.Count > 0)
        {
            await KgEmbeddingDeltaPublisher.PublishNodesUpsertAsync(_messagePublisher, _logger, job.KnowledgeGraphId, touchedNodeIds);
        }

        return new ImportKnowledgeGraphFromFileResponse
        {
            NodesCreated = nodesCreated,
            NodesMerged = nodesMerged,
            EdgesCreated = edgesCreated,
            SkippedNodes = skippedNodes,
            SkippedEdges = skippedEdges,
            ContentLength = contentLength,
            Truncated = truncated,
        };
    }

    private async Task TransitionAsync(KgImportJobEntity job, string status, CancellationToken cancellationToken, string? resultJson = null)
    {
        job.Status = status;
        if (resultJson != null)
        {
            job.ResultJson = resultJson;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ThrowIfCancelledAsync(KgImportJobEntity job, CancellationToken cancellationToken)
    {
        await _databaseContext.Entry(job).ReloadAsync(cancellationToken);
        if (job.Status == KnowledgeGraphImportJobStatus.Cancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task MarkFailedAsync(KgImportJobEntity job, string message, int statusCode)
    {
        job.Status = KnowledgeGraphImportJobStatus.Failed;
        job.ErrorJson = JsonSerializer.Serialize(new { message, statusCode });
        await _databaseContext.SaveChangesAsync();
    }

    private async Task MarkCancelledAsync(KgImportJobEntity job)
    {
        if (job.Status != KnowledgeGraphImportJobStatus.Done)
        {
            job.Status = KnowledgeGraphImportJobStatus.Cancelled;
            await _databaseContext.SaveChangesAsync();
        }
    }

    private static string Serialize(ImportKnowledgeGraphFromFileResponse result)
        => JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    private string BuildPrompt(IReadOnlyList<KnowledgeGraphEntityTypeEntity> entityTypes, IReadOnlyList<KnowledgeGraphRelationTypeEntity> relationTypes, string content)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是知识图谱构建助手。请从文本中抽取符合下述模型的实体与关系，只输出 JSON，不要输出任何其他内容。");
        builder.AppendLine("JSON 格式：{\"nodes\":[{\"name\":\"实体名称\",\"entityType\":\"实体类型名\",\"description\":\"简述\",\"properties\":{\"属性名\":\"值\"}}],\"edges\":[{\"source\":\"起点实体名\",\"target\":\"终点实体名\",\"relationType\":\"关系类型名\"}]}");
        builder.AppendLine("要求：实体类型与关系类型必须从清单中选择，不得创造新类型；关系起止实体类型必须满足约束；实体名称使用文本中的原名；属性值取自文本，缺失的属性不要输出；最多抽取 100 个实体与 200 条关系。");
        builder.AppendLine("实体类型清单：");
        foreach (var entityType in entityTypes)
        {
            var defs = KnowledgeGraphPropertyJson.ParseDefinitions(entityType.Properties);
            var props = defs.Count > 0
                ? "；属性：" + string.Join("、", defs.Select(d => $"{d.Name}({d.Type}{(d.Required ? ",必填" : string.Empty)})"))
                : string.Empty;
            builder.AppendLine($"- {entityType.Name}{props}");
        }

        builder.AppendLine("关系类型清单（起点类型 → 终点类型）：");
        var typeName = entityTypes.ToDictionary(x => x.Id, x => x.Name);
        foreach (var relationType in relationTypes)
        {
            var source = relationType.SourceTypeId != null && typeName.TryGetValue(relationType.SourceTypeId.Value, out var sn) ? sn : "任意";
            var target = relationType.TargetTypeId != null && typeName.TryGetValue(relationType.TargetTypeId.Value, out var tn) ? tn : "任意";
            builder.AppendLine($"- {relationType.Name}: {source} → {target}");
        }

        builder.AppendLine("文本：");
        builder.Append(content);
        return builder.ToString();
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveChatModelAsync(Guid? modelId, CancellationToken cancellationToken)
    {
        if (modelId == null)
        {
            throw new BusinessException("未指定导入模型.") { StatusCode = 400 };
        }

        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.m.IsPublic) ?? candidates.FirstOrDefault();
        if (model == null)
        {
            throw new BusinessException("AI 对话模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.m.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是对话模型，无法用于 AI 导入.") { StatusCode = 400 };
        }

        return (model.m, model.c);
    }
}
