using FluentValidation;
using MediatR;
using MoAI.Variable.Commands.Responses;
using MoAI.Variable.Queries.Responses;

namespace MoAI.Variable.Commands;

/// <summary>
/// 对文本执行 <c>${key}</c> 变量替换（含私密变量解密），仅团队 Admin 及以上可调用；
/// 插件运行时应使用服务端内部的 <see cref="Services.IVariableService"/>，避免将私密值回传给成员.
/// </summary>
public class SubstituteVariableCommand : IRequest<SubstituteVariableCommandResponse>, IModelValidator<SubstituteVariableCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 待替换的文本.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SubstituteVariableCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Content).NotEmpty().WithMessage("替换内容不能为空.");
    }
}
