using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Skill.Services;

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
    }
}
