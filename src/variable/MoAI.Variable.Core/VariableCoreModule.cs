using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Variable.Services;

namespace MoAI.Variable;

/// <summary>
/// VariableCoreModule.
/// </summary>
[InjectModule<VariableSharedModule>]
[InjectModule<VariableApiModule>]
public class VariableCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<IVariableService, VariableService>();
    }
}
