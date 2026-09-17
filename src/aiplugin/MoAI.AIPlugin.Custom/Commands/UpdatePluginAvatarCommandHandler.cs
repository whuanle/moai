using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="UpdatePluginAvatarCommand"/>
/// </summary>
public class UpdatePluginAvatarCommandHandler : IRequestHandler<UpdatePluginAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePluginAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdatePluginAvatarCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePluginAvatarCommand request, CancellationToken cancellationToken)
    {
        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与提示词/用户头像同规则）
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded, cancellationToken);

        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken);

        if (plugin == null)
        {
            throw new BusinessException("插件不存在.") { StatusCode = 404 };
        }

        plugin.AvatarPath = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
