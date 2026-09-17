using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 内置节点类型常量.
/// 节点类型使用字符串而非枚举，方便后续按定义扩展新的节点类型（自定义插件节点等）.
/// </summary>
public static class NodeTypes
{
    /// <summary>
    /// 开始节点 - 工作流入口，将启动参数作为输出.
    /// </summary>
    public const string Start = "start";

    /// <summary>
    /// 结束节点 - 工作流终点，收集最终输出.
    /// </summary>
    public const string End = "end";

    /// <summary>
    /// 插件节点 - 调用 <see cref="Nodes.IWorkflowPluginInvoker"/> 执行注册的插件.
    /// </summary>
    public const string Plugin = "plugin";

    /// <summary>
    /// AI 对话节点 - 调用 <see cref="Nodes.IAiChatClient"/> 与模型对话.
    /// </summary>
    public const string AiChat = "aiChat";

    /// <summary>
    /// 条件节点 - 根据布尔结果路由到不同分支.
    /// </summary>
    public const string Condition = "condition";

    /// <summary>
    /// JavaScript 节点 - 使用 Jint 执行 JS 脚本转换数据.
    /// </summary>
    public const string JavaScript = "javascript";

    /// <summary>
    /// 多条件节点 - 按顺序评估多个条件，走第一个命中的分支（if-else 语义）；全部未命中走 else 分支.
    /// </summary>
    public const string Switch = "switch";
}
