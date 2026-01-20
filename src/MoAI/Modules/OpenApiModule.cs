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
/// 使用 NSwag 作为 API 文档的模块.
/// </summary>
public class OpenApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddEndpointsApiExplorer();

        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
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
        });
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