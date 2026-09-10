namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 内置知识图谱模板目录（只读）.
/// </summary>
public static class KnowledgeGraphTemplates
{
    /// <summary>
    /// 空白模板 key.
    /// </summary>
    public const string BlankKey = "blank";

    /// <summary>
    /// 全部模板.
    /// </summary>
    public static readonly IReadOnlyList<KnowledgeGraphTemplate> All = new List<KnowledgeGraphTemplate>
    {
        new KnowledgeGraphTemplate
        {
            Key = BlankKey,
            Name = "空白 / 自定义",
            Description = "不预置类型，自行定义实体与关系",
        },
        new KnowledgeGraphTemplate
        {
            Key = "ops",
            Name = "运维服务",
            Description = "服务、人员、项目，维护与依赖",
            EntityTypes = new[] { "服务", "人员", "项目" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "维护", SourceType = "人员", TargetType = "服务" },
                new KnowledgeGraphTemplateRelation { Name = "依赖", SourceType = "项目", TargetType = "服务" },
            },
        },
        new KnowledgeGraphTemplate
        {
            Key = "org",
            Name = "组织人脉",
            Description = "人员、部门、公司，任职与隶属",
            EntityTypes = new[] { "人员", "部门", "公司" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "任职", SourceType = "人员", TargetType = "部门" },
                new KnowledgeGraphTemplateRelation { Name = "隶属", SourceType = "部门", TargetType = "公司" },
            },
        },
        new KnowledgeGraphTemplate
        {
            Key = "event",
            Name = "事件脉络",
            Description = "事件、时间、人物，参与与先后",
            EntityTypes = new[] { "事件", "时间", "人物" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "参与", SourceType = "人物", TargetType = "事件" },
                new KnowledgeGraphTemplateRelation { Name = "先后", SourceType = "事件", TargetType = "事件" },
            },
        },
    };

    /// <summary>
    /// 按 key 查找模板.
    /// </summary>
    /// <param name="key">模板 key.</param>
    /// <returns>模板或 null.</returns>
    public static KnowledgeGraphTemplate? Find(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(x => x.Key == key);
}
