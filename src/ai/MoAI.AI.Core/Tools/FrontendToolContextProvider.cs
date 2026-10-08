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
                (System.Text.Json.Nodes.JsonNode? title = null, System.Text.Json.Nodes.JsonNode? option = null) => Task.FromResult(FrontendToolContract.StubResultJson),
                name: FrontendToolContract.ShowChartToolName,
                description: "把数据可视化图表交给用户界面：图表会折叠为消息卡片并在用户侧边栏中渲染。参数：title=图表标题，option=ECharts option 配置（必须直接嵌套 JSON 对象，如 {\"xAxis\":{...},\"series\":[{\"type\":\"bar\",\"data\":[...]}]}；禁止把 option 序列化成字符串、禁止包含函数或表达式）。"),
        ];

        _instructions = """
## 前端展示工具（ui_ 前缀）
你可以调用 ui_show_document / ui_show_code / ui_show_chart，把大块产出内容交给用户界面侧边栏展示：
- 报告/方案/长总结 → ui_show_document（markdown 正文）
- 完整代码/脚本/配置 → ui_show_code
- 数据图表 → ui_show_chart（ECharts option，必须直接传 JSON 对象、不要序列化成字符串，禁止函数）
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
