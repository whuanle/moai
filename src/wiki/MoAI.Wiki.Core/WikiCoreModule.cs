using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Wiki.Mcp;

namespace MoAI.Wiki;

/// <summary>
/// WikiCodeModule.
/// </summary>
[InjectModule<WikiSharedModule>]
[InjectModule<WikiApiModule>]
public class WikiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.ConfigureSessionOptions = async (context, serverOptions, cancellationToken) =>
                {
                    var mcpServer = context.RequestServices.GetRequiredService<McpWikiToolServer>();
                    await mcpServer.ConfigureWikiSpecificOptions(serverOptions, context, cancellationToken);
                };
            }).WithTools<McpWikiToolServer>();
    }
}
