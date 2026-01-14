using FluentValidation;
using Maomi;
using MediatR;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 激活计数器.
/// </summary>
public class IncrementCounterActivatorCommand : IRequest, IModelValidator<IncrementCounterActivatorCommand>
{
    /// <summary>
    /// 名称，分类，相同名称的对象放在一起存储.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 批量增加计数器，key 是 id，value 是数量.
    /// </summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = new Dictionary<string, int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<IncrementCounterActivatorCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空");
    }
}
