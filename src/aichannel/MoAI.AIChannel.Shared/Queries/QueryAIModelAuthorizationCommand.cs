using System;
using FluentValidation;
using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询模型的团队授权与额度设置：公开模型返回全局额度，私有模型返回授权团队及各自额度.
/// </summary>
public class QueryAIModelAuthorizationCommand : IRequest<QueryAIModelAuthorizationCommandResponse>, IModelValidator<QueryAIModelAuthorizationCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAIModelAuthorizationCommand> validate)
    {
        // ModelId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验.
    }
}
