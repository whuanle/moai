# 规范更新摘要

## 更新日期
2025-01-25

## 更新内容

### 1. 使用 `IReadOnlyCollection` 替代 `List`

**变更原因：**
- 更好的不可变性：表明数据是只读的，防止意外修改
- API 层验证：ASP.NET Core 的模型绑定会在请求到达 Handler 之前验证模型
- 更好的封装：不暴露 List 的修改方法
- 符合 CQRS 原则：命令应该是不可变的

**变更位置：**

#### CreateWorkflowDefinitionCommand
```csharp
// 之前
public List<NodeDesign> Nodes { get; set; }
public List<Connection> Connections { get; set; }

// 之后
public IReadOnlyCollection<NodeDesign> Nodes { get; set; }
public IReadOnlyCollection<Connection> Connections { get; set; }
```

#### UpdateWorkflowDefinitionCommand
```csharp
// 之前
public List<NodeDesign> Nodes { get; set; }
public List<Connection> Connections { get; set; }

// 之后
public IReadOnlyCollection<NodeDesign> Nodes { get; set; }
public IReadOnlyCollection<Connection> Connections { get; set; }
```

#### WorkflowDefinition
```csharp
// 之前
public List<NodeDesign> Nodes { get; set; }
public List<Connection> Connections { get; set; }

// 之后
public IReadOnlyCollection<NodeDesign> Nodes { get; set; }
public IReadOnlyCollection<Connection> Connections { get; set; }
```

#### WorkflowDefinitionService.ValidateConnections
```csharp
// 之前
public void ValidateConnections(
    List<NodeDesign> nodes, 
    List<Connection> connections)

// 之后
public void ValidateConnections(
    IReadOnlyCollection<NodeDesign> nodes, 
    IReadOnlyCollection<Connection> connections)
```

### 2. 明确验证层次

#### API 层验证（模型绑定）
- 验证 JSON 格式正确
- 验证属性类型匹配
- 验证必需字段存在（使用 `[Required]` 特性）
- 验证字符串长度（使用 `[StringLength]` 特性）
- 验证集合最小长度（使用 `[MinLength]` 特性）

#### Handler 层验证（业务逻辑）
- 验证节点类型有效
- 验证 Start/End 节点数量
- 验证连接的源节点和目标节点存在
- 验证图结构有效
- 验证节点配置满足要求

### 3. 文档更新

#### requirements.md
- ✅ 明确说明接收节点定义列表和连接列表
- ✅ 添加连接验证要求

#### design.md
- ✅ 更新 WorkflowDefinition 模型使用 IReadOnlyCollection
- ✅ 更新 WorkflowDefinitionService 方法签名

#### tasks.md
- ✅ 更新任务描述以反映验证要求

#### VALIDATION_REQUIREMENTS.md（新增）
- ✅ 详细的验证要求文档
- ✅ API 层和 Handler 层验证说明
- ✅ 错误处理和错误代码
- ✅ 实现示例

## 实现建议

### 1. Command 定义示例

```csharp
using System.ComponentModel.DataAnnotations;
using MediatR;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Commands;

/// <summary>
/// 创建工作流定义命令.
/// </summary>
public class CreateWorkflowDefinitionCommand : IRequest<CreateWorkflowDefinitionCommandResponse>
{
    /// <summary>
    /// 工作流名称.
    /// </summary>
    [Required(ErrorMessage = "工作流名称不能为空")]
    [StringLength(100, ErrorMessage = "工作流名称不能超过100个字符")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作流描述.
    /// </summary>
    [StringLength(500, ErrorMessage = "工作流描述不能超过500个字符")]
    public string? Description { get; set; }
    
    /// <summary>
    /// 节点列表.
    /// </summary>
    [Required(ErrorMessage = "节点列表不能为空")]
    [MinLength(1, ErrorMessage = "至少需要一个节点")]
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; } = Array.Empty<NodeDesign>();
    
    /// <summary>
    /// 连接列表.
    /// </summary>
    [Required(ErrorMessage = "连接列表不能为空")]
    public IReadOnlyCollection<Connection> Connections { get; set; } = Array.Empty<Connection>();
}
```

