using Maomi;
using Maomi.I18n;
using MoAI.Admin;
using MoAI.AI;
using MoAI.AiModel.Core;
using MoAI.App;
using MoAI.App.AIAssistant;
using MoAI.Common;
using MoAI.Database;
using MoAI.External;
using MoAI.Filters;
using MoAI.Hangfire;
using MoAI.Infra;
using MoAI.Login;
using MoAI.Modules;
using MoAI.Plugin;
using MoAI.Storage;
using MoAI.Team;
using MoAI.User;
using MoAI.Wiki;
using MoAI.Workflow;
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
[InjectModule<WikiCoreModule>]
[InjectModule<PluginCoreModule>]
[InjectModule<PromptCoreModule>]
[InjectModule<AppAiAssistantCoreModule>]
[InjectModule<AiCoreModule>]
[InjectModule<TeamCoreModule>]
[InjectModule<AppCoreModule>]
[InjectModule<ExternalCoreModule>]
[InjectModule<HangfireCoreModule>]
[InjectModule<WorkflowCoreModule>]
[InjectModule<ApiModule>]
public partial class MainModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 添加HTTP上下文访问器
        context.Services.AddHttpContextAccessor();
        context.Services.AddExceptionHandler<MaomiExceptionHandler>();
        context.Services.AddI18nAspNetCore();
    }
}