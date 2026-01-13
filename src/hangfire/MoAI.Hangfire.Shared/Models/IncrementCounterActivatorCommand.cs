using FluentValidation;
using Maomi;
using MediatR;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 激活计数器.
/// </summary>
public class IncrementCounterActivatorCommand : IRequest<int>, IModelValidator<IncrementCounterActivatorCommand>
{
    /// <summary>
    /// 名称，分类，相同名称的对象放在一起存储.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 实例 id 标识.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 增加的数量，默认 1.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<IncrementCounterActivatorCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空");
        validate.RuleFor(x => x.Id).NotEmpty().WithMessage("实例 id 不能为空");
        validate.RuleFor(x => x.Count).GreaterThan(0).WithMessage("增加的数量必须大于 0");
    }
}
