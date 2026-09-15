using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词；个人提示词仅创建人可删，团队提示词需要团队 Admin 及以上角色；删除时同步移除该提示词待审核的上架申请.
/// </summary>
public class DeletePromptCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<DeletePromptCommand>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeletePromptCommand> validate)
    {
        validate.RuleFor(x => x.PromptId).GreaterThan(0).WithMessage("提示词 id 不正确.");
    }
}
