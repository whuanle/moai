using Maomi;
using Maomi.I18n;
using MoAI.Common;
using MoAI.Database;
using MoAI.Filters;
using MoAI.Hangfire;
using MoAI.Infra;
using MoAI.Login;
using MoAI.Modules;
using MoAI.Storage;

namespace MoAI;

/// <summary>
/// MainModule.
/// </summary>
[InjectModule<InfraCoreModule>]
[InjectModule<DatabaseCoreModule>]
[InjectModule<StorageCoreModule>]
[InjectModule<CommonCoreModule>]
[InjectModule<LoginCoreModule>]
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