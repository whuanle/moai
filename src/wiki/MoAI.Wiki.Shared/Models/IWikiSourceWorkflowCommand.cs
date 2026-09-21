namespace MoAI.Wiki.Models;

/// <summary>
/// 携带外部源工作流配置的命令，用于复用创建/更新外部源的工作流校验规则.
/// </summary>
public interface IWikiSourceWorkflowCommand
{
    /// <summary>
    /// 外部源工作流配置（切割/元数据/向量化三步），为空表示回退知识库默认工作流.
    /// </summary>
    WikiWorkflowConfig? Workflow { get; }
}
