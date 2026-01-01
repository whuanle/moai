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
│   ├── {Query}{Entity}Query.cs
│   └── Responses/             # Query 响应模型
│       ├── {Query}{Entity}QueryResponse.cs
│       └── {Query}{Entity}QueryResponseItem.cs
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
│   └── {Query}{Entity}QueryHandler.cs
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

```csharp
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.{Domain}.Commands;

/// <summary>
/// {功能描述}.
/// </summary>
public class {Action}{Entity}Command : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// {属性描述}.
    /// </summary>
    public int Id { get; set; }
}
```

### Query 定义

```csharp
using MediatR;
using MoAI.{Domain}.Queries.Responses;

namespace MoAI.{Domain}.Queries;

/// <summary>
/// {查询描述}.
/// </summary>
public class Query{Entity}Command : IRequest<Query{Entity}CommandResponse>
{
    /// <summary>
    /// {参数描述}.
    /// </summary>
    public int Id { get; init; }
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
    private readonly UserContext _userContext;

    public {Entity}Controller(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

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

不允许在 Handler 直接注入 IUserContext，如果需要根据用户 id 查询信息或限制范围，需要在 Command 继承 IUserIdContext，由 Command 传入。

```csharp
/// <summary>
/// 查询能看到的提示词列表.
/// </summary>
public class QueryPromptListCommand : IUserIdContext, IRequest<QueryPromptListCommandResponse>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }
}
```


程序会在 ASP.NET Core 做模型绑定后，自动注入用户信息。

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

常用状态码：
- 400: 请求参数错误
- 403: 无权限
- 404: 资源不存在
- 409: 资源冲突
