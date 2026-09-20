using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Services;

namespace MoAI.Settings.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateSystemLogoCommand"/>
/// </summary>
public class UpdateSystemLogoCommandHandler : IRequestHandler<UpdateSystemLogoCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSystemLogoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="settingsService">设置领域服务.</param>
    public UpdateSystemLogoCommandHandler(DatabaseContext databaseContext, ISettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSystemLogoCommand request, CancellationToken cancellationToken)
    {
        // ObjectKey 为空表示恢复默认 Logo，清空设置值；非空时仅允许引用已完成上传并登记的文件，防止伪造 objectKey
        if (!string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            var fileExists = await _databaseContext.Files
                .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded && f.IsDeleted == 0, cancellationToken);
            if (!fileExists)
            {
                throw new BusinessException("Logo 文件不存在或未完成上传.") { StatusCode = 404 };
            }
        }

        return await _settingsService.SaveSettingAsync(new SaveSettingCommand
        {
            Key = SettingDefinitions.SystemLogoKey,
            Value = request.ObjectKey?.Trim() ?? string.Empty
        }, cancellationToken);
    }
}
