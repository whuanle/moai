using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 设置插件头像；仅管理员可操作；objectKey 需为已完成上传并登记的文件.
/// </summary>
public class UpdatePluginAvatarCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdatePluginAvatarCommand>
{
    /// <summary>
    /// 插件记录 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <summary>
    /// 头像文件的 ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdatePluginAvatarCommand> validate)
    {
        // PluginId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.");
    }
}
