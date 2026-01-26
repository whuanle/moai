# 实现计划：AI 工作流编排引擎

## 概述

本实现计划将 AI 工作流编排引擎设计转化为一系列离散的编码步骤。每个步骤都建立在前一个步骤的基础上，最终将所有组件集成在一起。实现遵循 MoAI CQRS 约定，使用现有的数据库实体（WorkflowDesignEntity、WorkflowDesignSnapshotEntity、WorkflowHistoryEntity）。

## 任务

- [x] 1. 创建 Shared 层 - 基础模型和枚举
  - [x] 1.1 创建 MoAI.Workflow.Shared 项目结构
    - 创建 Commands、Queries、Models、Enums 目录
    - 创建 WorkflowSharedModule.cs
    - _需求：6.1_

  - [x] 1.2 定义节点类型和字段类型枚举
    - 创建 NodeType.cs（Start、End、Plugin、Wiki、AiChat、Condition、ForEach、Fork、JavaScript、DataProcess）
    - 创建 FieldType.cs（Empty、String、Number、Boolean、Object、Array、Dynamic）
    - 创建 FieldExpressionType.cs（Fixed、Variable、JsonPath、StringInterpolation）
    - 创建 NodeState.cs（Pending、Running、Completed、Failed）
    - _需求：1.2, 2.1_

  - [x] 1.3 定义节点定义接口和模型
    - 创建 INodeDefine.cs 接口
    - 创建 FieldDefine.cs 模型
    - 创建 NodeDefine.cs 实现类
    - _需求：2.1, 2.6_

  - [x] 1.4 定义节点设计模型
    - 创建 NodeDesign.cs（NodeKey、Name、Description、NodeType、NextNodeKey、FieldDesigns）
    - 创建 FieldDesign.cs（FieldName、ExpressionType、Value）
    - 创建 Connection.cs（SourceNodeKey、TargetNodeKey、Label）
    - 创建 WorkflowDefinition.cs（Id、Name、Description、Nodes、Connections）
    - _需求：1.1, 2.1, 2.2_

  - [x] 1.5 定义工作流上下文和节点管道接口
    - 创建 IWorkflowContext.cs 接口
    - 创建 INodePipeline.cs 接口
    - 创建 WorkflowProcessingItem.cs 模型
    - _需求：3.3, 4.4_

  - [x] 1.6 定义 CQRS Commands
    - 创建 CreateWorkflowDefinitionCommand.cs
    - 创建 UpdateWorkflowDefinitionCommand.cs
    - 创建 DeleteWorkflowDefinitionCommand.cs
    - 创建 ExecuteWorkflowCommand.cs
    - _需求：6.1, 6.2_

  - [x] 1.7 定义 CQRS Queries 和 Responses
    - 创建 QueryWorkflowDefinitionCommand.cs 和 Response
    - 创建 QueryWorkflowInstanceCommand.cs 和 Response
    - 创建 QueryWorkflowExecutionCommand.cs 和 Response
    - _需求：6.3, 6.4_

  - [ ]* 1.8 为 Shared 层编写单元测试
    - 测试枚举值
    - 测试模型序列化/反序列化
    - _需求：1.2_

