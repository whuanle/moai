using Maomi;
using Maomi.I18n;
using MoAI.Admin;
using MoAI.AI;
using MoAI.AiModel.Core;
using MoAI.App.AIAssistant;
using MoAI.Common;
using MoAI.Database;
using MoAI.Filters;
using MoAI.Infra;
using MoAI.Login;
using MoAI.Modules;
using MoAI.Plugin;
using MoAI.Storage;
using MoAI.User;
using MoAI.Wiki;
using MoAIPrompt.Core;

namespace MoAI;

/// <summary>
/// MainModule.
/// </summary>
[InjectModule<InfraCoreModule>]
[InjectModule<DatabaseCoreModule>]
[InjectModule<StorageCoreModule>]
[InjectModule<CommonCoreModule>]
[InjectModule<LoginCoreModule>]
[InjectModule<AdminCoreModule>]
[InjectModule<UserCoreModule>]
[InjectModule<AiModelCoreModule>]
//[InjectModule<WikiCoreModule>]
[InjectModule<PluginCoreModule>]
[InjectModule<PromptCoreModule>]
[InjectModule<AppAiAssistantCoreModule>]
[InjectModule<AiCoreModule>]
[InjectModule<ApiModule>]
public partial class MainModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public MainModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 添加HTTP上下文访问器
        context.Services.AddHttpContextAccessor();
        context.Services.AddExceptionHandler<MaomiExceptionHandler>();
        context.Services.AddI18nAspNetCore();
    }
}