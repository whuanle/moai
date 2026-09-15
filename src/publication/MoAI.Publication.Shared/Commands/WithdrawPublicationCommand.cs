using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Publication.Commands;

/// <summary>
/// 撤回上架申请，仅待审核状态可撤回；需要资源所属团队的 Admin 及以上角色.
/// </summary>
public class WithdrawPublicationCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<WithdrawPublicationCommand>
{
    /// <summary>
    /// 上架审核记录 id.
    /// </summary>
    public long PublicationId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<WithdrawPublicationCommand> validate)
    {
        validate.RuleFor(x => x.PublicationId).GreaterThan(0).WithMessage("上架审核记录 id 不正确.");
    }
}
