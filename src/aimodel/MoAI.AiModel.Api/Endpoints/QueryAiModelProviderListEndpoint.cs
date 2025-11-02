using FastEndpoints;
using MediatR;
using MoAI.AiModel.Queries;
using MoAI.AiModel.Queries.Respones;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Endpoints;

/// <summary>
/// 按维度查询不同分类的模型的数量，管理员访问.
/// </summary>
[HttpGet($"{ApiPrefix.AdminPrefix}/providerlist")]
public class QueryAiModelProviderListEndpoint : EndpointWithoutRequest<QueryAiModelProviderListResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiModelProviderListEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryAiModelProviderListEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<QueryAiModelProviderListResponse> ExecuteAsync(CancellationToken ct)
    {
        // 如果用户不是管理员，只能查到公开的模型或个人模型.
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand
        {
            ContextUserId = _userContext.UserId
        });

        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }

        return await _mediator.Send(new QueryAiModelProviderListCommand(), ct);
    }
}
