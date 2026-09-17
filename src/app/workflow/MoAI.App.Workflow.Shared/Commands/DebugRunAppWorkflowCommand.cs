using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Workflow.Commands;

/// <summary>
/// 调试执行流程应用：隐式保存草稿（可选）→ 校验 → 同步执行到终态，返回每个节点的执行状态.
/// 需要团队 Admin 及以上角色.
/// </summary>
public class DebugRunAppWorkflowCommand : IRequest<DebugRunAppWorkflowResponse>, IUserIdContext, IModelValidator<DebugRunAppWorkflowCommand>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 启动参数 JSON 对象文本（开始节点的 run 输入），空为 '{}'；键需覆盖开始节点声明的必需启动参数.
    /// </summary>
    public string InputJson { get; init; } = "{}";

    /// <summary>
    /// 随调试一起保存的流程定义 JSON；为空则使用已保存的草稿.
    /// </summary>
    public string? Definition { get; init; }

    /// <summary>
    /// 全局变量实际值 JSON 对象文本（键为变量名），未提供的变量使用定义默认值；空为 '{}'.
    /// </summary>
    public string SystemJson { get; init; } = "{}";

    /// <summary>
    /// 随调试一起保存的编辑器画布原始 JSON；为空则保留已保存内容.
    /// </summary>
    public string? EditorData { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DebugRunAppWorkflowCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.InputJson).NotEmpty().WithMessage("启动参数不能为空.");
    }
}
