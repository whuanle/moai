using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Storage;

namespace MoAI.App;

/// <summary>
/// AppCoreModule.
/// </summary>
[InjectModule<AppSharedModule>]
[InjectModule<AppApiModule>]
[InjectModule<StorageSharedModule>]
public class AppCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 对话附件文本提取（Maomi.ToMarkdown 进程内库）
        context.Services.AddTextExtraction();
    }
}
