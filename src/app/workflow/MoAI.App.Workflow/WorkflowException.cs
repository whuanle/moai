namespace MoAI.App.Workflow;

/// <summary>
/// 工作流引擎基础异常.
/// </summary>
public class WorkflowException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowException"/> class.
    /// </summary>
    public WorkflowException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowException"/> class.
    /// </summary>
    public WorkflowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 工作流定义验证异常，包含全部验证错误.
/// </summary>
public class WorkflowValidationException : WorkflowException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowValidationException"/> class.
    /// </summary>
    public WorkflowValidationException(IReadOnlyList<string> errors)
        : base($"工作流定义验证失败：{string.Join("；", errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// 验证错误列表.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
