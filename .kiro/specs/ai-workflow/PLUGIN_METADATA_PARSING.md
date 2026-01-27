# 插件元数据解析实现总结

## 实现概述

成功实现了工作流节点定义查询中的插件元数据解析功能，使得 Plugin 节点可以根据插件类型动态生成输入输出字段定义。

## 实现内容

### 1. 支持的插件类型

根据 `PluginEntity.Type` 字段，支持以下插件类型：

| Type | 插件类型 | 实体表 | 解析方式 |
|------|---------|--------|---------|
| 0 | Native Plugin（原生插件） | `PluginNativeEntity` | 从 `NativePluginFactory` 获取模板信息 |
| 1 | Tool Plugin（工具插件） | `PluginToolEntity` | 从 `NativePluginFactory` 获取模板信息 |
| 2 | Custom Plugin (MCP) | `PluginCustomEntity` | 从 `PluginFunctionEntity` 获取函数列表 |
| 3 | Custom Plugin (OpenAPI) | `PluginCustomEntity` | 从 `PluginFunctionEntity` 获取函数列表 |

### 2. 字段类型映射

实现了 `PluginConfigFieldType` 到 `FieldType` 的转换：

```csharp
PluginConfigFieldType → FieldType
- String → String
- Code → String
- Number → Number
- Integer → Number
- Boolean → Boolean
- Object → Object
- Map → Object
- Array → Array
```

### 3. 解析逻辑

#### Native Plugin 解析

```csharp
private async Task ParseNativePluginFieldsAsync(int nativePluginId, ...)
{
    // 1. 查询 PluginNativeEntity
    var nativePlugin = await _databaseContext.PluginNatives
        .Where(p => p.Id == nativePluginId && p.IsDeleted == 0)
        .FirstOrDefaultAsync(cancellationToken);

    // 2. 从插件工厂获取模板信息
    var pluginTemplate = _nativePluginFactory.GetPluginByKey(nativePlugin.TemplatePluginKey);

    // 3. 解析 ParamsFieldTemplates 为输入字段
    foreach (var fieldTemplate in pluginTemplate.ParamsFieldTemplates)
    {
        inputFields.Add(new FieldDefine
        {
            FieldName = fieldTemplate.Key,
            FieldType = ConvertPluginFieldTypeToWorkflowFieldType(fieldTemplate.FieldType),
            IsRequired = fieldTemplate.IsRequired,
            Description = fieldTemplate.Description
        });
    }

    // 4. 添加通用输出字段
    outputFields.Add(new FieldDefine
    {
        FieldName = "result",
        FieldType = FieldType.Dynamic,
        IsRequired = true,
        Description = "插件执行结果"
    });
}
```

#### Tool Plugin 解析

与 Native Plugin 类似，也是从 `NativePluginFactory` 获取模板信息。

#### Custom Plugin 解析

```csharp
private async Task ParseCustomPluginFieldsAsync(int customPluginId, ...)
{
    // 1. 查询 PluginCustomEntity
    var customPlugin = await _databaseContext.PluginCustoms
        .Where(p => p.Id == customPluginId && p.IsDeleted == 0)
        .FirstOrDefaultAsync(cancellationToken);

    // 2. 查询插件函数列表
    var functions = await _databaseContext.PluginFunctions
        .Where(f => f.PluginCustomId == customPluginId && f.IsDeleted == 0)
        .ToListAsync(cancellationToken);

    // 3. 提供通用输入字段
    inputFields.Add(new FieldDefine
    {
        FieldName = "functionName",
        FieldType = FieldType.String,
        IsRequired = true,
        Description = "要调用的函数名称"
    });

    inputFields.Add(new FieldDefine
    {
        FieldName = "parameters",
        FieldType = FieldType.Object,
        IsRequired = false,
        Description = "函数参数（JSON 对象）"
    });

    // 4. 添加输出字段
    outputFields.Add(new FieldDefine
    {
        FieldName = "result",
        FieldType = FieldType.Dynamic,
        IsRequired = true,
        Description = "函数执行结果"
    });

    // 5. 如果有可用函数，添加到描述中
    if (functions.Any())
    {
        outputFields.Add(new FieldDefine
        {
            FieldName = "availableFunctions",
            FieldType = FieldType.Array,
            IsRequired = false,
            Description = $"可用函数列表: {string.Join(", ", functions.Select(f => f.Name))}"
        });
    }
}
```

### 4. 依赖注入

在 `QueryNodeDefineCommandHandler` 中注入 `INativePluginFactory`：

```csharp
public class QueryNodeDefineCommandHandler : IRequestHandler<QueryNodeDefineCommand, QueryNodeDefineCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly INativePluginFactory _nativePluginFactory;

    public QueryNodeDefineCommandHandler(
        DatabaseContext databaseContext, 
        INativePluginFactory nativePluginFactory)
    {
        _databaseContext = databaseContext;
        _nativePluginFactory = nativePluginFactory;
    }
}
```

### 5. 项目引用

在 `MoAI.App.Workflow.Core.csproj` 中添加了对 `MoAI.Plugin.Shared` 的引用：

```xml
<ItemGroup>
    <ProjectReference Include="..\..\..\..\plugin\MoAI.Plugin.Shared\MoAI.Plugin.Shared.csproj" />
</ItemGroup>
```

## 使用示例

### 查询 Native Plugin 节点定义

**请求**：
```json
{
  "nodeType": "Plugin",
  "pluginId": 123
}
```

