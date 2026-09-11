using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Commands.Responses;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 预上传团队 OpenAPI 文件，支持 json、yaml，需团队 Owner/Admin.
/// </summary>
public class PreUploadTeamOpenApiFileCommand : IUserIdContext, IRequest<PreUploadOpenApiFilePluginCommandResponse>, IModelValidator<PreUploadTeamOpenApiFileCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件名称，用于检测团队内同名.
    /// </summary>
    public string PluginName { get; init; } = default!;

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadTeamOpenApiFileCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        validate.RuleFor(x => x.SHA256)
            .NotEmpty().WithMessage("文件 SHA-256 不能为空.")
            .Matches("^[a-fA-F0-9]{64}$").WithMessage("文件 SHA-256 格式不正确.");

        validate.RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("文件大小必须大于 0.")
            .LessThanOrEqualTo(1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}
