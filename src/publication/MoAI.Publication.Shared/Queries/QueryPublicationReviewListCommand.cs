using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Publication.Queries.Responses;

namespace MoAI.Publication.Queries;

/// <summary>
/// 查询上架审核列表（全平台），仅系统管理员可访问.
/// </summary>
public class QueryPublicationReviewListCommand : IRequest<QueryPublicationReviewListResponse>, IModelValidator<QueryPublicationReviewListCommand>
{
    /// <summary>
    /// 按审核状态过滤，为空查全部.
    /// </summary>
    public PublicationState? State { get; init; }

    /// <summary>
    /// 按资源类型过滤，为空查全部.
    /// </summary>
    public PublicationResourceType? ResourceType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPublicationReviewListCommand> validate)
    {
        validate.RuleFor(x => x.State).IsInEnum().WithMessage("审核状态不正确.");
        validate.RuleFor(x => x.ResourceType).IsInEnum().WithMessage("资源类型不正确.");
    }
}
