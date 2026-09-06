using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 仅更新模型的公私有可见性，不触碰模型元数据：true=所有团队可用；false=私有，仅授权团队可用.
/// </summary>
public class UpdateAIModelVisibilityCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIModelVisibilityCommand>
{
    /// <summary>
    /// 模型 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIModelVisibilityCommand> validate)
    {
        // ModelId 由 Controller 从路由参数回填，IsPublic 为布尔值无需校验.
    }
}
