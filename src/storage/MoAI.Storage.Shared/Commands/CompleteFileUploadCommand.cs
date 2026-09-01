using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Models;

namespace MoAI.Storage.Commands;

/// <summary>
/// 完成文件上传.
/// </summary>
public class CompleteFileUploadCommand : IRequest<CompleteFileUploadCommandResponse>, IModelValidator<CompleteFileUploadCommand>
{
    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件 ID.
    /// </summary>
    public long FileId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CompleteFileUploadCommand> validate)
    {
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 ID 不存在");
    }
}
