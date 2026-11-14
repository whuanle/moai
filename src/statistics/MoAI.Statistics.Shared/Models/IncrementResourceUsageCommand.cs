using FluentValidation;
using Maomi;
using MediatR;

namespace MoAI.Statistics.Services;

/// <summary>
/// 累加资源使用计数：写入 Redis hash counter:{Name}，由统计模块定期批量落库（支持正增负减）.
/// </summary>
public class IncrementResourceUsageCommand : IRequest, IModelValidator<IncrementResourceUsageCommand>
{
    /// <summary>
    /// 资源类型名，相同名称的计数放同一个 hash（见 <see cref="ResourceUsageNames"/>）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 批量增加计数器，key 是资源 id 文本，value 是增量（正增负减，不可为 0）.
    /// </summary>
    public IReadOnlyDictionary<string, long> Counters { get; init; } = new Dictionary<string, long>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<IncrementResourceUsageCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空");
        validate.RuleFor(x => x.Counters)
            .Must(x => x.Values.All(value => value != 0))
            .WithMessage("计数增量不能为 0");
    }
}
