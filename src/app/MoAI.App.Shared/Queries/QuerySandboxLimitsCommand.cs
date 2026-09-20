using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;
/// <summary>
/// 查询沙箱资源上限（每个应用可配置的存活时间 / CPU / 内存最大值），登录用户即可访问，供应用配置页约束取值范围.
/// </summary>
public class QuerySandboxLimitsCommand : IRequest<QuerySandboxLimitsCommandResponse>, IModelValidator<QuerySandboxLimitsCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySandboxLimitsCommand> validate)
    {
    }
}
