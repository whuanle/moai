#pragma warning disable CA1810 // 以内联方式初始化引用类型的静态字段

using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.NativePlugins.Queries;
using MoAI.Plugin.NativePlugins.Queries.Responses;
using MoAI.Plugin.Plugins;
using System.Reflection;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryNativePluginTemplateParamsCommand"/>
/// </summary>
public class QueryNativePluginTemplateParamsCommandHandler : IRequestHandler<QueryNativePluginTemplateParamsCommand, QueryNativePluginTemplateParamsCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryNativePluginTemplateParamsCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public QueryNativePluginTemplateParamsCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryNativePluginTemplateParamsCommandResponse> Handle(QueryNativePluginTemplateParamsCommand request, CancellationToken cancellationToken)
    {
        var pluginInfo = NativePluginFactory.Plugins.FirstOrDefault(x => x.Key == request.TemplatePluginKey);
        if (pluginInfo == null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var service = _serviceProvider.GetService(pluginInfo.Type);
        if (service is null)
        {
            throw new BusinessException("未找到插件模板") { StatusCode = 404 };
        }

        var toolPlugin = service as IToolPluginRuntime;

        if (toolPlugin == null)
        {
            throw new BusinessException("插件初始化异常，请联系开发者") { StatusCode = 404 };
        }

        var exampleValue = await toolPlugin.GetParamsExampleValue();
        List<NativePluginConfigFieldTemplate> confgFields = new List<NativePluginConfigFieldTemplate>();

        if (!pluginInfo.IsTool)
        {
            var nativePlugin = service as INativePluginRuntime;
            if (nativePlugin == null)
            {
                throw new BusinessException("插件初始化异常，请联系开发者") { StatusCode = 404 };
            }

            var configType = await nativePlugin.GetConfigTypeAsync();

            var properties = configType.GetProperties();

            foreach (var item in properties)
            {
                var nativePluginConfigFieldAttribute = item.GetCustomAttribute<NativePluginConfigFieldAttribute>();
                if (nativePluginConfigFieldAttribute != null)
                {
                    confgFields.Add(new NativePluginConfigFieldTemplate
                    {
                        Key = item.Name,
                        Description = nativePluginConfigFieldAttribute.Description,
                        IsRequired = nativePluginConfigFieldAttribute.IsRequired,
                        FieldType = nativePluginConfigFieldAttribute.FieldType,
                        ExampleValue = nativePluginConfigFieldAttribute.ExampleValue,
                    });
                }
            }
        }

        return new QueryNativePluginTemplateParamsCommandResponse
        {
            Items = confgFields,
            ExampleValue = exampleValue
        };
    }
}
