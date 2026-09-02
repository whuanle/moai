# CQRS 模块开发规范

## 模块三层架构

每个业务领域遵循三层模块化架构：

```
src/{domain}/
├── MoAI.{Domain}.Shared/     # 共享层 - DTO、Command、Query 定义
├── MoAI.{Domain}.Core/       # 核心层 - Handler 实现、业务逻辑
└── MoAI.{Domain}.Api/        # API 层 - Controller/Endpoint 暴露
```

依赖关系：`Api → Core → Shared`

## 目录结构规范

### Shared 层 (MoAI.{Domain}.Shared)

```
MoAI.{Domain}.Shared/
├── Commands/                  # Command 定义
│   └── {Action}{Entity}Command.cs
├── Queries/                   # Query 定义
│   ├── {Query}{Entity}Command.cs
│   └── Responses/             # Query 响应模型
│       ├── {Query}{Entity}CommandResponse.cs
│       └── {Query}{Entity}CommandResponseItem.cs
├── Models/                    # 共享模型、DTO
├── Services/                  # 服务接口定义
├── {Domain}SharedModule.cs    # 模块注册
└── MoAI.{Domain}.Shared.csproj
```

### Core 层 (MoAI.{Domain}.Core)

```
MoAI.{Domain}.Core/
├── Handlers/                  # Command Handler 实现
│   └── {Action}{Entity}CommandHandler.cs
├── Queries/                   # Query Handler 实现
│   └── {Query}{Entity}CommandHandler.cs
├── Services/                  # 服务实现
├── {Domain}CoreModule.cs      # 模块注册
└── MoAI.{Domain}.Core.csproj
```

### Api 层 (MoAI.{Domain}.Api)

```
MoAI.{Domain}.Api/
├── Controllers/               # API Controller
│   └── {Entity}Controller.cs
├── {Domain}ApiModule.cs       # 模块注册
└── MoAI.{Domain}.Api.csproj
```

## 命名规范

### Command 命名

- 文件名：`{动作}{实体}Command.cs`
- 类名：`{动作}{实体}Command`
- 命名空间：`MoAI.{Domain}.Commands`
- 示例：`UpdateUserInfoCommand.cs`

### Query 命名

- 文件名：`Query{实体}{描述}Command.cs` 或 `{Query}{Entity}Command.cs`
- 类名：与文件名一致
- 命名空间：`MoAI.{Domain}.Queries`
- 示例：`QueryUserBindAccountCommand.cs`

### Handler 命名

- 文件名：`{Command/Query名}Handler.cs`
- 类名：`{Command/Query名}Handler`
- 命名空间：`MoAI.{Domain}.Handlers` (Command) 或 `MoAI.{Domain}.Queries` (Query)
- 示例：`UpdateUserInfoCommandHandler.cs`

### Response 命名

- 文件名：`{Query名}Response.cs`
- 列表项：`{Query名}ResponseItem.cs`
- 命名空间：`MoAI.{Domain}.Queries.Responses`

## 代码模板

### Command 定义

注意请求模型需要继承 `IModelValidator<T>` ，编写验证模型请求参数是否正确，提前拦截无效请求。

```csharp
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.{Domain}.Commands;

/// <summary>
/// {功能描述}.
/// </summary>
public class {Action}{Entity}Command : IRequest<EmptyCommandResponse>, IModelValidator<{Action}{Entity}Command>
{
    /// <summary>
    /// {属性描述}.
    /// </summary>
    public int Id { get; set; }
    
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<{Action}{Entity}Command> validate)
    {
        // 模型验证
        validate.RuleFor(x => x.Id).NotEmpty();
    }
}
```



示例：

```csharp
/// <summary>
/// 完成文件上传.
/// </summary>
public class CompleteFileUploadCommand : IRequest<EmptyCommandResponse>, IModelValidator<CompleteFileUploadCommand>
{
    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件 ID.
    /// </summary>
    public int FileId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CompleteFileUploadCommand> validate)
    {
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 ID 不存在");
    }
}
```





### Query 定义

注意请求模型需要继承 `IModelValidator<T>` ，编写验证模型请求参数是否正确，提前拦截无效请求。

Query 如果是搜索、模糊查询，则参数可以可为空，但是注意长度等。

```csharp
using MediatR;
using MoAI.{Domain}.Queries.Responses;

namespace MoAI.{Domain}.Queries;

/// <summary>
/// {查询描述}.
/// </summary>
public class Query{Entity}Command : IRequest<Query{Entity}CommandResponse>, IModelValidator<Query{Entity}Command>
{
    /// <summary>
    /// {参数描述}.
    /// </summary>
    public int Id { get; init; }
    
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<Query{Entity}Command> validate)
    {
        // 模型验证
        validate.RuleFor(x => x.Id).NotEmpty();
    }
}
```

### Command Handler 实现

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.{Domain}.Commands;

namespace MoAI.{Domain}.Handlers;

