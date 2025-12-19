using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 邀请或移除知识库协作成员，可以管理知识库文档等.
/// </summary>
public class InviteWikiUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<InviteWikiUserCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 如果是则邀请，否则是移除.
    /// </summary>
    public bool IsInvite { get; init; } = true;

    /// <summary>
    /// 用户名称.
    /// </summary>
    public IReadOnlyCollection<string> UserNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用户id.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<InviteWikiUserCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.UserNames)
            .NotEmpty().WithMessage("被邀请用户信息错误");
    }
}