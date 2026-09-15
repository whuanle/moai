using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 设置知识图谱头像，仅 Owner/Admin 可操作；objectKey 需为已完成上传并登记的文件.
/// </summary>
public class UpdateKnowledgeGraphAvatarCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphAvatarCommand>
{
    /// <summary>
    /// 图谱 id，由 Controller 从路由参数回填.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 头像文件的 ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphAvatarCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("头像文件不能为空.");
    }
}
