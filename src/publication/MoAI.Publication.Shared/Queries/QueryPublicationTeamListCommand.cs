using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Publication.Queries.Responses;

namespace MoAI.Publication.Queries;

/// <summary>
/// 查询团队的上架审核列表，团队成员可访问（用于团队侧查看申请/审核状态）.
/// </summary>
public class QueryPublicationTeamListCommand : IRequest<QueryPublicationReviewListResponse>, IUserIdContext, IModelValidator<QueryPublicationTeamListCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 按审核状态过滤，为空查全部.
    /// </summary>
    public PublicationState? State { get; init; }

    /// <summary>
    /// 按资源类型过滤，为空查全部.
    /// </summary>
    public PublicationResourceType? ResourceType { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPublicationTeamListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.State).IsInEnum().WithMessage("审核状态不正确.");
        validate.RuleFor(x => x.ResourceType).IsInEnum().WithMessage("资源类型不正确.");
    }
}