- [ ] 2. 创建 Core 层 - 服务和业务逻辑
  - [x] 2.1 创建 MoAI.Workflow.Core 项目结构
    - 创建 Handlers、Services、Runtime 目录
    - 创建 WorkflowCoreModule.cs
    - _需求：6.1_

  - [x] 2.2 实现变量解析服务
    - 创建 VariableResolutionService.cs
    - 实现 ResolveVariable 方法（支持 sys.*、input.*、nodeKey.* 格式）
    - 实现 FlattenJson 方法（将嵌套 JSON 扁平化）
    - 实现数组访问支持（[0]、[*]）
    - _需求：2.2, 4.1, 4.2, 4.3_

  - [~]* 2.3 为变量解析服务编写属性测试
    - **属性 4：变量引用解析**
    - **属性 7：输出扁平化一致性**
    - **验证：需求 2.2, 4.1, 4.2, 4.3**

  - [x] 2.4 实现表达式评估服务
    - 创建 ExpressionEvaluationService.cs
    - 实现 Fixed 表达式评估
    - 实现 Variable 表达式评估
    - 实现 JsonPath 表达式评估
    - 实现 StringInterpolation 表达式评估
    - _需求：2.1, 2.3, 2.4_

  - [~]* 2.5 为表达式评估服务编写属性测试
    - **属性 5：JsonPath 表达式评估**
    - **属性 6：字符串插值替换**
    - **验证：需求 2.3, 2.4_

  - [x] 2.6 实现工作流定义服务
    - 创建 WorkflowDefinitionService.cs
    - 实现 ValidateWorkflowDefinition 方法（验证节点和连接）
    - 实现 ValidateNodeDesign 方法（验证节点配置）
    - 实现 ValidateConnections 方法（验证连接有效性）
    - 验证 Start/End 节点、图形连接、节点类型
    - 验证所有连接的源节点和目标节点都存在
    - 验证没有孤立节点和无效循环
    - _需求：1.1, 1.2, 1.3, 1.4, 2.5_

  - [~]* 2.7 为工作流定义服务编写属性测试
    - **属性 2：节点类型验证**
    - **属性 3：工作流图有效性**
    - **属性 14：输入验证**
    - **验证：需求 1.2, 1.3, 2.5**

  - [x] 2.8 实现节点运行时工厂
    - 创建 INodeRuntime.cs 接口
    - 创建 NodeExecutionResult.cs 模型
    - 创建 NodeRuntimeFactory.cs
    - 实现 GetRuntime 和 RegisterRuntime 方法
    - _需求：3.2, 5.1-5.8_

  - [x] 2.9 实现 Start 节点运行时
    - 创建 StartNodeRuntime.cs
    - 初始化工作流上下文
    - 返回启动参数作为输出
    - _需求：5.1_

  - [x] 2.10 实现 End 节点运行时
    - 创建 EndNodeRuntime.cs
    - 终止工作流执行
    - 返回最终输出
    - _需求：5.2_

  - [x] 2.11 实现 AiChat 节点运行时
    - 创建 AiChatNodeRuntime.cs
    - 调用 AI 模型服务
    - 处理模型响应
    - _需求：5.3_

  - [x] 2.12 实现 Wiki 节点运行时
    - 创建 WikiNodeRuntime.cs
    - 调用知识库搜索服务
    - 返回相关文档
    - _需求：5.4_

  - [x] 2.13 实现 Plugin 节点运行时
    - 创建 PluginNodeRuntime.cs
    - 调用插件执行服务
    - 处理插件输出
    - _需求：5.5_

  - [x] 2.14 实现 Condition 节点运行时
    - 创建 ConditionNodeRuntime.cs
    - 评估条件表达式
    - 返回条件结果
    - _需求：5.8, 3.5_

  - [x] 2.15 实现 ForEach 节点运行时
    - 创建 ForEachNodeRuntime.cs
    - 迭代集合
    - 为每个项目执行循环体
    - _需求：5.7, 3.6_

  - [x] 2.16 实现 Fork 节点运行时
    - 创建 ForkNodeRuntime.cs
    - 并行执行多个分支
    - 等待所有分支完成
    - _需求：5.8, 3.7_

  - [x] 2.17 实现 JavaScript 节点运行时
    - 创建 JavaScriptNodeRuntime.cs
    - 执行 JavaScript 代码
    - 提供工作流上下文访问
    - _需求：5.6_

  - [x] 2.18 实现 DataProcess 节点运行时
    - 创建 DataProcessNodeRuntime.cs
    - 实现 map、filter、aggregate 操作
    - _需求：5.7_

  - [ ]* 2.19 为节点运行时编写单元测试
    - 测试每个节点类型的执行
    - 测试错误处理
    - _需求：5.1-5.8_

  - [x] 2.20 实现工作流上下文管理器
    - 创建 WorkflowContextManager.cs
    - 实现上下文初始化
    - 实现上下文更新
    - 实现变量映射维护
    - _需求：4.4_

  - [x] 2.21 实现工作流运行时
    - 创建 WorkflowRuntime.cs
    - 实现 ExecuteAsync 方法（流式传输）
    - 实现节点顺序执行
    - 实现错误处理和恢复
    - _需求：3.1, 3.2, 3.3, 3.4_

  - [~]* 2.22 为工作流运行时编写属性测试
    - **属性 8：节点执行顺序**
    - **属性 9：条件路由正确性**
    - **属性 10：ForEach 循环完整性**
    - **属性 11：Fork 并行执行**
    - **属性 12：执行历史完整性**
    - **属性 13：错误捕获和报告**
    - **验证：需求 3.1-3.8**

  - [x] 2.23 实现工作流执行服务
    - 创建 WorkflowExecutionService.cs
    - 实现工作流执行逻辑
    - 实现执行历史存储
    - 实现错误处理
    - _需求：3.8, 8.4, 9.4_

  - [x] 2.24 实现 CreateWorkflowDefinitionCommand Handler
    - 创建 CreateWorkflowDefinitionCommandHandler.cs
    - 接收节点定义列表和连接列表（而非字符串 FunctionDesign）
    - 调用 WorkflowDefinitionService 验证工作流定义
    - 验证所有节点类型有效
    - 验证所有连接的源节点和目标节点存在
    - 验证节点配置满足要求
    - 存储到 WorkflowDesignEntity
    - _需求：1.1, 1.2, 1.3, 1.4, 6.1_

  - [x] 2.25 实现 UpdateWorkflowDefinitionCommand Handler
    - 创建 UpdateWorkflowDefinitionCommandHandler.cs
    - 接收节点定义列表和连接列表
    - 调用 WorkflowDefinitionService 重新验证工作流定义
    - 验证所有节点类型有效
    - 验证所有连接的源节点和目标节点存在
    - 验证节点配置满足要求
    - 更新 WorkflowDesignEntity
    - 创建 WorkflowDesignSnapshotEntity（版本历史）
    - _需求：1.4, 1.6, 6.1_

  - [x] 2.26 实现 DeleteWorkflowDefinitionCommand Handler
    - 创建 DeleteWorkflowDefinitionCommandHandler.cs
    - 软删除 WorkflowDesignEntity
    - 保留执行历史
    - _需求：1.6, 6.1_

  - [x] 2.27 实现 ExecuteWorkflowCommand Handler
    - 创建 ExecuteWorkflowCommandHandler.cs
    - 检索工作流定义
    - 调用 WorkflowRuntime
    - 流式传输结果
    - _需求：3.1-3.8, 6.2_

  - [x] 2.28 实现 QueryWorkflowDefinitionCommand Handler
    - 创建 QueryWorkflowDefinitionCommandHandler.cs
    - 查询 WorkflowDesignEntity
    - 返回工作流定义
    - _需求：1.4, 6.3_

  - [x] 2.29 实现 QueryWorkflowInstanceCommand Handler
    - 创建 QueryWorkflowInstanceCommandHandler.cs
    - 查询 WorkflowHistoryEntity
    - 返回执行历史
    - _需求：6.4_

  - [ ]* 2.30 为 Handlers 编写单元测试
    - 测试命令处理
    - 测试查询处理
    - 测试错误处理
    - _需求：6.1-6.4_

