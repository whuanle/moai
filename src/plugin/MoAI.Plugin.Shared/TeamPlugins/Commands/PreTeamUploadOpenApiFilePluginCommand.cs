using FluentValidation;
using MediatR;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.CustomPlugins.Commands.Responses;

namespace MoAI.Plugin.TeamPlugins.Commands;

/// <summary>
/// 预上传 openapi 文件，支持 json、yaml.
/// </summary>
public class PreTeamUploadOpenApiFilePluginCommand : PreUploadOpenApiFilePluginCommand, IRequest<PreUploadOpenApiFilePluginCommandResponse>, IModelValidator<PreTeamUploadOpenApiFilePluginCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreTeamUploadOpenApiFilePluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 错误.");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        validate.RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.")
            .Length(32).WithMessage("文件 MD5 长度必须为 32 位.")
            .Matches("^[a-fA-F0-9]{32}$").WithMessage("文件 MD5 格式不正确.");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于 0.")
            .LessThanOrEqualTo(1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}
