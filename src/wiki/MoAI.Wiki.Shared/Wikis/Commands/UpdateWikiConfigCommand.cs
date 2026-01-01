using FluentValidation;
using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 更新知识库设置信息.
/// </summary>
public class UpdateWikiConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiConfigCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 头像 objectKey.
    /// </summary>
    public string Avatar { get; init; } = default!;

    /// <summary>
    /// 公开，私有知识库不能公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 指定进行文档向量化的模型，为空则不更新 .
    /// </summary>
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 维度，跟模型有关，小于嵌入向量的最大值，为空则不更新 .
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("知识库名称长度在 2-20 之间.")
            .Length(2, 20).WithMessage("知识库名称长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("知识库描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("知识库描述长度在 2-255 之间.");
    }
}
