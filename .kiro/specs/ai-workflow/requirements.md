# 需求文档：AI 工作流编排引擎

## 介绍

AI 工作流编排引擎是 MoAI 平台的核心功能，使用户能够设计和执行复杂的工作流，结合 AI 模型、知识库、插件和条件逻辑。工作流作为一种特殊类型的应用（AppType.Workflow），通过 AppEntity 统一管理。该系统提供可视化工作流设计器，用户可以创建节点、使用表达式和变量配置输入，并通过实时流式反馈执行工作流。引擎支持多种节点类型（Start、End、AiChat、Wiki、Plugin、Condition、ForEach、Fork、JavaScript、DataProcess），并维护执行历史以供审计和调试。

## 术语表

- **工作流应用**：AppType 为 Workflow 的 AppEntity，作为工作流的入口
- **工作流定义**：存储在 AppWorkflowDesignEntity 中的完整工作流设计，包含所有节点定义和连接
- **工作流实例**：工作流定义的特定执行，存储在 AppWorkflowHistoryEntity 中
- **节点**：工作流中的离散工作单元（例如 AiChat、Wiki、Plugin）
- **节点设计**：节点的配置，包括输入、输出和参数分配
- **节点运行时**：特定节点类型的执行引擎
- **工作流上下文**：只读运行时上下文，包含执行状态、变量和节点输出
- **字段表达式**：为节点输入分配值的方式（Fixed、Variable、JsonPath、StringInterpolation）
- **变量引用**：访问工作流上下文中的数据（例如 sys.userId、input.field、nodeKey.output）
- **扁平化输出**：将嵌套 JSON 转换为扁平结构以便变量访问（例如从 {A1:{Name:"value"}} 中访问 A1.Name）
- **节点管道**：节点的执行记录，包括输入、输出和状态
- **流式响应**：工作流执行期间发送给客户端的实时更新
- **系统变量**：以 sys.* 为前缀的内置变量（例如 sys.userId、sys.timestamp）
- **启动参数**：执行工作流时传递的初始参数
- **节点状态**：节点的当前执行状态（Pending、Running、Completed、Failed）
- **草稿版本**：UiDesignDraft 和 FunctionDesignDraft，用于编辑中的工作流
- **发布版本**：UiDesign 和 FunctionDesign，用于执行的正式版本

## 需求

### 需求 1：工作流应用管理

**用户故事**：作为用户，我想通过 App 统一入口创建和管理工作流应用，以便工作流与其他应用类型保持一致的管理方式。

#### 验收标准

1. 当用户创建工作流应用时，系统应通过 TeamAppController 的 CreateApp 接口创建 AppEntity（AppType=Workflow）和对应的 AppWorkflowDesignEntity
2. 当用户删除工作流应用时，系统应通过 TeamAppController 的 DeleteApp 接口软删除 AppEntity 和 AppWorkflowDesignEntity，但保留执行历史
3. 当用户查询团队应用列表时，系统应通过 TeamAppController 的 QueryAppList 接口返回包括工作流应用在内的所有应用
4. 当用户启用/禁用工作流应用时，系统应通过 TeamAppController 的 SetAppDisable 接口更新 AppEntity 的 IsDisable 状态
5. 工作流应用的创建和删除必须通过 TeamAppController 统一管理，不允许绕过 App 层直接操作 AppWorkflowDesignEntity

### 需求 2：工作流定义编辑

**用户故事**：作为用户，我想编辑工作流定义的节点和连接，以便我可以设计结合多个节点和连接的复杂 AI 工作流。

#### 验收标准

1. 当用户编辑工作流定义时，工作流系统应接收节点定义列表和连接列表（而非字符串形式的 FunctionDesign），并存储到 FunctionDesignDraft 字段
2. 当用户向工作流定义添加节点时，工作流系统应验证每个节点具有来自固定集合的有效 NodeType（Start、End、Plugin、Wiki、AiChat、Condition、ForEach、Fork、JavaScript、DataProcess）
3. 当用户配置节点连接时，工作流系统应验证连接形成有效的有向图，恰好有一个 Start 节点和至少一个 End 节点，并且所有连接的源节点和目标节点都存在于节点列表中
4. 当用户创建或更新工作流定义时，工作流系统应验证每个节点的必需输入字段已配置，字段类型兼容，并且所有变量引用在执行时可解析
5. 当用户检索工作流定义时，工作流系统应返回完整定义，包括所有节点、连接和节点设计（包括草稿和发布版本）
6. 当用户更新工作流定义时，工作流系统应重新验证节点和连接，持久化更改到草稿字段
7. 当用户发布工作流定义时，工作流系统应将草稿版本（FunctionDesignDraft）复制到发布版本（FunctionDesign），并创建版本快照

