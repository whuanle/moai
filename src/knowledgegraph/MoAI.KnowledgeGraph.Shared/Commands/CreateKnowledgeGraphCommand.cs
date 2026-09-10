using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 创建知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key，null=自定义.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// 来源：managed / connected.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入的 Neo4j 数据库名（仅 connected）.
    /// </summary>
    public string? Database { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空.").MaximumLength(50).WithMessage("名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("简介最长 255 个字符.");
        validate.RuleFor(x => x.Mode).Must(KnowledgeGraphModes.IsValid).WithMessage("图谱来源不合法.");
        validate.RuleFor(x => x.Database)
            .NotEmpty().WithMessage("接入图谱必须填写数据库名.")
            .MaximumLength(100).WithMessage("数据库名最长 100 个字符.")
            .When(x => x.Mode == KnowledgeGraphModes.Connected);
        validate.RuleFor(x => x.TemplateKey).Empty().When(x => x.Mode == KnowledgeGraphModes.Connected).WithMessage("接入图谱不能使用模板.");
    }
}
