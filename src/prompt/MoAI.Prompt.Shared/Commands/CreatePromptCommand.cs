using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 创建提示词；TeamId 为 0 时创建个人提示词仅本人可见可用，TeamId 大于 0 时创建团队提示词，需要团队 Admin 及以上角色.
/// </summary>
public class CreatePromptCommand : IRequest<SimpleInt>, IUserIdContext, IModelValidator<CreatePromptCommand>
{
    /// <summary>
    /// 所属团队 id，0 表示个人提示词.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 提示词内容.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 分类 id，0 表示未分类，分类类型必须为 prompt.
    /// </summary>
    public int PromptClassId { get; init; }

    /// <summary>
    /// 头像地址，可为空.
    /// </summary>
    public string? AvatarPath { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreatePromptCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThanOrEqualTo(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空.").MaximumLength(20).WithMessage("名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.Content).NotEmpty().WithMessage("提示词内容不能为空.").MaximumLength(10000).WithMessage("提示词内容最长 10000 个字符.");
        validate.RuleFor(x => x.PromptClassId).GreaterThanOrEqualTo(0).WithMessage("分类 id 不正确.");
        validate.RuleFor(x => x.AvatarPath).MaximumLength(255).WithMessage("头像地址最长 255 个字符.");
    }
}