### 需求 3：节点定义和配置

**用户故事**：作为用户，我想使用不同的表达式类型配置节点输入，以便我可以创建具有动态数据流的灵活工作流。

#### 验收标准

1. 当用户配置节点输入时，工作流系统应支持四种表达式类型：Fixed（常数值）、Variable（对上下文的引用）、JsonPath（嵌套对象访问）、StringInterpolation（带变量替换的模板）
2. 当用户在节点输入中引用变量时，工作流系统应从工作流上下文中解析它，包括系统变量（sys.*）、启动参数（input.*）和前一个节点的输出（nodeKey.*）
3. 当用户使用 JsonPath 表达式时，工作流系统应正确解析和评估路径，如"sys.user.id"或"nodeA.result[0].name"
4. 当用户使用 StringInterpolation 表达式时，工作流系统应替换模板字符串中的变量，如"Hello {input.name}, your ID is {sys.userId}"
5. 当用户配置节点时，工作流系统应验证所有必需的输入字段都已提供且类型兼容
6. 当用户定义 Plugin 节点时，工作流系统应从插件元数据动态生成节点定义，包括输入、输出和字段类型

### 需求 4：工作流执行引擎

**用户故事**：作为用户，我想执行工作流并接收实时状态更新，以便我可以监控工作流进度并调试问题。

#### 验收标准

1. 当用户执行工作流时，工作流系统应创建 AppWorkflowHistoryEntity 记录并使用启动参数初始化执行上下文
2. 当工作流系统处理工作流时，工作流系统应按照定义的连接从 Start 节点开始顺序执行节点
3. 当节点完成执行时，工作流系统应向客户端流式传输 WorkflowProcessingItem，包含 NodeType、NodeKey、Input、Output 和 State
4. 当节点在执行期间失败时，工作流系统应捕获错误、将节点状态更新为 Failed 并向客户端流式传输错误信息和堆栈跟踪
5. 当工作流系统遇到 Condition 节点时，工作流系统应评估条件表达式并将执行路由到适当的下一个节点
6. 当工作流系统遇到 ForEach 节点时，工作流系统应迭代集合并为每个项目执行循环体
7. 当工作流系统遇到 Fork 节点时，工作流系统应并行执行多个分支并等待所有分支完成后再继续
8. 当工作流完成执行时，工作流系统应存储完整的执行历史到 AppWorkflowHistoryEntity，包括所有节点管道和最终输出

### 需求 5：变量解析和输出扁平化

**用户故事**：作为用户，我想在后续节点中引用前一个节点的输出，以便我可以创建节点相互依赖的数据管道。

#### 验收标准

1. 当节点完成执行时，工作流系统应将嵌套 JSON 输出扁平化为可通过点符号访问的扁平结构（例如从 {A1:{Name:"value"}} 中访问 A1.Name）
2. 当用户引用节点输出变量时，工作流系统应使用格式 nodeKey.fieldName 从扁平化输出映射中解析它
3. 当用户引用数组元素时，工作流系统应支持数组访问语法，如 nodeKey.items[0].name 或 nodeKey.items[*].name 用于所有元素
4. 当工作流系统评估变量引用时，工作流系统应维护包含系统变量、启动参数和所有已执行节点输出的上下文映射
5. 当变量引用无效或缺失时，工作流系统应返回错误，指示缺失的变量和上下文中可用的变量

### 需求 6：节点类型实现

**用户故事**：作为用户，我想使用不同的节点类型来执行各种工作流任务，以便我可以构建全面的 AI 工作流。

#### 验收标准

1. 当用户使用 Start 节点时，工作流系统应使用启动参数初始化工作流上下文并路由到下一个节点
2. 当用户使用 End 节点时，工作流系统应终止工作流并返回最终输出
3. 当用户使用 AiChat 节点时，工作流系统应使用配置的提示和参数调用 AI 模型，并返回模型响应
4. 当用户使用 Wiki 节点时，工作流系统应使用提供的搜索查询查询知识库并返回相关文档
5. 当用户使用 Plugin 节点时，工作流系统应使用配置的输入执行指定的插件并返回插件输出
6. 当用户使用 JavaScript 节点时，工作流系统应执行提供的 JavaScript 代码并可访问工作流上下文，并返回结果
7. 当用户使用 DataProcess 节点时，工作流系统应对输入数据执行数据转换操作（map、filter、aggregate）
8. 当用户使用 Condition 节点时，工作流系统应评估布尔表达式并根据结果路由到不同的下一个节点

### 需求 7：CQRS 实现

**用户故事**：作为开发者，我想工作流系统遵循 CQRS 模式，以便系统可维护且可扩展。

#### 验收标准

