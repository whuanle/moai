using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.OpenApi;

/// <summary>
/// 将 OpenApi 导入知识库.
/// </summary>
public class ImportOpenApiToWikiCommand : IRequest<EmptyCommandResponse>, IModelValidator<ImportOpenApiToWikiCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 直接上传，上传后会有 FileId.
    /// </summary>
    public int? FileId { get; init; }

    /// <summary>
    /// json 文件地址.
    /// </summary>
    public string? OpenApiSpecUrl { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportOpenApiToWikiCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库 id 错误");
        validate.RuleFor(x => x)
            .Must(x => x.FileId.HasValue || !string.IsNullOrWhiteSpace(x.OpenApiSpecUrl))
            .WithMessage("必须提供 FileId 或 OpenApiSpecUrl 中的一个。");
        validate.When(x => x.FileId.HasValue, () =>
        {
            validate.RuleFor(x => x.FileId.Value).GreaterThan(0).WithMessage("FileId 错误");
        });
        validate.When(x => !string.IsNullOrWhiteSpace(x.OpenApiSpecUrl), () =>
        {
            validate.RuleFor(x => x.OpenApiSpecUrl)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("OpenApiSpecUrl 必须是一个有效的 URL。");
        });
    }
}
