using FluentValidation;
using MediatR;
using MoAI.App.Commands.Responses;
using MoAI.App.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 创建应用.
/// </summary>
public class CreateAppCommand : IUserIdContext, IRequest<CreateAppCommandResponse>, IModelValidator<CreateAppCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; init; }

    /// <summary>
    /// 应用类型.
    /// </summary>
    public AppType AppType { get; init; } = AppType.Common;

    /// <summary>
    /// 头像.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 模型id.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 知识库id列表.
    /// </summary>
    public IReadOnlyCollection<int> WikiIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 插件列表.
    /// </summary>
    public IReadOnlyCollection<string> Plugins { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空.")
            .MaximumLength(100).WithMessage("应用名称长度不能超过100个字符.");

        validate.RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("描述长度不能超过500个字符.");

        validate.RuleFor(x => x.AppType)
            .Must(x => x == AppType.Common).WithMessage("暂时只能创建普通应用.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型id不能为空.");

        validate.RuleFor(x => x.Prompt)
            .MaximumLength(3000).WithMessage("提示词长度不能超过3000个字符.");
    }
}