1. 当用户创建工作流应用时，系统应使用 CreateAppCommand 通过 CQRS 模式处理操作，同时创建 AppWorkflowDesignEntity
2. 当用户更新工作流定义时，工作流系统应使用 UpdateWorkflowDefinitionCommand 处理操作并更新草稿字段
3. 当用户发布工作流定义时，工作流系统应使用 PublishWorkflowDefinitionCommand 处理操作并创建版本快照
4. 当用户执行工作流时，工作流系统应使用 ExecuteWorkflowCommand 处理操作并流式传输结果
5. 当用户查询工作流定义时，工作流系统应使用 QueryWorkflowDefinitionCommand 检索数据
6. 当用户查询工作流实例时，工作流系统应使用 QueryWorkflowInstanceCommand 检索执行历史
7. 工作流系统应将关注点分离为 Shared（DTOs、Commands、Queries）和 Core（Handlers、Services）层
8. 工作流系统的 API 接口应放在 MoAI.App.Api 的 Workflow 目录下，而不是独立的 Workflow.Api 项目

### 需求 8：API 端点

**用户故事**：作为前端开发者，我想要工作流操作的 REST API 端点，以便我可以将工作流集成到用户界面中。

#### 验收标准

1. 当客户端调用 POST /app/team/create（AppType=Workflow）时，系统应创建工作流应用和对应的 AppWorkflowDesignEntity
2. 当客户端调用 DELETE /app/team/delete 时，系统应软删除工作流应用和 AppWorkflowDesignEntity
3. 当客户端调用 POST /app/team/list 时，系统应返回包括工作流应用在内的应用列表
4. 当客户端调用 GET /app/workflow/definition/{appId} 时，工作流系统应返回完整的工作流定义（包括草稿和发布版本）
5. 当客户端调用 PUT /app/workflow/definition/{appId} 时，工作流系统应更新工作流定义的草稿字段
6. 当客户端调用 POST /app/workflow/definition/{appId}/publish 时，工作流系统应发布工作流定义
7. 当客户端调用 POST /app/workflow/execute 时，工作流系统应执行工作流并通过服务器发送事件（SSE）流式传输结果
8. 当客户端调用 GET /app/workflow/instance/{id} 时，工作流系统应返回工作流实例的执行历史和结果
9. 当客户端调用 GET /app/workflow/instance 时，工作流系统应返回工作流实例的分页列表

### 需求 9：数据持久化

**用户故事**：作为系统管理员，我想将工作流定义和执行历史存储在数据库中，以便工作流是持久的且可审计的。

#### 验收标准

1. 当创建工作流应用时，系统应存储 AppEntity（AppType=Workflow）和 AppWorkflowDesignEntity 及其所有元数据
2. 当执行工作流时，工作流系统应创建 AppWorkflowHistoryEntity 实体，包含执行参数、状态和完整的节点执行记录
3. 当用户编辑工作流时，工作流系统应更新 AppWorkflowDesignEntity 的草稿字段（UiDesignDraft、FunctionDesignDraft）
4. 当用户发布工作流时，工作流系统应将草稿复制到发布字段（UiDesign、FunctionDesign）并创建版本快照
5. 当用户查询执行历史时，工作流系统应检索 AppWorkflowHistoryEntity 记录，包含适当的过滤和分页
6. 工作流系统应在所有实体上维护审计字段（CreateUserId、CreateTime、UpdateUserId、UpdateTime、IsDeleted）

### 需求 10：错误处理和验证

**用户故事**：作为用户，我想在工作流失败时获得清晰的错误消息，以便我可以快速调试和修复问题。

#### 验收标准

1. 当工作流定义无效时，工作流系统应返回验证错误，列出所有问题（缺少 Start 节点、无效连接等）
2. 当节点输入缺失或类型错误时，工作流系统应返回验证错误，指示字段名称和预期类型
3. 当变量引用无效时，工作流系统应返回错误，指示缺失的变量和上下文中可用的变量
4. 当节点执行失败时，工作流系统应捕获异常、记录它并向客户端流式传输包含堆栈跟踪信息的错误
5. 当工作流被中断时，工作流系统应将实例标记为 Failed 并保留执行状态以供调试

### 需求 11：可扩展性

**用户故事**：作为开发者，我想轻松添加新的节点类型，以便工作流系统可以使用自定义功能进行扩展。

#### 验收标准

1. 当需要新的节点类型时，工作流系统应允许注册新的 INodeRuntime 实现
2. 当注册新的节点类型时，工作流系统应自动在工作流设计器中提供它
3. 当注册插件时，工作流系统应从插件元数据动态生成节点定义
4. 工作流系统应通过 FieldType 枚举支持自定义字段类型（Empty、String、Number、Boolean、Object、Array、Dynamic）
