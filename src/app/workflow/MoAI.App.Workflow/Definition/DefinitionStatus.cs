namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 流程定义状态枚举.
/// </summary>
public enum DefinitionStatus
{
    /// <summary>
    /// 草稿 - 前端设计器保存的中间状态，可继续编辑.
    /// </summary>
    Draft,

    /// <summary>
    /// 已发布 - 可被创建流程实例执行.
    /// </summary>
    Published,
}
