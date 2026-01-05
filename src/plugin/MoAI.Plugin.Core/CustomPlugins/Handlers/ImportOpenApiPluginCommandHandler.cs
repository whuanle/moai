using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;

namespace MoAIPlugin.Core.Commands;

/// <summary>
/// 完成 openapi 文件上传，并拆解生成到数据库.
/// </summary>
public class ImportOpenApiPluginCommandHandler : IRequestHandler<ImportOpenApiPluginCommand, SimpleInt>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public ImportOpenApiPluginCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task<SimpleInt> Handle(ImportOpenApiPluginCommand request, CancellationToken cancellationToken)
    {
        return _mediator.Send(
            new TemplateImportOpenApiPluginCommand
            {
                FileId = request.FileId,
                FileName = request.FileName,
                Name = request.Name,
                Title = request.Title,
                Description = request.Description,
                ClassifyId = request.ClassifyId,
                IsPublic = request.IsPublic,
                TeamId = null,
            },
            cancellationToken);
    }
}
