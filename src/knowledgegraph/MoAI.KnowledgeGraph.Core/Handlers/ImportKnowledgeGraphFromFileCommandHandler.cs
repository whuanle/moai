using System.Text;
using System.Text.Json;
using Maomi.ToMarkdown;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Storage.Models;
using MoAI.Storage.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="ImportKnowledgeGraphFromFileCommand"/>
/// AI 导入文件生成图谱：Maomi.ToMarkdown 提取文本 → 对话模型按图谱现有模型抽取实体与关系 → 校验后写入图库.
/// </summary>
public class ImportKnowledgeGraphFromFileCommandHandler : IRequestHandler<ImportKnowledgeGraphFromFileCommand, ImportKnowledgeGraphFromFileResponse>
{
    /// <summary>
    /// 提取文本截断上限（发给模型的字符预算，防止超长文档撑爆上下文）.
    /// </summary>
    public const int MaxPromptChars = 12_000;

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;
    private readonly IStorageService _storageService;
    private readonly TextExtractionService _textExtractionService;
    private readonly IAiChatCompletionService _chatCompletionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportKnowledgeGraphFromFileCommandHandler"/> class.
    /// </summary>
    public ImportKnowledgeGraphFromFileCommandHandler(
        DatabaseContext databaseContext,
        IKnowledgeGraphAuthorizer authorizer,
        IKnowledgeGraphSettingsService settingsService,
        IKnowledgeGraphStore store,
        IStorageService storageService,
        TextExtractionService textExtractionService,
        IAiChatCompletionService chatCompletionService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _store = store;
        _storageService = storageService;
        _textExtractionService = textExtractionService;
        _chatCompletionService = chatCompletionService;
    }

    /// <inheritdoc/>
    public async Task<ImportKnowledgeGraphFromFileResponse> Handle(ImportKnowledgeGraphFromFileCommand request, CancellationToken cancellationToken)
    {
        // 导入即写节点/边：仅托管图 Admin+（接入图只读 409 由授权器拦截）
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.IsDeleted == 0)
            .OrderBy(x => x.Sort)
            .ToListAsync(cancellationToken);
        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId && x.IsDeleted == 0)
            .OrderBy(x => x.Sort)
            .ToListAsync(cancellationToken);

        if (entityTypes.Count == 0)
        {
            throw new BusinessException("该图谱还没有实体类型，请先在「模型」页定义模型后再导入.") { StatusCode = 409 };
        }

        var (model, channel) = await ResolveChatModelAsync(request.AiModelId, cancellationToken);

        // 1. 读取文件并用 Maomi.ToMarkdown 提取文本
        string markdown;
        var truncated = false;
        try
        {
            var file = await _storageService.ReadAsync(request.ObjectKey, cancellationToken);
            await using (file.FileStream)
            {
                markdown = await _textExtractionService.ExtractAsync(file.FileStream, request.FileName, cancellationToken);
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

        // 2. 组装抽取提示词并调用对话模型
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

        // 3. 解析抽取结果
        var (nodeDrafts, edgeDrafts) = KnowledgeGraphImportParser.Parse(output);
        if (nodeDrafts.Count == 0)
        {
            return new ImportKnowledgeGraphFromFileResponse
            {
                ContentLength = contentLength,
                Truncated = truncated,
                Message = "AI 未从文件中抽取到符合当前模型的实体，请检查模型是否匹配文件内容.",
            };
        }

        // 4. 类型映射与写入
        var typeNameToId = entityTypes.ToDictionary(x => x.Name, x => x.Id);
        var relationNameToEntity = relationTypes.ToDictionary(x => x.Name, x => x);
        var propertyDefs = entityTypes.ToDictionary(
            x => x.Name,
            x => KnowledgeGraphPropertyJson.ParseDefinitions(x.Properties));

        var nodeKeyToId = new Dictionary<string, string>(StringComparer.Ordinal);
        var nodesCreated = 0;
        var skippedNodes = 0;
        foreach (var draft in nodeDrafts)
        {
            if (nodeKeyToId.ContainsKey(draft.Name))
            {
                skippedNodes++;
                continue;
            }

            if (!typeNameToId.TryGetValue(draft.EntityType, out var entityTypeId))
            {
                skippedNodes++;
                continue;
            }

            // 属性仅保留模型已定义的键，避免 AI 编造属性名
            var allowed = propertyDefs[draft.EntityType];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var def in allowed)
            {
                if (draft.Properties.TryGetValue(def.Name, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    values[def.Name] = value;
                }
            }

            var record = await _store.CreateNodeAsync(
                request.KnowledgeGraphId,
                entityTypeId,
                draft.Name,
                draft.Description ?? string.Empty,
                KnowledgeGraphPropertyJson.WriteValues(values),
                cancellationToken);
            nodeKeyToId[draft.Name] = record.Id;
            nodesCreated++;
        }

        var edgesCreated = 0;
        var skippedEdges = 0;
        foreach (var draft in edgeDrafts)
        {
            if (!relationNameToEntity.TryGetValue(draft.RelationType, out var relationType)
                || !nodeKeyToId.TryGetValue(draft.Source, out var sourceNodeId)
                || !nodeKeyToId.TryGetValue(draft.Target, out var targetNodeId))
            {
                skippedEdges++;
                continue;
            }

            // 起止约束与建边接口同规则：约束为空表示任意
            var sourceEntityTypeId = await GetNodeEntityTypeIdAsync(request.KnowledgeGraphId, sourceNodeId, cancellationToken);
            var targetEntityTypeId = await GetNodeEntityTypeIdAsync(request.KnowledgeGraphId, targetNodeId, cancellationToken);
            if ((relationType.SourceTypeId != null && relationType.SourceTypeId != sourceEntityTypeId)
                || (relationType.TargetTypeId != null && relationType.TargetTypeId != targetEntityTypeId))
            {
                skippedEdges++;
                continue;
            }

            await _store.CreateEdgeAsync(request.KnowledgeGraphId, relationType.Id, sourceNodeId, targetNodeId, cancellationToken);
            edgesCreated++;
        }

        return new ImportKnowledgeGraphFromFileResponse
        {
            NodesCreated = nodesCreated,
            EdgesCreated = edgesCreated,
            SkippedNodes = skippedNodes,
            SkippedEdges = skippedEdges,
            ContentLength = contentLength,
            Truncated = truncated,
        };
    }

    private async Task<long> GetNodeEntityTypeIdAsync(long knowledgeGraphId, string nodeId, CancellationToken cancellationToken)
    {
        var node = await _store.GetNodeAsync(knowledgeGraphId, nodeId, cancellationToken);
        return node?.EntityTypeId ?? 0;
    }

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

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)> ResolveChatModelAsync(Guid modelId, CancellationToken cancellationToken)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            model = candidates.FirstOrDefault();
        }

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
