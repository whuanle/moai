using MoAI.KnowledgeGraph.Services;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphImportParserTests
{
    [Fact]
    public void Parse_StandardJson_ReturnsDrafts()
    {
        var json = """
            {"nodes":[
                {"name":"上海港","entityType":"港口","description":"华东枢纽","properties":{"港口代码":"CNSHA","国家区域":"中国"}},
                {"name":"沪甬线","entityType":"航段","properties":{"距离公里":"250"}}
            ],
            "edges":[{"source":"沪甬线","target":"上海港","relationType":"出发"}]}
            """;

        var (nodes, edges) = KnowledgeGraphImportParser.Parse(json);

        Assert.Equal(2, nodes.Count);
        Assert.Equal("上海港", nodes[0].Name);
        Assert.Equal("港口", nodes[0].EntityType);
        Assert.Equal("CNSHA", nodes[0].Properties["港口代码"]);
        Assert.Single(edges);
        Assert.Equal("沪甬线", edges[0].Source);
        Assert.Equal("出发", edges[0].RelationType);
    }

    [Fact]
    public void Parse_FencedOutputWithNoise_Parses()
    {
        var text = """
            以下是抽取结果：
            ```json
            {"nodes":[{"name":"深圳","entity_type":"港口"}],"edges":[{"from":"深圳","to":"宁波","relation_type":"抵达"}]}
            ```
            希望对你有帮助。
            """;

        var (nodes, edges) = KnowledgeGraphImportParser.Parse(text);

        Assert.Single(nodes);
        Assert.Equal("深圳", nodes[0].Name);
        Assert.Equal("港口", nodes[0].EntityType);
        Assert.Single(edges);
        Assert.Equal("抵达", edges[0].RelationType);
    }

    [Fact]
    public void Parse_MissingRequiredFields_SkipsEntries()
    {
        var json = """
            {"nodes":[
                {"entityType":"港口"},
                {"name":"无类型实体"},
                {"name":"-ok-","entityType":"航段"}
            ],
            "edges":[{"source":"-ok-"},{"source":"x","target":"y","relationType":"出发"}]}
            """;

        var (nodes, edges) = KnowledgeGraphImportParser.Parse(json);

        Assert.Single(nodes);
        Assert.Equal("-ok-", nodes[0].Name);
        Assert.Single(edges);
    }

    [Fact]
    public void Parse_NonJson_ReturnsEmpty()
    {
        var (nodes, edges) = KnowledgeGraphImportParser.Parse("抱歉，我无法从该文本中抽取内容。");

        Assert.Empty(nodes);
        Assert.Empty(edges);
    }
}
