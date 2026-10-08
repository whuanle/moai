namespace MoAI.AI;

/// <summary>
/// 前端展示工具契约：ui_ 前缀工具由前端 AG-UI 消费端识别并渲染（侧边栏文档/代码/图表），
/// 后端只注册工具定义并返回固定桩结果（不执行任何业务），工具名与参数契约须与前端 chat/uiTools.ts 保持同步.
/// </summary>
public static class FrontendToolContract
{
    /// <summary>
    /// 对话 SSE 请求上携带前端展示工具开关的请求头（1=启用），未携带按关闭处理；
    /// 嵌入组件/外部渠道未适配侧边栏前不下发，后端不注册相关工具.
    /// </summary>
    public const string HeaderName = "X-Moai-Ui-Tools";

    /// <summary>
    /// 前端展示工具名统一前缀，前端据此识别并转交侧边栏渲染.
    /// </summary>
    public const string ToolPrefix = "ui_";

    /// <summary>
    /// 长文档/报告工具：markdown 正文折叠为消息卡片，点击在用户侧边栏打开.
    /// </summary>
    public const string ShowDocumentToolName = "ui_show_document";

    /// <summary>
    /// 大块代码工具：代码折叠为消息卡片，点击在用户侧边栏打开.
    /// </summary>
    public const string ShowCodeToolName = "ui_show_code";

    /// <summary>
    /// 数据图表工具：ECharts option JSON 折叠为消息卡片，点击在用户侧边栏渲染图表.
    /// </summary>
    public const string ShowChartToolName = "ui_show_chart";

    /// <summary>
    /// 桩执行结果：告知模型内容已交由用户界面展示，无需等待用户操作.
    /// </summary>
    public const string StubResultJson = """{"success":true,"data":{"rendered":true,"surface":"client"}}""";

    /// <summary>
    /// 判断工具名是否为前端展示工具.
    /// </summary>
    /// <param name="name">工具名.</param>
    /// <returns>是前端展示工具返回 true.</returns>
    public static bool IsFrontendTool(string? name)
        => name != null && name.StartsWith(ToolPrefix, StringComparison.Ordinal);
}
