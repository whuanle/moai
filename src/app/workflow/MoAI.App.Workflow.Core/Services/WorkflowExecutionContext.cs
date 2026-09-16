using Maomi;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流执行上下文（Scoped）— 调试/执行处理器在调用引擎前写入团队与应用信息，
/// 存储与端口实现（同为 Scoped、同作用域）据此落库和解析团队资源.
/// 引擎接口本身不携带团队维度信息，由宿主通过此上下文补齐.
/// </summary>
[InjectOnScoped]
public class WorkflowExecutionContext
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 编排配置 id（app_workflow_config.id）.
    /// </summary>
    public Guid ConfigId { get; set; }

    /// <summary>
    /// 是否调试运行（设计器内发起）.
    /// </summary>
    public bool IsDebug { get; set; }
}
