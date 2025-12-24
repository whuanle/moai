#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.SemanticKernel;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.System.Text.Json;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MoAI.Wiki.WikiPlugin;

[InjectOnScoped]
internal class WikiPluginBuilder : IWikiPluginBuilder
{
    private static readonly MethodInfo InvokeInfo = typeof(WikiPlugin).GetMethod(nameof(WikiPlugin.InvokeAsync), BindingFlags.Public | BindingFlags.Instance)!;
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IAiClientBuilder _aiClientBuilder;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiPluginBuilder"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="aiClientBuilder"></param>
    /// <param name="mediator"></param>
    public WikiPluginBuilder(
        DatabaseContext databaseContext,
        SystemOptions systemOptions,
        IAiClientBuilder aiClientBuilder,
        IMediator mediator)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
        _aiClientBuilder = aiClientBuilder;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyDictionary<string,string> PluginKeyName, IReadOnlyCollection<KernelPlugin> Plugins)> CreatePlugin(AiEndpoint aiEndpoint, IReadOnlyCollection<int> wikiIds)
    {
        List<KernelPlugin> plugins = new();

        var wikis = await _databaseContext.Wikis
        .Where(x => wikiIds.Contains(x.Id))
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiId = a.Id,
            Name = a.Name,
            Description = a.Description,
            Config = new EmbeddingConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingModelId = a.EmbeddingModelId,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                Provider = x.AiProvider.JsonToObject<AiProvider>(),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                Key = x.Key,
            }
        }).ToArrayAsync();

        Dictionary<string, string> pluginKeyNames = new();
        foreach (var item in wikis)
        {
            var textEmbeddingGenerator = _aiClientBuilder.CreateTextEmbeddingGenerator(item.AiEndpoint, item.Config.EmbeddingDimensions);
            var memoryDb = _aiClientBuilder.CreateMemoryDb(textEmbeddingGenerator, _systemOptions.Wiki.DBType.JsonToObject<MemoryDbType>());

            WikiPlugin wikiPlugin = new(_databaseContext, memoryDb, item.WikiId);

            var function = KernelFunctionFactory.CreateFromMethod(InvokeInfo, wikiPlugin, loggerFactory: null);
            var plugin = KernelPluginFactory.CreateFromFunctions($"wiki_{item.WikiId}", item.Description, new KernelFunction[] { function });
            plugins.Add(plugin);
            pluginKeyNames[$"wiki_{item.WikiId}"] = item.Name;
        }

        return (pluginKeyNames, plugins);
    }

    private async Task<(EmbeddingConfig? WikiConfig, AiEndpoint? AiEndpoint)> GetWikiConfigAsync(int wikiId)
    {
        var result = await _databaseContext.Wikis
        .Where(x => x.Id == wikiId)
        .Join(_databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new
        {
            WikiConfig = new EmbeddingConfig
            {
                EmbeddingDimensions = a.EmbeddingDimensions,
                EmbeddingModelId = a.EmbeddingModelId,
            },
            AiEndpoint = new AiEndpoint
            {
                Name = x.Name,
                DeploymentName = x.DeploymentName,
                Title = x.Title,
                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                Provider = x.AiProvider.JsonToObject<AiProvider>(),
                ContextWindowTokens = x.ContextWindowTokens,
                Endpoint = x.Endpoint,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                Key = x.Key,
            }
        }).FirstOrDefaultAsync();

        return (result?.WikiConfig, result?.AiEndpoint);
    }
}
