using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Skill.Services;
using MoAI.Statistics.Services;

namespace MoAI.Skill;

/// <summary>
/// SkillCoreModule.
/// </summary>
[InjectModule<SkillSharedModule>]
[InjectModule<SkillApiModule>]
public class SkillCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<ISkillService, SkillService>();

        // 技能使用次数计数器：Redis 自增 + 统计模块每分钟批量累加到 skills.use_count
        context.Services.AddScoped<ISkillUsageCounter, SkillUsageCounter>();
        context.Services.AddScoped<IResourceUsageWriter, SkillUsageCounterActivatorJob>();
    }
}
