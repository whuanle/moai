using MediatR;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Services;

namespace MoAI.Settings.Handlers;

/// <summary>
/// <inheritdoc cref="SaveSettingCommand"/>
/// </summary>
public class SaveSettingCommandHandler : IRequestHandler<SaveSettingCommand, EmptyCommandResponse>
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveSettingCommandHandler"/> class.
    /// </summary>
    /// <param name="settingsService"></param>
    public SaveSettingCommandHandler(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public Task<EmptyCommandResponse> Handle(SaveSettingCommand request, CancellationToken cancellationToken)
    {
        return _settingsService.SaveSettingAsync(request, cancellationToken);
    }
}