- [x] 3. 检查点 - 确保所有测试通过
  - 运行所有单元测试和属性测试
  - 验证代码覆盖率
  - 如有问题请咨询用户

- [x] 4. 创建 API 层 - 控制器和端点
  - [x] 4.1 创建 MoAI.Workflow.Api 项目结构
    - 创建 Controllers 目录
    - 创建 WorkflowApiModule.cs
    - _需求：7.1_

  - [x] 4.2 实现 WorkflowDefinition 控制器
    - 创建 WorkflowDefinitionController.cs
    - 实现 POST /api/workflow/definition（创建）
    - 实现 GET /api/workflow/definition/{id}（获取）
    - 实现 PUT /api/workflow/definition/{id}（更新）
    - 实现 DELETE /api/workflow/definition/{id}（删除）
    - 实现 GET /api/workflow/definition（列表）
    - _需求：7.1-7.5_

  - [x] 4.3 实现 WorkflowExecution 控制器
    - 创建 WorkflowExecutionController.cs
    - 实现 POST /api/workflow/execute（执行，SSE 流式传输）
    - 实现 GET /api/workflow/instance/{id}（获取执行历史）
    - 实现 GET /api/workflow/instance（列表）
    - _需求：7.6-7.8_

  - [ ]* 4.4 为 API 端点编写集成测试
    - **属性 15：API 端点响应一致性**
    - 测试所有端点
    - 测试错误响应
    - **验证：需求 7.1-7.8**

- [x] 5. 检查点 - 确保所有测试通过
  - 运行所有单元测试、属性测试和集成测试
  - 验证 API 端点功能
  - 如有问题请咨询用户

- [x] 6. 集成和验证
  - [x] 6.1 集成所有模块
    - 在 MoAI 主项目中注册 WorkflowCoreModule
    - 在 MoAI 主项目中注册 WorkflowApiModule
    - 验证依赖注入配置
    - _需求：6.1-6.6_

  - [x] 6.2 验证工作流定义往返一致性
    - **属性 1：工作流定义往返一致性**
    - 创建工作流、存储、检索、验证
    - **验证：需求 1.4**

  - [x] 6.3 验证数据持久化
    - 验证 WorkflowDesignEntity 存储
    - 验证 WorkflowDesignSnapshotEntity 版本历史
    - 验证 WorkflowHistoryEntity 执行历史
    - _需求：8.1-8.6_

  - [x] 6.4 验证错误处理和验证
    - 测试无效的工作流定义
    - 测试缺失的变量引用
    - 测试节点执行失败
    - _需求：9.1-9.5_

  - [~]* 6.5 编写端到端集成测试
    - 测试完整的工作流执行流程
    - 测试多个节点的数据流
    - 测试条件分支和循环
    - _需求：3.1-3.8_

- [x] 7. 最终检查点 - 确保所有测试通过
  - 运行所有测试（单元、属性、集成、端到端）
  - 验证代码质量和覆盖率
  - 如有问题请咨询用户

## 注意

- 标记为 `*` 的任务是可选的，可以跳过以加快 MVP 开发
- 每个任务都引用特定的需求以便追踪
- 检查点确保增量验证
- 属性测试验证通用正确性属性
- 单元测试验证特定示例和边界情况
