using Maomi;
using Maomi.I18n;
using MoAI.Account;
using MoAI.AI;
using MoAI.AIChannel;
using MoAI.AIPlugin;
using MoAI.AIPlugin.Custom;
using MoAI.AIPlugin.Dynamic;
using MoAI.AIPlugin.Static;
using MoAI.App;
using MoAI.Auth;
using MoAI.Classify;
using MoAI.Common;
using MoAI.Database;
using MoAI.Feishu;
using MoAI.Filters;
using MoAI.Gateway;
using MoAI.Hangfire;
using MoAI.Infra;
using MoAI.KnowledgeGraph;
using MoAI.Modules;
using MoAI.OauthConnect;
using MoAI.Publication;
using MoAI.Prompt;
using MoAI.Settings;
using MoAI.Skill;
using MoAI.Storage;
using MoAI.Team;
using MoAI.TeamPlugin;
using MoAI.Variable;
using MoAI.Wiki;

namespace MoAI;

/// <summary>
/// MainModule.
/// </summary>
[InjectModule<InfraCoreModule>]
[InjectModule<DatabaseCoreModule>]
[InjectModule<StorageCoreModule>]
[InjectModule<CommonCoreModule>]
[InjectModule<AuthCoreModule>]
[InjectModule<AccountCoreModule>]
[InjectModule<AppCoreModule>]
[InjectModule<SettingsCoreModule>]
[InjectModule<OauthConnectCoreModule>]
[InjectModule<AIChannelCoreModule>]
[InjectModule<GatewayCoreModule>]
[InjectModule<TeamCoreModule>]
[InjectModule<WikiCoreModule>]
[InjectModule<KnowledgeGraphCoreModule>]
[InjectModule<VariableCoreModule>]
[InjectModule<SkillCoreModule>]
[InjectModule<TeamPluginCoreModule>]
[InjectModule<AIPluginCoreModule>]
[InjectModule<AiCoreModule>]
[InjectModule<FeishuCoreModule>]
[InjectModule<PublicationCoreModule>]
[InjectModule<PromptCoreModule>]
[InjectModule<ClassifyCoreModule>]
[InjectModule<StaticPluginsModule>]
[InjectModule<DynamicPluginsModule>]
[InjectModule<CustomPluginModule>]
[InjectModule<HangfireCoreModule>]
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