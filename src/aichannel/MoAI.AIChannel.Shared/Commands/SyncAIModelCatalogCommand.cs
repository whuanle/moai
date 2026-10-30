using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 从 models.opencode.ai 下载最新模型目录（models.json）并保存到 configs 目录，保存后立即生效，无需重启.
/// </summary>
public class SyncAIModelCatalogCommand : IRequest<SyncAIModelCatalogCommandResponse>, IModelValidator<SyncAIModelCatalogCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SyncAIModelCatalogCommand> validate)
    {
    }
}