### 2. Handler 实现示例

```csharp
using MediatR;
using MoAI.Database;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Services;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="CreateWorkflowDefinitionCommand"/>
/// </summary>
public class CreateWorkflowDefinitionCommandHandler 
    : IRequestHandler<CreateWorkflowDefinitionCommand, CreateWorkflowDefinitionCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowDefinitionService _workflowDefinitionService;

    public CreateWorkflowDefinitionCommandHandler(
        DatabaseContext databaseContext,
        WorkflowDefinitionService workflowDefinitionService)
    {
        _databaseContext = databaseContext;
        _workflowDefinitionService = workflowDefinitionService;
    }

    public async Task<CreateWorkflowDefinitionCommandResponse> Handle(
        CreateWorkflowDefinitionCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. 构建工作流定义对象
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Nodes = request.Nodes,
            Connections = request.Connections
        };
        
        // 2. 验证工作流定义（会抛出 WorkflowValidationException）
        _workflowDefinitionService.ValidateWorkflowDefinition(definition);
        
        // 3. 序列化并存储到数据库
        var entity = new WorkflowDesignEntity
        {
            Name = definition.Name,
            Description = definition.Description,
            FunctionDesign = JsonSerializer.Serialize(definition)
        };
        
        _databaseContext.WorkflowDesigns.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        
        return new CreateWorkflowDefinitionCommandResponse
        {
            Id = entity.Id
        };
    }
}
```

### 3. Controller 实现示例

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Workflow.Commands;

namespace MoAI.Workflow.Controllers;

/// <summary>
/// 工作流定义相关接口.
/// </summary>
[ApiController]
[Route("/api/workflow/definition")]
public class WorkflowDefinitionController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowDefinitionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建工作流定义.
    /// </summary>
    [HttpPost]
    public async Task<CreateWorkflowDefinitionCommandResponse> Create(
        [FromBody] CreateWorkflowDefinitionCommand command,
        CancellationToken ct = default)
    {
        // ASP.NET Core 已经完成了模型绑定和基本验证
        // 如果验证失败，会自动返回 400 Bad Request
        return await _mediator.Send(command, ct);
    }
}
```

## 验证流程图

```
客户端请求
    ↓
API 层（Controller）
    ├─ 模型绑定
    ├─ 基本验证（[Required], [StringLength] 等）
    └─ 如果失败 → 返回 400 Bad Request
    ↓
MediatR Pipeline
    ↓
Handler 层
    ├─ 构建 WorkflowDefinition 对象
    ├─ 调用 WorkflowDefinitionService.ValidateWorkflowDefinition()
    │   ├─ 验证节点类型
    │   ├─ 验证 Start/End 节点
    │   ├─ 验证节点配置
    │   └─ 验证连接
    └─ 如果失败 → 抛出 WorkflowValidationException → 返回 400 Bad Request
    ↓
数据库层
    └─ 存储 WorkflowDesignEntity
    ↓
返回响应
```

## 关键要点

1. **不可变性**：使用 `IReadOnlyCollection` 确保命令数据不被修改
2. **分层验证**：API 层验证格式，Handler 层验证业务逻辑
3. **清晰的错误信息**：提供详细的验证错误，帮助用户快速定位问题
4. **类型安全**：使用强类型的 NodeDesign 和 Connection，而不是字符串
5. **关注点分离**：验证逻辑集中在 WorkflowDefinitionService 中

## 下一步

规范已经完成更新，可以开始实现任务：

1. 打开 `.kiro/specs/ai-workflow/tasks.md` 查看任务列表
2. 按照任务顺序实现功能
3. 参考 `VALIDATION_REQUIREMENTS.md` 了解详细的验证要求
4. 使用 `IReadOnlyCollection` 作为集合类型
5. 在 API 层添加适当的验证特性
6. 在 Handler 层调用 WorkflowDefinitionService 进行业务验证
