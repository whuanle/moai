using System.Text.Json.Serialization;
using FluentValidation;

namespace MoAI.KnowledgeGraph.External;

/// <summary>
/// 结构化导入载荷（外部 /import 与内部 /import-json 共用）：类型名引用可自动创建、节点业务 key 幂等 upsert、边端点按引用解析、逐条报告.
/// </summary>
public class KnowledgeGraphImportPayload
{
    /// <summary>
    /// 写入模式：upsert（默认，幂等重导）或 create（一律新建）.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = ImportExternalGraphDataCommand.ModeUpsert;

    /// <summary>
    /// 实体/关系类型名称不存在时是否自动创建.
    /// </summary>
    [JsonPropertyName("autoCreateTypes")]
    public bool AutoCreateTypes { get; init; }

    /// <summary>
    /// 仅校验不落库（预测结果）.
    /// </summary>
    [JsonPropertyName("validateOnly")]
    public bool ValidateOnly { get; init; }

    /// <summary>
    /// 是否做疑似重复实体检测（向量相似度，需图谱已配置向量化模型）；默认开启，检测失败自动跳过不影响导入.
    /// </summary>
    [JsonPropertyName("detectDuplicates")]
    public bool DetectDuplicates { get; init; } = true;

    /// <summary>
    /// 节点列表.
    /// </summary>
    [JsonPropertyName("nodes")]
    public IReadOnlyList<ExternalImportNodeItem> Nodes { get; init; } = new List<ExternalImportNodeItem>();

    /// <summary>
    /// 边列表.
    /// </summary>
    [JsonPropertyName("edges")]
    public IReadOnlyList<ExternalImportEdgeItem> Edges { get; init; } = new List<ExternalImportEdgeItem>();
}

/// <summary>
/// <see cref="KnowledgeGraphImportPayload"/> 校验器：外部命令 MVC 校验与导入服务入口（JSON 反序列化不经 MVC）共用同一规则集，修改须两处同步语义.
/// </summary>
public sealed class KnowledgeGraphImportPayloadValidator : AbstractValidator<KnowledgeGraphImportPayload>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphImportPayloadValidator"/> class.
    /// </summary>
    public KnowledgeGraphImportPayloadValidator()
    {
        RuleFor(x => x.Mode).Must(x => x == ImportExternalGraphDataCommand.ModeUpsert || x == ImportExternalGraphDataCommand.ModeCreate).WithMessage("mode 仅支持 upsert 或 create.");
        RuleFor(x => x.Nodes).Must(x => x.Count <= ImportExternalGraphDataCommand.MaxNodes).WithMessage($"单次导入节点最多 {ImportExternalGraphDataCommand.MaxNodes} 条.");
        RuleFor(x => x.Edges).Must(x => x.Count <= ImportExternalGraphDataCommand.MaxEdges).WithMessage($"单次导入边最多 {ImportExternalGraphDataCommand.MaxEdges} 条.");
        RuleFor(x => new { x.Nodes, x.Edges }).Must(x => x.Nodes.Count + x.Edges.Count > 0).WithMessage("nodes 与 edges 不能同时为空.");

        RuleForEach(x => x.Nodes).ChildRules(item =>
        {
            item.RuleFor(i => i.Key).MaximumLength(200).WithMessage("key 最长 200 个字符.");
            item.RuleFor(i => i.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
            item.RuleFor(i => i.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
            item.RuleFor(i => i.EntityTypeId).Must(x => x == null || x > 0).WithMessage("实体类型 id 不正确.");
            item.RuleFor(i => i.EntityTypeName).MaximumLength(50).WithMessage("实体类型名称最长 50 个字符.");
            item.RuleFor(i => i).Must(i => i.EntityTypeId != null || !string.IsNullOrWhiteSpace(i.EntityTypeName)).WithMessage("entityTypeId 与 entityTypeName 必填其一.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Count <= 50).WithMessage("属性最多 50 个.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 100)).WithMessage("属性名不能为空且最长 100 个字符.");
            item.RuleFor(i => i.Properties).Must(x => x == null || x.Values.All(v => v == null || v.Length <= 2000)).WithMessage("属性值最长 2000 个字符.");
        });

        RuleForEach(x => x.Edges).ChildRules(item =>
        {
            item.RuleFor(i => i.RelationTypeId).Must(x => x == null || x > 0).WithMessage("关系类型 id 不正确.");
            item.RuleFor(i => i.RelationTypeName).MaximumLength(50).WithMessage("关系类型名称最长 50 个字符.");
            item.RuleFor(i => i).Must(i => i.RelationTypeId != null || !string.IsNullOrWhiteSpace(i.RelationTypeName)).WithMessage("relationTypeId 与 relationTypeName 必填其一.");
            item.RuleFor(i => i.Source).NotNull().WithMessage("source 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("source 必须提供 nodeId、key、name 之一.");
            item.RuleFor(i => i.Target).NotNull().WithMessage("target 不能为空.").Must(ExternalImportNodeRef.HasAnyRef).WithMessage("target 必须提供 nodeId、key、name 之一.");
        });
    }

    /// <summary>
    /// 校验载荷，非法时抛 400（汇总全部错误消息）.
    /// </summary>
    /// <param name="payload">导入载荷.</param>
    public static void ValidateOrThrow(KnowledgeGraphImportPayload payload)
    {
        var result = new KnowledgeGraphImportPayloadValidator().Validate(payload);
        if (result.IsValid)
        {
            return;
        }

        var messages = string.Join('；', result.Errors.Select(x => x.ErrorMessage).Distinct());
        throw new Infra.Exceptions.BusinessException(messages) { StatusCode = 400 };
    }
}
