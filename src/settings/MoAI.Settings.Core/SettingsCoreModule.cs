using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Settings.Services;

namespace MoAI.Settings;

/// <summary>
/// SettingsCoreModule.
/// </summary>
[InjectModule<SettingsSharedModule>]
[InjectModule<SettingsApiModule>]
public class SettingsCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<ISettingsService, SettingsService>();
    }
}
