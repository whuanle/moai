using FluentValidation;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Helpers;
using Org.BouncyCastle.Asn1.Ocsp;

namespace MoAI.Plugin.CustomPlugins.Commands;

/// <summary>
/// 导入 openapi 文件，支持 json、yaml.
/// </summary>
public class TemplateImportOpenApiPluginCommand : IRequest<SimpleInt>, IModelValidator<ImportOpenApiPluginCommand>
{
    /// <summary>
    /// 上传的 id.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开，团队的插件不能设置公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int? TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportOpenApiPluginCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.")
            .Must(f =>
            {
                if (FileStoreHelper.OpenApiFormats.Contains(Path.GetExtension(f).ToLower()))
                {
                    return true;
                }

                return false;
            }).WithMessage("不支持的文件格式，请导入 .json/.yaml/.yml 文件");

        validate.RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage("文件 ID 必须大于 0.");
    }
}