/// <summary>
/// <inheritdoc cref="{Action}{Entity}Command"/>
/// </summary>
public class {Action}{Entity}CommandHandler : IRequestHandler<{Action}{Entity}Command, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public {Action}{Entity}CommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<EmptyCommandResponse> Handle({Action}{Entity}Command request, CancellationToken cancellationToken)
    {
        // 业务逻辑实现
        return EmptyCommandResponse.Default;
    }
}
```

### Query Handler 实现

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.{Domain}.Queries.Responses;

namespace MoAI.{Domain}.Queries;

/// <summary>
/// <inheritdoc cref="Query{Entity}Query"/>
/// </summary>
public class Query{Entity}CommandHandler : IRequestHandler<Query{Entity}Command, Query{Entity}CommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    public Query{Entity}CommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<Query{Entity}QueryResponse> Handle(Query{Entity}Query request, CancellationToken cancellationToken)
    {
        // 查询逻辑实现
        return new Query{Entity}QueryResponse();
    }
}
```

### Controller 实现

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.{Domain}.Commands;
using MoAI.{Domain}.Queries;

namespace MoAI.{Domain}.Controllers;

/// <summary>
/// {领域}相关接口.
/// </summary>
[ApiController]
[Route("/{domain}/{entity}")]
public partial class {Entity}Controller : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    public {Entity}Controller(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 获取当前用户上下文，可通过 userContext、UserContext...
    /// </summary>
    private UserContext CurrentUser => _userContextProvider.GetUserContext();

    /// <summary>
    /// {接口描述}.
    /// </summary>
    [HttpPost("{action}")]
    public async Task<EmptyCommandResponse> {Action}([FromBody] {Action}{Entity}Command req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }
}
```

## 用户信息传递

不允许直接注入 `UserContext`，必须先注入 `IUserContextProvider`，再通过 `GetUserContext()` 获取 `UserContext`：

```csharp
using MoAI.Infra.Services;

public class {Entity}Controller : ControllerBase
{
    private readonly IUserContextProvider _userContextProvider;

    public {Entity}Controller(IUserContextProvider userContextProvider)
    {
        _userContextProvider = userContextProvider;
    }

    public void Demo()
    {
        var userContext = _userContextProvider.GetUserContext();
        var userId = userContext.UserId; // 当前用户 ID
    }
}
```

不允许在 Handler 直接注入 `IUserContextProvider` 或 `UserContext`，如果需要根据用户 id 查询信息或限制范围，需要在 Command 继承 IUserIdContext，由 Command 传入。

```csharp
/// <summary>
/// 查询能看到的提示词列表.
/// </summary>
public class QueryPromptListCommand : IUserIdContext, IRequest<QueryPromptListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }
}
```

默认命令或查询模型用不到用户信息，请不要继承 IUserIdContext.


程序会在 ASP.NET Core 做模型绑定后，自动注入用户信息。

## 审计属性
审计输入由框架自动注入，不需要自己在插入或更新实体时手动传递。
实体的以下审计属性会自动赋值，不需要在 Handler 里面手动设置。

```
    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
```


## 模块注册

### Shared 模块

```csharp
using Maomi;

namespace MoAI.{Domain};

public class {Domain}SharedModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
    }
}
```

### Core 模块

```csharp
using Maomi;

namespace MoAI.{Domain};

[InjectModule<{Domain}SharedModule>]
[InjectModule<{Domain}ApiModule>]
public class {Domain}CoreModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
    }
}
```

## 子领域组织

对于复杂模块，可按子领域组织目录：

```
MoAI.Plugin.Shared/
├── Classify/
│   ├── Commands/
│   └── Queries/
├── CustomPlugins/
│   ├── Commands/
│   └── Queries/
├── NativePlugins/
│   ├── Commands/
│   ├── Queries/
│   └── Models/
└── ...

MoAI.Plugin.Core/
├── Classify/
│   ├── Handlers/
│   └── Queries/
├── CustomPlugins/
│   ├── Handlers/
│   └── Queries/
└── ...
```

## 异常处理

使用 `BusinessException` 抛出业务异常：

```csharp
throw new BusinessException("错误消息") { StatusCode = 404 };
```

抛出异常时，务必为 BusinessException 设置具体状态码，BusinessException 状态码会被设置为 http response 状态码，否则默 http statecode 值默认为 500，对前端拦截并不友好。



## 时间

时间统一使用 DataTimeOffset 类型，不要使用 DateTime 类型。



## Guid

当需要使用 Guid、uuid 时，数据库使用 uuid，后端代码使用 Guid，并且使用 `Guid.CreateVersion7()` 创建新的 id，不可以使用 `Guid.NewGuid()`。



## 枚举

枚举都应该设置 JsonPropertyName，因为前后端传递枚举参数类型时是使用字符串表示而不是枚举值，所以前后端传递枚举使用 JsonPropertyName 字符串表示。一般首字母小写接口，不使用下划线等拼接单词。

```csharp
public enum AIProtocolFamily
{
    [JsonPropertyName("openaiChatCompletions")]
    OpenAIChatCompletions = 0,

    [JsonPropertyName("openaiResponses")]
    OpenAIResponses = 1,

    [JsonPropertyName("anthropicMessages")]
    AnthropicMessages = 2,
    
    [JsonPropertyName("googleGemini")]
    GoogleGemini = 3,
}
```

