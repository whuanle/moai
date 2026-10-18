using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 前端展示工具上下文提供者：把长文档/大块代码/数据图表注册为 ui_ 前缀的顶层直连工具
/// （不经 list_tools/call_tool 渐进披露，AG-UI 事件流以真实工具名推送到前端）。
/// 工具在后端不执行任何业务，桩执行直接返回固定结果，渲染由前端识别前缀后在侧边栏完成.
/// </summary>
public sealed class FrontendToolContextProvider : AIContextProvider
{
    private readonly AITool[] _tools;
    private readonly string _instructions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontendToolContextProvider"/> class.
    /// </summary>
    public FrontendToolContextProvider()
    {
        // 参数一律用 JsonNode 承接后忽略，且全部带默认值（可选参数）：部分模型不遵守 schema
        // （漏传可选字段/把字符串传成对象），强类型必填形参在编组时抛异常会中断整轮对话；
        // 内容消费端在前端，后端无需读取参数
        _tools =
        [
            AIFunctionFactory.Create(
                (System.Text.Json.Nodes.JsonNode? title = null, System.Text.Json.Nodes.JsonNode? content = null) => Task.FromResult(FrontendToolContract.StubResultJson),
                name: FrontendToolContract.ShowDocumentToolName,
                description: "把一份较长的文档/报告以 markdown 形式交给用户界面：内容会折叠为消息卡片并在用户侧边栏中完整展示。适用于报告、方案、总结等篇幅较长、不适合直接写在回复正文里的内容。参数：title=标题（简短），content=markdown 正文（完整内容，支持标题/列表/表格）。"),
            AIFunctionFactory.Create(
                (System.Text.Json.Nodes.JsonNode? title = null, System.Text.Json.Nodes.JsonNode? language = null, System.Text.Json.Nodes.JsonNode? code = null) => Task.FromResult(FrontendToolContract.StubResultJson),
                name: FrontendToolContract.ShowCodeToolName,
                description: "把一段完整的代码交给用户界面：代码会折叠为消息卡片并在用户侧边栏中打开。适用于脚本、配置文件、项目文件等大块代码产出。参数：title=标题，language=编程语言（如 python/sql/typescript，可省略），code=完整代码文本。"),
            AIFunctionFactory.Create(
                (System.Text.Json.Nodes.JsonNode? title = null, System.Text.Json.Nodes.JsonNode? chart = null) => Task.FromResult(FrontendToolContract.StubResultJson),
                name: FrontendToolContract.ShowChartToolName,
                description: """
                    把数据可视化图表交给用户界面：图表会折叠为消息卡片并在用户侧边栏中渲染。
                    参数：title=图表标题（简短，写结论更好，如「GMV 连续 9 个月正增长」），chart=图表配置（必须直接嵌套 JSON 对象，禁止序列化成字符串、禁止函数）。
                    chart.type 决定图表形态，data 的字段名是硬性契约（字段名错误会渲染成废图），按类型选型与给数：
                    - line/area（时序趋势）：data=[{"time":"2025-01","value":123,"group":"系列名(可选)"}]
                    - column/bar（类别对比、含正负值的环比/同比增长）：data=[{"category":"1月","value":123,"group":"系列名(可选)"}]，多系列再加 "group":true（分组）或 "stack":true（堆叠）
                    - pie（构成占比）：data=[{"category":"A","value":123}]
                    - dual-axes（量级+比率双指标，如 GMV 柱+增速线）：{"categories":["2025-01"],"series":[{"type":"column","data":[123],"axisYTitle":"GMV"},{"type":"line","data":[4.5],"axisYTitle":"增速%"}]}
                    - waterfall（增减分解）：data=[{"category":"项目","value":123,"isTotal":true(合计行,可选)}]
                    - scatter（相关性）：data=[{"x":10,"y":15,"group":"系列名(可选)"}]
                    - radar：data=[{"name":"维度","value":5}]；funnel/boxplot/violin 同 column 的 {"category","value"}；histogram 的 data 为数值数组（可加 "binNumber"）；venn 的 data=[{"sets":["A"],"value":20}]；sankey 的 data=[{"source":"A","target":"B","value":1}]；treemap 的 data=[{"name":"A","value":100,"children":[{"name":"A1","value":40}]}]；word-cloud 的 data=[{"text":"词","value":20}]
                    - liquid（单值进度）：{"percent":0.75}
                    - table：data 为对象数组，键即列名
                    - mindmap/organization-chart/indented-tree/fishbone-diagram：data={"name":"根","children":[{"name":"子"}]}
                    - network-graph/flow-diagram：data={"nodes":[{"name":"A"}],"edges":[{"source":"A","target":"B"}]}
                    笛卡尔类图表（line/area/column/bar/waterfall/scatter/histogram/boxplot/violin）可另加 axisXTitle/axisYTitle（带单位）。
                    """),
        ];

        _instructions = """
## 前端展示工具（ui_ 前缀）
你可以调用 ui_show_document / ui_show_code / ui_show_chart，把大块产出内容交给用户界面侧边栏展示：
- 报告/方案/长总结 → ui_show_document（markdown 正文）
- 完整代码/脚本/配置 → ui_show_code
- 数据图表 → ui_show_chart（图表配置 JSON 对象，不要序列化成字符串，禁止函数；各类型的 data 字段名必须严格按工具描述给出，字段名错误会渲染成废图）
规则：
1. 调用即视为内容已在用户界面展示（结果固定返回 rendered=true），不要等待用户操作，继续完成后续任务。
2. 调用后在回复正文里用一两句话概括产出了什么即可，不要在正文重复完整内容；简短内容（几行文字/代码）直接写在正文，不要调用这些工具。
3. 这些工具直接调用（不要通过 call_tool），同一轮可以多次调用不同工具。

""";
    }

    /// <inheritdoc/>
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask<AIContext>(new AIContext
        {
            Tools = _tools,
            Instructions = _instructions,
        });
    }

    /// <summary>
    /// 获取装配好的工具与指引（供单测校验工具名与桩结果）.
    /// </summary>
    internal Task<AIContext> BuildAIContextAsync() => Task.FromResult(new AIContext
    {
        Tools = _tools,
        Instructions = _instructions,
    });
}
