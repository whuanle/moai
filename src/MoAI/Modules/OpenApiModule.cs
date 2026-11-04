using System.Collections;
using System.Reflection;
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
/// 使用 NSwag 作为 API 文档的模块：内部接口生成 /openapi/v1.json，团队资源接口（/api/teamres）单独生成 /openapi/teamres.json，
/// 子系统协作接口（/api/subsys）单独生成 /openapi/subsys.json.
/// </summary>
public class OpenApiModule : IModule
{
    /// <summary>
    /// 团队资源接口路由前缀，控制器路由为 /teamres，全局 /api 前缀由 ApiApplicationModelConvention 添加.
    /// </summary>
    private const string TeamResourcePathPrefix = "/api/teamres";

    /// <summary>
    /// 子系统协作接口路由前缀，控制器路由为 /subsys，全局 /api 前缀由 ApiApplicationModelConvention 添加.
    /// </summary>
    private const string SubsysPathPrefix = "/api/subsys";

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddEndpointsApiExplorer();

        // 主文档：内部接口（不含团队资源接口与子系统协作接口），保持默认文档名 v1（/openapi/v1.json，前端 Kiota 依赖）
        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
        {
            ConfigureDocument(settings, serviceProvider);
            settings.OperationProcessors.Add(new ExcludeTeamResourcePathOperationProcessor());
            settings.OperationProcessors.Add(new ExcludeSubsysPathOperationProcessor());
        });

        // 团队资源文档：仅包含团队资源接口（/openapi/teamres.json）
        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
        {
            ConfigureDocument(settings, serviceProvider);
            settings.DocumentName = "teamres";
            settings.Title = "AI Team Resource API";
            settings.Description = "MoAI team resource openapi document (machine face, /api/teamres).";
            settings.OperationProcessors.Add(new TeamResourceOnlyPathOperationProcessor());
        });

        // 子系统协作文档：仅包含协作接口（/openapi/subsys.json），供子系统后端对接
        context.Services.AddOpenApiDocument((settings, serviceProvider) =>
        {
            ConfigureDocument(settings, serviceProvider);
            settings.DocumentName = "subsys";
            settings.Title = "AI Subsystem API";
            settings.Description = "MoAI subsystem collaboration openapi document (machine face, /api/subsys).";
            settings.OperationProcessors.Add(new SubsysOnlyPathOperationProcessor());
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

            // 路径过滤只管 operations，components.schemas 仍是全库注册表；按本文档可达性裁剪使其自包含
            PruneUnreferencedSchemas(document);
        };
    }

    /// <summary>
    /// 判断操作是否为团队资源接口（/api/teamres 前缀，按路径段匹配，避免 /api/teamresxxx 误命中）.
    /// </summary>
    private static bool IsTeamResourcePath(OperationProcessorContext context)
    {
        return HasPathPrefix(context.OperationDescription.Path, TeamResourcePathPrefix);
    }

    /// <summary>
    /// 判断操作是否为子系统协作接口（/api/subsys 前缀，按路径段匹配，避免 /api/subsystem/** 管理面误命中）.
    /// </summary>
    private static bool IsSubsysPath(OperationProcessorContext context)
    {
        return HasPathPrefix(context.OperationDescription.Path, SubsysPathPrefix);
    }

    /// <summary>
    /// 按完整路径段判断前缀命中：相等或以 prefix + "/" 开头.
    /// </summary>
    private static bool HasPathPrefix(string? path, string prefix)
    {
        return !string.IsNullOrEmpty(path)
            && (path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 裁剪文档 components.schemas 至仅保留本文档操作可达的定义.
    /// NSwag 按全部控制器生成 schema 注册表，路径级处理器（ExternalOnly/SubsysOnly）不会回收被剔除操作引用的定义，
    /// 导致每份文档都携带全库 schema（v1/external/subsys 各 580 个），机器面文档不自包含、
    /// 接入方据此生成客户端时混入无关模型与告警.
    /// </summary>
    private static void PruneUnreferencedSchemas(OpenApiDocument document)
    {
        var components = document.Components?.Schemas;
        if (components == null || components.Count == 0)
        {
            return;
        }

        // 定义实例（含 components 包装实例）→ 逻辑名，用于把 $ref 解析回 components 键
        var nameByDefinition = new Dictionary<JsonSchema, string>(ReferenceEqualityComparer.Instance);
        foreach (var pair in components)
        {
            nameByDefinition.TryAdd(pair.Value, pair.Key);
            if (pair.Value.Reference != null)
            {
                nameByDefinition.TryAdd(pair.Value.Reference, pair.Key);
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<JsonSchema>();

        // 种子 = 组件定义树之外的全部 schema 引用（operations 请求/响应/头/参数、path 级参数、ExpectedSchemas、回调等）。
        // NSwag 序列化时会遍历整张文档对象图并解析每个 $ref，任何位置漏收集都会留下悬空引用导致 ToJson 失败，
        // 因此这里按 NSwag 对象模型做反射遍历，而不是只枚举 operations。
        CollectExternalSchemaReferences(document, pending, new HashSet<object>(ReferenceEqualityComparer.Instance));

        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            if (node == null)
            {
                continue;
            }

            var target = node.Reference ?? node;
            if (nameByDefinition.TryGetValue(target, out var name) && visited.Add(name))
            {
                EnqueueSchemaChildren(target, pending);
                if (target.Reference != null)
                {
                    // components 里存的也可能是包装实例，真实定义体在 Reference 上
                    EnqueueSchemaChildren(target.Reference, pending);
                }
            }

            if (node != target)
            {
                // 包装实例自身可能还带组合子引用（如 nullable oneOf 包 $ref）
                EnqueueSchemaChildren(node, pending);
            }
        }

        foreach (var key in components.Keys.Where(key => !visited.Contains(key)).ToList())
        {
            components.Remove(key);
        }
    }

    /// <summary>
    /// 反射遍历文档对象图，收集组件定义树之外出现的全部 schema 引用目标（$ref 解析后的定义实例）.
    /// 只下钻 NSwag/NJsonSchema 命名空间的属性图与容器值，ExtensionData（原始 JSON）跳过.
    /// </summary>
    private static void CollectExternalSchemaReferences(object? node, Queue<JsonSchema> pending, HashSet<object> graphSeen)
    {
        if (node == null || node is string || node is ValueType || !graphSeen.Add(node))
        {
            return;
        }

        if (node is JsonSchema schema)
        {
            if (schema.Reference != null)
            {
                pending.Enqueue(schema.Reference);
            }

            // 无引用的内联 schema 还可能嵌套组合子引用（如 nullable oneOf 包 $ref），继续下钻
            EnqueueChildNodes(schema, pending, graphSeen);
            return;
        }

        // 容器（字典/序列）下钻其值；OpenApiPathItem/OpenApiHeaders 等「既是字典又是 POCO」的类型还需继续走属性（如 path 级 Parameters）
        if (node is IDictionary dictionary)
        {
            foreach (var value in dictionary.Values)
            {
                CollectExternalSchemaReferences(value, pending, graphSeen);
            }
        }
        else if (node is IEnumerable sequence)
        {
            foreach (var element in sequence)
            {
                CollectExternalSchemaReferences(element, pending, graphSeen);
            }
        }

        // components 整体跳过：定义树内部的引用由 BFS 的 EnqueueSchemaChildren 按可达性传递，避免「被裁剪定义的内部引用」反向保活其它定义
        if (node is OpenApiDocument document)
        {
            CollectExternalSchemaReferences(document.Paths, pending, graphSeen);
            CollectExternalSchemaReferences(document.SecurityDefinitions, pending, graphSeen);
            return;
        }

        var type = node.GetType();
        if (type.Namespace == null || (!type.Namespace.StartsWith("NSwag", StringComparison.Ordinal) && !type.Namespace.StartsWith("NJsonSchema", StringComparison.Ordinal)))
        {
            return;
        }

        EnqueueChildNodes(node, pending, graphSeen);
    }

    /// <summary>
    /// 反射枚举对象的公共可读属性并逐个下钻（跳过索引器与 ExtensionData 原始扩展数据）.
    /// </summary>
    private static void EnqueueChildNodes(object node, Queue<JsonSchema> pending, HashSet<object> graphSeen)
    {
        var properties = node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length > 0 || property.Name == "ExtensionData" || property.Name == "Parent")
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(node);
            }
            catch (Exception)
            {
                // 个别派生属性可能抛错，跳过即可
                continue;
            }

            CollectExternalSchemaReferences(value, pending, graphSeen);
        }
    }

    /// <summary>
    /// 把 schema 的子引用（属性/数组项/附加属性/取反/allOf·anyOf·oneOf）加入待访问队列.
    /// </summary>
    private static void EnqueueSchemaChildren(JsonSchema schema, Queue<JsonSchema> pending)
    {
        foreach (var property in schema.Properties.Values)
        {
            pending.Enqueue(property);
        }

        pending.Enqueue(schema.Item);
        pending.Enqueue(schema.AdditionalPropertiesSchema);
        pending.Enqueue(schema.Not);

        foreach (var child in schema.AllOf)
        {
            pending.Enqueue(child);
        }

        foreach (var child in schema.AnyOf)
        {
            pending.Enqueue(child);
        }

        foreach (var child in schema.OneOf)
        {
            pending.Enqueue(child);
        }
    }

    /// <summary>
    /// 剔除团队资源接口：操作处理器返回 false 时该操作不写入当前文档.
    /// </summary>
    private class ExcludeTeamResourcePathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return !IsTeamResourcePath(context);
        }
    }

    /// <summary>
    /// 剔除子系统协作接口：不写入内部主文档.
    /// </summary>
    private class ExcludeSubsysPathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return !IsSubsysPath(context);
        }
    }

    /// <summary>
    /// 仅保留团队资源接口，其余操作不写入当前文档.
    /// </summary>
    private class TeamResourceOnlyPathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return IsTeamResourcePath(context);
        }
    }

    /// <summary>
    /// 仅保留子系统协作接口，其余操作不写入当前文档.
    /// </summary>
    private class SubsysOnlyPathOperationProcessor : IOperationProcessor
    {
        public bool Process(OperationProcessorContext context)
        {
            return IsSubsysPath(context);
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
