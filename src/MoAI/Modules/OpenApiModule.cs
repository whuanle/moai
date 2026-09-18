using Maomi;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using MoAI.Infra;
using MoAI.Swaggers;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.Generation.TypeMappers;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MoAI.Modules;

/// <summary>
/// 使用 NSwag 作为 API 文档的模块：内部接口生成 /openapi/v1.json，外部接口（/api/external）单独生成 /openapi/external.json.
/// </summary>
public class OpenApiModule : IModule
{
    /// <summary>
    /// 外部接口路由前缀，控制器路由为 /external，全局 /api 前缀由 ApiApplicationModelConvention 添加.
    /// </summary>
    private const string ExternalPathPrefix = "/api/external";

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddEndpointsApiExplorer();

        // 主文档：内部接口（不含外部接口），保持默认文档名 v1（/openapi/v1.json，前端 Kiota 依赖）
        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
        {
            ConfigureDocument(settings, serviceProvider);
            settings.OperationProcessors.Add(new ExcludeExternalPathOperationProcessor());
        });

        // 外部文档：仅包含外部接口（/openapi/external.json）
        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
        {
            ConfigureDocument(settings, serviceProvider);
            settings.DocumentName = "external";
            settings.Title = "AI External API";
            settings.Description = "MoAI external openapi document.";
            settings.OperationProcessors.Add(new ExternalOnlyPathOperationProcessor());
        });
    }

    private static void ConfigureDocument(AspNetCoreOpenApiDocumentGeneratorSettings settings, IServiceProvider serviceProvider)
    {
        settings.Title = "AI API";
        settings.Version = "v1";
        settings.Description = "MoAI openapi document.";

        // 配置 System.Text.Json 序列化选项
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        serializerOptions.Converters.Add(JsonMetadataServices.DecimalConverter);

        // 将配置应用到 SchemaSettings
        if (settings.SchemaSettings is SystemTextJsonSchemaGeneratorSettings stjSettings)
        {
            stjSettings.SerializerOptions = serializerOptions;
        }

        // 要与 SerializerSettings 对应序列化配置和显示的 Swagger 类型
        settings.SchemaSettings.TypeMappers.Add(
            new PrimitiveTypeMapper(
                typeof(Guid),
                schema =>
                {
                    schema.Type = JsonObjectType.String;
                    schema.Format = "uuid";
                }));

        settings.SchemaSettings.TypeMappers.Add(new LongTypeMapper());
        settings.SchemaSettings.TypeMappers.Add(new DateTimeOffsetTypeMapper());

        settings.OperationProcessors.Add(new ErrorResponseOperationProcessor());
        settings.OperationProcessors.Add(new EndpointGroupingOperationProcessor());

        settings.PostProcess = document =>
        {
            var systemOptions = serviceProvider.GetRequiredService<SystemOptions>();
            var server = serviceProvider.GetService<IServer>();
            var serverAddressesFeature = server?.Features.Get<IServerAddressesFeature>();

            document.Servers.Add(new OpenApiServer
            {
                Url = systemOptions.Server,
                Description = "User-defined service address"
            });

            if (serverAddressesFeature != null)
            {
                foreach (var address in serverAddressesFeature.Addresses)
                {
                    document.Servers.Add(new OpenApiServer
                    {
                        Url = address,
                        Description = "Local service address"
                    });
                }
            }
        };
    }

    /// <summary>
    /// 判断操作是否为外部接口（/api/external 前缀）.
    /// </summary>
    private static bool IsExternalPath(OperationProcessorContext context)
    {
        var path = context.OperationDescription.Path;
        return !string.IsNullOrEmpty(path) && path.StartsWith(ExternalPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 剔除外部接口：操作处理器返回 false 时该操作不写入当前文档.
    /// </summary>
    private class ExcludeExternalPathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return !IsExternalPath(context);
        }
    }

    /// <summary>
    /// 仅保留外部接口，其余操作不写入当前文档.
    /// </summary>
    private class ExternalOnlyPathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return IsExternalPath(context);
        }
    }

    /// <summary>
    /// 自定义操作处理器：优先使用 EndpointGroupName 作为 Tag，否则保留默认（通常是 Controller 名称）
    /// </summary>
    private class EndpointGroupingOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            if (context is AspNetCoreOperationProcessorContext aspNetContext &&
                !string.IsNullOrEmpty(aspNetContext.ApiDescription.GroupName))
            {
                context.OperationDescription.Operation.Tags.Clear();
                context.OperationDescription.Operation.Tags.Add(aspNetContext.ApiDescription.GroupName);
            }

            return true;
        }
    }
}
