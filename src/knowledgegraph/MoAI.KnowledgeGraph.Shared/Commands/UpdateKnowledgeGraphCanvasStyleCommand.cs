using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 配置画布样式规则（管理员，B8）：按实体类型配置画布节点第二行 caption 显示什么——
/// 键=实体类型 id 字符串，值=属性名（节点带该属性时显示属性值）或空串（不显示第二行）；未配置的类型默认显示类型名.
/// </summary>
public class UpdateKnowledgeGraphCanvasStyleCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphCanvasStyleCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// caption 规则（实体类型 id → 属性名或空串）；传 null 清除全部规则回默认.
    /// </summary>
    public Dictionary<string, string?>? Captions { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphCanvasStyleCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Captions)
            .Must(x => x == null || x.Count <= 50).WithMessage("caption 规则最多 50 条.")
            .Must(x => x == null || x.Keys.All(k => long.TryParse(k, out _))).WithMessage("caption 规则键必须为实体类型 id.")
            .Must(x => x == null || x.Values.All(v => v == null || v.Length <= 100)).WithMessage("属性名最长 100 个字符.");
    }
}
