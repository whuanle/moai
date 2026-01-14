#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using MoAI.AI.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Plugins;
using MoAI.Storage.Queries;
using MoAI.Wiki.WikiPlugin;
using ModelContextProtocol.Client;

namespace MoAI.App.Handlers;

/// <summary>
/// 构建插件.
/// </summary>
public partial class ProcessingAppChatCommandHandler
{
    private async Task<IReadOnlyCollection<KernelPlugin>> GetPluginsAsync(ProcessingAiAssistantChatContext context, IReadOnlyCollection<string> pluginKeys, IReadOnlyCollection<int> wikiIds)
    {
        var plugins = new List<KernelPlugin>();
        var mcpPlugins = await GetMCPPluginsAsync(context, pluginKeys);
        plugins.AddRange(mcpPlugins);

        var openApiPlugins = await GetOpenApiPluginsAsync(context, pluginKeys);
        plugins.AddRange(openApiPlugins);

        var nativePlugins = await GeNativePluginsAsync(context, pluginKeys);
        plugins.AddRange(nativePlugins);

        var wikiPlugins = await GeWikiPluginsAsync(context, wikiIds);
        plugins.AddRange(wikiPlugins);

        return plugins;
    }

    private async Task<IReadOnlyCollection<KernelPlugin>> GetMCPPluginsAsync(ProcessingAiAssistantChatContext context, IReadOnlyCollection<string> pluginKeys)
    {
        var customPlugins = await _databaseContext.Plugins.Where(x => pluginKeys.Contains(x.PluginName) && x.Type == (int)PluginType.MCP)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (a, b) => new
            {
                PluginEntity = a,
                PluginCustomEntity = b
            })
            .ToArrayAsync();

        List<KernelPlugin> kernelFunctions = new List<KernelPlugin>();

        foreach (var item in customPlugins)
        {
            var pluginEntity = item.PluginEntity;
            var pluginCustomEntity = item.PluginCustomEntity;

            var headers = pluginCustomEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;
            var queries = pluginCustomEntity.Queries.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;

            var defaultOptions = new McpClientOptions
            {
                ClientInfo = new() { Name = "MoAI", Version = "1.0.0" }
            };

            var uriBuilder = new UriBuilder(pluginCustomEntity.Server);
            if (queries != null && queries.Count > 0)
            {
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                foreach (var kv in queries)
                {
                    query[kv.Key] = kv.Value;
                }

                uriBuilder.Query = query.ToString();
            }

            var serverUrl = uriBuilder.Uri;
            var defaultConfig = new HttpClientTransportOptions
            {
                Endpoint = serverUrl,
                AdditionalHeaders = headers.ToDictionary(x => x.Key, x => x.Value),
            };

            var sseTransport = new HttpClientTransport(defaultConfig);
            var client = await McpClient.CreateAsync(
             sseTransport,
             defaultOptions,
             loggerFactory: _loggerFactory);

            _asyncDisposables.Add(client);
            _asyncDisposables.Add(sseTransport);

            var tools = await client.ListToolsAsync();

            KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions(
                pluginName: pluginEntity.PluginName,
                description: pluginEntity.Description ?? string.Empty,
                functions: tools.Select(aiFunction => aiFunction.AsKernelFunction()));

            kernelFunctions.Add(plugin);
            context.PluginKeyNames[pluginEntity.PluginName] = pluginEntity.Title;
        }