**响应**（假设是一个天气查询插件）：
```json
{
  "nodeType": "Plugin",
  "nodeTypeName": "插件节点",
  "description": "执行插件: 天气查询",
  "pluginId": 123,
  "pluginName": "天气查询",
  "inputFields": [
    {
      "fieldName": "city",
      "fieldType": "String",
      "isRequired": true,
      "description": "城市名称"
    },
    {
      "fieldName": "unit",
      "fieldType": "String",
      "isRequired": false,
      "description": "温度单位（celsius/fahrenheit）"
    }
  ],
  "outputFields": [
    {
      "fieldName": "result",
      "fieldType": "Dynamic",
      "isRequired": true,
      "description": "插件执行结果"
    }
  ],
  "supportsStreaming": false,
  "icon": "api",
  "color": "#1890ff"
}
```

### 查询 Custom Plugin 节点定义

**请求**：
```json
{
  "nodeType": "Plugin",
  "pluginId": 456
}
```

**响应**：
```json
{
  "nodeType": "Plugin",
  "nodeTypeName": "插件节点",
  "description": "执行插件: 自定义 API",
  "pluginId": 456,
  "pluginName": "自定义 API",
  "inputFields": [
    {
      "fieldName": "functionName",
      "fieldType": "String",
      "isRequired": true,
      "description": "要调用的函数名称"
    },
    {
      "fieldName": "parameters",
      "fieldType": "Object",
      "isRequired": false,
      "description": "函数参数（JSON 对象）"
    }
  ],
  "outputFields": [
    {
      "fieldName": "result",
      "fieldType": "Dynamic",
      "isRequired": true,
      "description": "函数执行结果"
    },
    {
      "fieldName": "availableFunctions",
      "fieldType": "Array",
      "isRequired": false,
      "description": "可用函数列表: getUserInfo, createOrder, updateStatus"
    }
  ],
  "supportsStreaming": false,
  "icon": "api",
  "color": "#1890ff"
}
```

## 设计考虑

### 1. 灵活性

- **Native/Tool Plugin**：直接从插件模板获取详细的参数定义，提供精确的字段信息
- **Custom Plugin**：提供通用的输入字段（functionName, parameters），因为自定义插件的参数结构可能非常灵活

### 2. 可扩展性

- 易于添加新的插件类型支持
- 字段类型映射可以轻松扩展
- 插件模板系统提供了统一的元数据管理

### 3. 性能

- 使用异步查询避免阻塞
- 只在需要时查询数据库
- 利用 EF Core 的查询优化

### 4. 错误处理

- 插件不存在时抛出 404 异常
- 不支持的插件类型抛出 400 异常
- 数据库查询失败会自动传播异常

## 后续优化建议

### 1. 缓存机制 💡

建议添加缓存以提高性能：

```csharp
// 使用 IMemoryCache 缓存插件定义
private readonly IMemoryCache _cache;

public async Task<QueryNodeDefineCommandResponse> GetPluginNodeDefineAsync(int pluginId, ...)
{
    var cacheKey = $"plugin-define-{pluginId}";
    
    if (_cache.TryGetValue(cacheKey, out QueryNodeDefineCommandResponse cached))
    {
        return cached;
    }
    
    var result = await ParsePluginDefinition(pluginId, cancellationToken);
    
    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
    
    return result;
}
```

### 2. Custom Plugin 参数解析 💡

对于 Custom Plugin（OpenAPI），可以进一步解析 OpenAPI 规范以提供更详细的参数定义：

```csharp
// 解析 OpenAPI 文件获取函数参数定义
var openapiSpec = await ParseOpenApiSpec(customPlugin.OpenapiFileId);
var functionDef = openapiSpec.GetFunction(functionName);

foreach (var param in functionDef.Parameters)
{
    inputFields.Add(new FieldDefine
    {
        FieldName = param.Name,
        FieldType = ConvertOpenApiType(param.Type),
        IsRequired = param.Required,
        Description = param.Description
    });
}
```

### 3. 权限验证 💡

添加权限检查确保用户有权访问特定插件：

```csharp
// 验证用户是否有权访问插件
var hasAccess = await _pluginAuthService.CheckAccessAsync(
    userId: request.ContextUserId,
    pluginId: pluginId,
    cancellationToken: cancellationToken
);

if (!hasAccess)
{
    throw new BusinessException("无权访问该插件") { StatusCode = 403 };
}
```

### 4. 国际化支持 💡

支持多语言的节点和字段描述：

```csharp
// 根据用户语言返回本地化描述
var description = _localizationService.GetString(
    key: $"plugin.{pluginTemplate.Key}.description",
    culture: request.Culture
);
```

## 测试建议

### 单元测试

1. 测试 Native Plugin 的字段解析
2. 测试 Tool Plugin 的字段解析
3. 测试 Custom Plugin 的字段解析
4. 测试字段类型转换
5. 测试插件不存在的错误处理
6. 测试不支持的插件类型的错误处理

### 集成测试

1. 测试完整的 API 调用流程
2. 测试数据库查询
3. 测试与 NativePluginFactory 的集成

## 总结

本次实现成功解决了插件元数据解析的问题，主要成果：

✅ **完整支持** - 支持所有三种插件类型（Native、Tool、Custom）
✅ **灵活设计** - 根据插件类型采用不同的解析策略
✅ **类型安全** - 正确的字段类型映射
✅ **依赖注入** - 使用 INativePluginFactory 访问插件模板
✅ **错误处理** - 完善的异常处理机制
✅ **可扩展** - 易于添加新的插件类型和字段类型

这个实现为工作流设计器提供了强大的插件节点定义查询能力，使得前端可以根据插件类型动态渲染参数表单，提供更好的用户体验。
