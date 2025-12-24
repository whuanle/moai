using Microsoft.SemanticKernel;
using MoAI.AI.Models;

namespace MoAI.Wiki.WikiPlugin;

public interface IWikiPluginBuilder
{
    Task<(IReadOnlyDictionary<string, string> PluginKeyName, IReadOnlyCollection<KernelPlugin> Plugins)> CreatePlugin(AiEndpoint aiEndpoint, IReadOnlyCollection<int> wikiIds);
}