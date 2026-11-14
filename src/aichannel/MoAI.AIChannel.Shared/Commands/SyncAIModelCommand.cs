using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 从供应商拉取并同步模型列表（后端负责获取模型并匹配内置 models.json）.
/// </summary>
public class SyncAIModelCommand : IRequest<SyncAIModelCommandResponse>, IModelValidator<SyncAIModelCommand>
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// 所属团队 id，0=系统渠道；由 Controller 按路由回填，请求体无需传递.
    /// </summary>
    public int TeamId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SyncAIModelCommand> validate)
    {
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.");
    }
}