        return kernelFunctions;
    }

    private async Task<IReadOnlyCollection<KernelPlugin>> GetOpenApiPluginsAsync(ProcessingAiAssistantChatContext context, IReadOnlyCollection<string> pluginKeys)
    {
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

        var customPlugins = await _databaseContext.Plugins.Where(x => pluginKeys.Contains(x.PluginName) && x.Type == (int)PluginType.OpenApi)
            .Join(_databaseContext.PluginCustoms, a => a.PluginId, b => b.Id, (a, b) => new
            {
                PluginEntity = a,
                PluginCustomEntity = b
            })
            .ToArrayAsync();

        List<KernelPlugin> kernelFunctions = new List<KernelPlugin>();

        foreach (var item in customPlugins)
        {
            var pluginEntity = item.PluginEntity;
            var pluginCustomEntity = item.PluginCustomEntity;

            var headers = pluginCustomEntity.Headers.JsonToObject<IReadOnlyCollection<KeyValueString>>()!;

            var fileEntity = await _databaseContext.Files
                .Where(x => x.Id == pluginCustomEntity.OpenapiFileId)
                .FirstOrDefaultAsync();

            if (fileEntity == null)
            {
                throw new BusinessException("插件{0}已失效", pluginEntity.Title) { StatusCode = 409 };
            }

            var filePath = await _mediator.Send(new QueryFileLocalPathCommand
            {
                ObjectKey = fileEntity.ObjectKey
            });

            OpenApiDocumentParser parser = new();
            using FileStream stream = System.IO.File.OpenRead(filePath.FilePath);
            RestApiSpecification specification = await parser.ParseAsync(stream);

            var httpClient = _httpClientFactory.CreateClient("OpenApiClient");

            foreach (var header in headers)
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            KernelPlugin plugin = OpenApiKernelPluginFactory.CreateFromOpenApi(
                pluginName: pluginEntity.PluginName,
                specification: specification,
                executionParameters: new OpenApiFunctionExecutionParameters()
                {
                    EnablePayloadNamespacing = true,
                    ServerUrlOverride = new Uri(pluginCustomEntity.Server),
                    LoggerFactory = _loggerFactory,
                    HttpClient = httpClient,
                });

            _disposables.Add(httpClient);

            kernelFunctions.Add(plugin);
            context.PluginKeyNames[pluginEntity.PluginName] = pluginEntity.Title;
        }

        return kernelFunctions;
    }

    private async Task<IReadOnlyCollection<KernelPlugin>> GeNativePluginsAsync(ProcessingAiAssistantChatContext context, IReadOnlyCollection<string> pluginKeys)
    {
        var templatePlugins = _nativePluginFactory.GetPlugins();

        var nativePlugins = await _databaseContext.Plugins.Where(x => pluginKeys.Contains(x.PluginName) && x.Type == (int)PluginType.NativePlugin)
            .Join(_databaseContext.PluginNatives, a => a.PluginId, b => b.Id, (a, b) => new
            {
                PluginEntity = a,
                PluginNativeEntity = b
            })
            .ToArrayAsync();

        List<KernelPlugin> kernelFunctions = new List<KernelPlugin>();

        foreach (var item in nativePlugins)
        {
            var pluginEntity = item.PluginEntity;
            var pluginNativeEntity = item.PluginNativeEntity;

            var template = _nativePluginFactory.GetPluginByKey(pluginNativeEntity.TemplatePluginKey);
            if (template == null)
            {
                continue;
            }

            var nativePluginRuntime = _serviceProvider.GetService(template.Type) as INativePluginRuntime;

            if (nativePluginRuntime == null)
            {
                continue;
            }

            try
            {
                await nativePluginRuntime.ImportConfigAsync(pluginNativeEntity.Config!);
            }
            catch
            {
                continue;
            }

            if (nativePluginRuntime is IDisposable disposable)
            {
                _disposables.Add(disposable);
            }

            if (nativePluginRuntime is IAsyncDisposable asyncDisposable)
            {
                _asyncDisposables.Add(asyncDisposable);
            }

            var kernelPlugin = KernelPluginFactory.CreateFromObject(nativePluginRuntime, pluginEntity.PluginName);
            kernelFunctions.Add(kernelPlugin);
            context.PluginKeyNames[pluginEntity.PluginName] = pluginEntity.Title;
        }

        foreach (var item in templatePlugins.Where(x => x.PluginType == PluginType.ToolPlugin && pluginKeys.Contains(x.Key)))
        {
            var nativePluginRuntime = _serviceProvider.GetService(item.Type) as IToolPluginRuntime;

            if (nativePluginRuntime == null)
            {
                continue;
            }

            if (nativePluginRuntime is IDisposable disposable)
            {
                _disposables.Add(disposable);
            }

            if (nativePluginRuntime is IAsyncDisposable asyncDisposable)
            {
                _asyncDisposables.Add(asyncDisposable);
            }

            var kernelPlugin = KernelPluginFactory.CreateFromObject(nativePluginRuntime, item.Key);
            kernelFunctions.Add(kernelPlugin);
            context.PluginKeyNames[item.Key] = item.Name;
        }

        return kernelFunctions;
    }

    private async Task<IReadOnlyCollection<KernelPlugin>> GeWikiPluginsAsync(ProcessingAiAssistantChatContext context, IReadOnlyCollection<int> wikiIds)
    {
        var nativePluginRuntime = _serviceProvider.GetRequiredService<IWikiPluginBuilder>();
        var (pluginKeyNames, plugins) = await nativePluginRuntime.CreatePlugin(context.AiModel, wikiIds);

        foreach (var item in pluginKeyNames)
        {
            context.PluginKeyNames[item.Key] = item.Value;
        }

        return plugins;
    }
}
