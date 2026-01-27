namespace MoAI.Workflow.Enums;

/// <summary>
/// 工作流节点类型枚举.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// 开始节点 - 工作流的入口点，初始化工作流上下文.
    /// </summary>
    Start,

    /// <summary>
    /// 结束节点 - 工作流的终点，返回最终输出.
    /// </summary>
    End,

    /// <summary>
    /// 插件节点 - 执行指定的插件功能.
    /// </summary>
    Plugin,

    /// <summary>
    /// 知识库节点 - 查询知识库并返回相关文档.
    /// </summary>
    Wiki,

    /// <summary>
    /// AI 对话节点 - 调用 AI 模型进行对话.
    /// </summary>
    AiChat,

    /// <summary>
    /// 条件节点 - 根据条件表达式评估结果路由到不同分支.
    /// </summary>
    Condition,

    /// <summary>
    /// 循环节点 - 迭代集合并为每个项目执行循环体.
    /// </summary>
    ForEach,

    /// <summary>
    /// 分支节点 - 并行执行多个分支并等待所有分支完成.
    /// </summary>
    Fork,

    /// <summary>
    /// JavaScript 节点 - 执行 JavaScript 代码并可访问工作流上下文.
    /// </summary>
    JavaScript,

    /// <summary>
    /// 数据处理节点 - 对输入数据执行转换操作（map、filter、aggregate）.
    /// </summary>
    DataProcess,
}
