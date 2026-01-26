using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Chat.Manager.Commands;

/// <summary>
/// 修改应用配置.
/// </summary>
public class UpdateChatAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateChatAppCommand>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 公开到团队外使用.
    /// </summary>
    public bool IsPublic { get; init; }

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

    /// <summary>
    /// 是否开启授权验证.
    /// </summary>
    public bool IsAuth { get; init; }

    /// <summary>
    /// 分类id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateChatAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用id不能为空.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空.")
            .MaximumLength(20).WithMessage("应用名称长度不能超过100个字符.");

        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述长度不能超过500个字符.");

        validate.RuleFor(x => x.ModelId)
            .GreaterThan(0).WithMessage("模型id不能为空.");

        validate.RuleFor(x => x.Prompt)
            .MaximumLength(3000).WithMessage("提示词长度不能超过3000个字符.");
    }
}
