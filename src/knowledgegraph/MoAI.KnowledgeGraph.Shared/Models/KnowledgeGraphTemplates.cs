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
            Key = "logistics",
            Name = "物流运输",
            Description = "港口地点、航段距离与运价，按航段串联地点即可计算 A 到 C 的最短距离与运输价格",
            EntityTypes = new[]
            {
                new KnowledgeGraphTemplateEntityType
                {
                    Name = "港口",
                    Description = "起运或到达的港口、地点",
                    Properties = new[]
                    {
                        new KnowledgeGraphEntityTypeProperty { Name = "港口代码", Type = KnowledgeGraphEntityTypeProperty.TypeString, Description = "如 CNSHA 等 UN/LOCODE 编码" },
                        new KnowledgeGraphEntityTypeProperty { Name = "国家区域", Type = KnowledgeGraphEntityTypeProperty.TypeString, Description = "所属国家或区域" },
                    },
                },
                new KnowledgeGraphTemplateEntityType
                {
                    Name = "航段",
                    Description = "两个地点之间的直达运输线路，是距离与运价计算的最小单元",
                    Properties = new[]
                    {
                        new KnowledgeGraphEntityTypeProperty { Name = "运输方式", Type = KnowledgeGraphEntityTypeProperty.TypeString, Description = "海运 / 铁路 / 公路 / 航空" },
                        new KnowledgeGraphEntityTypeProperty { Name = "距离公里", Type = KnowledgeGraphEntityTypeProperty.TypeNumber, Required = true, Description = "该航段单程距离（公里）" },
                        new KnowledgeGraphEntityTypeProperty { Name = "运输价格", Type = KnowledgeGraphEntityTypeProperty.TypeNumber, Required = true, Description = "该航段单程运输价格（元）" },
                        new KnowledgeGraphEntityTypeProperty { Name = "时效天", Type = KnowledgeGraphEntityTypeProperty.TypeNumber, Description = "该航段单程运输时间（天）" },
                    },
                },
                new KnowledgeGraphTemplateEntityType
                {
                    Name = "承运商",
                    Description = "提供航段运输服务的承运公司",
                    Properties = new[]
                    {
                        new KnowledgeGraphEntityTypeProperty { Name = "联系方式", Type = KnowledgeGraphEntityTypeProperty.TypeString, Description = "联系电话或邮箱" },
                    },
                },
            },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "出发", SourceType = "航段", TargetType = "港口" },
                new KnowledgeGraphTemplateRelation { Name = "抵达", SourceType = "航段", TargetType = "港口" },
                new KnowledgeGraphTemplateRelation { Name = "承运", SourceType = "承运商", TargetType = "航段" },
            },
            Nodes = new[]
            {
                new KnowledgeGraphTemplateNode
                {
                    Key = "port-shanghai",
                    EntityTypeName = "港口",
                    Name = "上海",
                    Description = "华东主枢纽港",
                    Properties = new Dictionary<string, string> { ["港口代码"] = "CNSHA", ["国家区域"] = "中国" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "port-ningbo",
                    EntityTypeName = "港口",
                    Name = "宁波",
                    Description = "宁波舟山港",
                    Properties = new Dictionary<string, string> { ["港口代码"] = "CNNGB", ["国家区域"] = "中国" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "port-xiamen",
                    EntityTypeName = "港口",
                    Name = "厦门",
                    Description = "东南沿海港口",
                    Properties = new Dictionary<string, string> { ["港口代码"] = "CNXMN", ["国家区域"] = "中国" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "port-shenzhen",
                    EntityTypeName = "港口",
                    Name = "深圳",
                    Description = "华南主枢纽港",
                    Properties = new Dictionary<string, string> { ["港口代码"] = "CNSZX", ["国家区域"] = "中国" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "leg-sh-ningbo",
                    EntityTypeName = "航段",
                    Name = "上海 → 宁波",
                    Properties = new Dictionary<string, string> { ["运输方式"] = "海运", ["距离公里"] = "250", ["运输价格"] = "800", ["时效天"] = "1" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "leg-ningbo-xiamen",
                    EntityTypeName = "航段",
                    Name = "宁波 → 厦门",
                    Properties = new Dictionary<string, string> { ["运输方式"] = "海运", ["距离公里"] = "900", ["运输价格"] = "2600", ["时效天"] = "2" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "leg-xiamen-shenzhen",
                    EntityTypeName = "航段",
                    Name = "厦门 → 深圳",
                    Properties = new Dictionary<string, string> { ["运输方式"] = "海运", ["距离公里"] = "550", ["运输价格"] = "1600", ["时效天"] = "1" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "leg-ningbo-shenzhen",
                    EntityTypeName = "航段",
                    Name = "宁波 → 深圳",
                    Properties = new Dictionary<string, string> { ["运输方式"] = "海运", ["距离公里"] = "1300", ["运输价格"] = "3900", ["时效天"] = "2" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "leg-sh-shenzhen",
                    EntityTypeName = "航段",
                    Name = "上海 → 深圳（直达）",
                    Description = "示例：直达最短（1500 公里），但价格高于中转（4700 元）",
                    Properties = new Dictionary<string, string> { ["运输方式"] = "海运", ["距离公里"] = "1500", ["运输价格"] = "5200", ["时效天"] = "3" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "carrier-cosco",
                    EntityTypeName = "承运商",
                    Name = "中远海运",
                    Properties = new Dictionary<string, string> { ["联系方式"] = "95549" },
                },
                new KnowledgeGraphTemplateNode
                {
                    Key = "carrier-maersk",
                    EntityTypeName = "承运商",
                    Name = "马士基",
                    Properties = new Dictionary<string, string> { ["联系方式"] = "400-636-3636" },
                },
            },
            Edges = new[]
            {
                new KnowledgeGraphTemplateEdge { RelationName = "出发", SourceNodeKey = "leg-sh-ningbo", TargetNodeKey = "port-shanghai" },
                new KnowledgeGraphTemplateEdge { RelationName = "抵达", SourceNodeKey = "leg-sh-ningbo", TargetNodeKey = "port-ningbo" },
                new KnowledgeGraphTemplateEdge { RelationName = "承运", SourceNodeKey = "carrier-cosco", TargetNodeKey = "leg-sh-ningbo" },
                new KnowledgeGraphTemplateEdge { RelationName = "出发", SourceNodeKey = "leg-ningbo-xiamen", TargetNodeKey = "port-ningbo" },
                new KnowledgeGraphTemplateEdge { RelationName = "抵达", SourceNodeKey = "leg-ningbo-xiamen", TargetNodeKey = "port-xiamen" },
                new KnowledgeGraphTemplateEdge { RelationName = "承运", SourceNodeKey = "carrier-maersk", TargetNodeKey = "leg-ningbo-xiamen" },
                new KnowledgeGraphTemplateEdge { RelationName = "出发", SourceNodeKey = "leg-xiamen-shenzhen", TargetNodeKey = "port-xiamen" },
                new KnowledgeGraphTemplateEdge { RelationName = "抵达", SourceNodeKey = "leg-xiamen-shenzhen", TargetNodeKey = "port-shenzhen" },
                new KnowledgeGraphTemplateEdge { RelationName = "承运", SourceNodeKey = "carrier-cosco", TargetNodeKey = "leg-xiamen-shenzhen" },
                new KnowledgeGraphTemplateEdge { RelationName = "出发", SourceNodeKey = "leg-ningbo-shenzhen", TargetNodeKey = "port-ningbo" },
                new KnowledgeGraphTemplateEdge { RelationName = "抵达", SourceNodeKey = "leg-ningbo-shenzhen", TargetNodeKey = "port-shenzhen" },
                new KnowledgeGraphTemplateEdge { RelationName = "承运", SourceNodeKey = "carrier-cosco", TargetNodeKey = "leg-ningbo-shenzhen" },
                new KnowledgeGraphTemplateEdge { RelationName = "出发", SourceNodeKey = "leg-sh-shenzhen", TargetNodeKey = "port-shanghai" },
                new KnowledgeGraphTemplateEdge { RelationName = "抵达", SourceNodeKey = "leg-sh-shenzhen", TargetNodeKey = "port-shenzhen" },
                new KnowledgeGraphTemplateEdge { RelationName = "承运", SourceNodeKey = "carrier-maersk", TargetNodeKey = "leg-sh-shenzhen" },
            },
        },
        new KnowledgeGraphTemplate
        {
            Key = "org",
            Name = "组织人脉",
            Description = "人员、部门、公司，任职与隶属",
            EntityTypes = new[]
            {
                new KnowledgeGraphTemplateEntityType { Name = "人员" },
                new KnowledgeGraphTemplateEntityType { Name = "部门" },
                new KnowledgeGraphTemplateEntityType { Name = "公司" },
            },
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
            EntityTypes = new[]
            {
                new KnowledgeGraphTemplateEntityType { Name = "事件" },
                new KnowledgeGraphTemplateEntityType { Name = "时间" },
                new KnowledgeGraphTemplateEntityType { Name = "人物" },
            },
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
