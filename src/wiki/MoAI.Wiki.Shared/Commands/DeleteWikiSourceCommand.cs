using System;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 删除知识库外部源（软删除），需要团队 Admin 及以上角色.
/// 同时解除飞书渠道绑定并移除定时同步任务；已同步进知识库的文档保留.
/// </summary>
public class DeleteWikiSourceCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiSourceCommand>, IUserIdContext
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 外部源 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid SourceId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteWikiSourceCommand> validate)
    {
        // WikiId/SourceId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}
