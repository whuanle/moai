# Requirements Document

## Introduction

本文档定义了工作流画布编辑器与后端 API 集成的需求。该功能将现有的基于 mock 数据的工作流画布编辑器与后端 API 连接，实现工作流的加载、保存和动态节点定义查询功能。

## Glossary

- **Workflow_Canvas**: 基于 @flowgram.ai/free-layout-editor 的可视化工作流编辑器
- **Backend_API**: 后端提供的工作流管理 API 接口
- **UiDesign**: 画布 UI 数据，包含节点位置、尺寸等可视化信息
- **FunctionDesign**: 后端逻辑数据，包含节点配置、连接逻辑等业务信息
- **Instance_Node**: 需要选择具体实例的节点类型（Plugin、Wiki、AiModel）
- **Static_Node**: 不需要实例的节点类型（Start、End、Condition、Fork、ForEach、AiChat、DataProcess、JavaScript）
- **Draft_Version**: 工作流的草稿版本，保存在 UiDesignDraft 和 FunctionDesignDraft 字段
- **Published_Version**: 工作流的已发布版本，保存在 UiDesign 和 FunctionDesign 字段
- **Node_Definition**: 节点的输入输出字段定义
- **API_Client**: 使用 Microsoft Kiota 生成的 TypeScript API 客户端

## Requirements

### Requirement 1: 加载工作流定义

**User Story:** 作为用户，我希望打开工作流编辑器时能够加载现有的工作流配置，以便继续编辑之前保存的工作流。

#### Acceptance Criteria

1. WHEN 用户打开工作流编辑器，THE System SHALL 调用 config API 获取工作流定义
2. WHEN API 返回工作流数据，THE System SHALL 将 UiDesign 和 FunctionDesign 转换为画布格式
3. WHEN 工作流为空或新建，THE System SHALL 初始化包含 Start 和 End 节点的默认工作流
4. WHEN 加载失败，THE System SHALL 显示错误信息并提供重试选项
5. WHEN 存在草稿版本，THE System SHALL 优先加载草稿版本而非已发布版本

### Requirement 2: 动态查询节点定义

**User Story:** 作为用户，我希望在选择具体的插件、知识库或 AI 模型后，系统能够自动获取该实例的输入输出字段定义，以便正确配置节点连接。

#### Acceptance Criteria

1. WHEN 用户为 Instance_Node 选择具体实例，THE System SHALL 调用 query_define API 获取节点定义
2. WHEN API 返回节点定义，THE System SHALL 更新节点的 inputFields 和 outputFields
3. WHEN 节点定义已缓存，THE System SHALL 使用缓存数据避免重复请求
4. WHEN 查询失败，THE System SHALL 显示错误信息但保持节点可编辑状态
5. WHEN 用户更改实例选择，THE System SHALL 清除旧定义并查询新定义

### Requirement 3: 保存工作流到后端

**User Story:** 作为用户，我希望能够保存工作流的修改，以便下次打开时继续使用。

#### Acceptance Criteria

1. WHEN 用户点击保存按钮，THE System SHALL 将画布数据转换为后端格式
2. WHEN 数据转换完成，THE System SHALL 调用 update API 保存工作流
3. WHEN 保存成功，THE System SHALL 显示成功提示并重置修改状态标记
4. WHEN 保存失败，THE System SHALL 显示错误信息并保持编辑状态
5. WHEN 工作流验证失败，THE System SHALL 阻止保存并显示验证错误

### Requirement 4: 节点布局和默认配置

**User Story:** 作为用户，我希望新添加的节点具有合理的默认配置和布局，以便快速构建工作流。

#### Acceptance Criteria

1. WHEN 添加新节点，THE System SHALL 应用该节点类型的默认布局配置
2. WHEN Instance_Node 未选择实例，THE System SHALL 显示占位符提示用户选择
3. WHEN Instance_Node 选择实例后，THE System SHALL 显示从 query_define 获取的实际字段
4. WHEN 节点位置冲突，THE System SHALL 自动调整新节点位置避免重叠
5. WHEN 复制节点，THE System SHALL 保留原节点配置但生成新的节点 ID

### Requirement 5: 数据格式转换

**User Story:** 作为系统，我需要在画布格式和后端格式之间正确转换数据，以确保数据一致性。

#### Acceptance Criteria

1. WHEN 转换为画布格式，THE System SHALL 将 FunctionDesign 和 UiDesign 合并为 WorkflowJSON
2. WHEN 转换为后端格式，THE System SHALL 将 WorkflowJSON 分离为 FunctionDesign 和 UiDesign
3. WHEN 数据不一致，THE System SHALL 记录验证错误并尝试修复
4. WHEN 节点 ID 不匹配，THE System SHALL 报告数据一致性错误
5. WHEN 连接引用不存在的节点，THE System SHALL 移除无效连接

### Requirement 6: 错误处理和用户反馈

**User Story:** 作为用户，我希望在操作失败时能够看到清晰的错误信息，以便了解问题并采取相应措施。

#### Acceptance Criteria

1. WHEN API 调用失败，THE System SHALL 使用 proxyRequestError 处理错误
2. WHEN 网络错误发生，THE System SHALL 显示友好的错误提示
3. WHEN 保存操作进行中，THE System SHALL 显示加载状态并禁用保存按钮
4. WHEN 验证错误发生，THE System SHALL 高亮显示有问题的节点
5. WHEN 用户尝试离开未保存的工作流，THE System SHALL 显示确认对话框

### Requirement 7: 缓存和性能优化

**User Story:** 作为用户，我希望编辑器响应迅速，避免不必要的 API 调用。

#### Acceptance Criteria

1. WHEN 节点定义已查询过，THE System SHALL 缓存定义避免重复请求
2. WHEN 多个节点使用相同实例，THE System SHALL 共享缓存的定义数据
3. WHEN 工作流数据较大，THE System SHALL 使用增量更新而非全量替换
4. WHEN 用户快速操作，THE System SHALL 防抖保存请求避免频繁调用
5. WHEN 缓存过期，THE System SHALL 自动清除并重新查询

### Requirement 8: 工作流版本管理

**User Story:** 作为用户，我希望能够区分草稿版本和已发布版本，以便在测试和生产环境中使用不同版本。

#### Acceptance Criteria

1. WHEN 保存工作流，THE System SHALL 保存到草稿字段（UiDesignDraft、FunctionDesignDraft）
2. WHEN 加载工作流，THE System SHALL 优先加载草稿版本
3. WHEN 草稿不存在，THE System SHALL 加载已发布版本
4. WHEN 显示版本状态，THE System SHALL 标识当前是草稿还是已发布版本
5. WHEN 用户发布工作流，THE System SHALL 将草稿复制到已发布字段

### Requirement 9: 批量节点定义查询

**User Story:** 作为系统，我需要支持批量查询多个节点的定义，以提高加载性能。

#### Acceptance Criteria

1. WHEN 加载包含多个 Instance_Node 的工作流，THE System SHALL 批量查询所有节点定义
2. WHEN 批量查询返回，THE System SHALL 将定义分配给对应的节点
3. WHEN 部分查询失败，THE System SHALL 仅更新成功的节点定义
4. WHEN 查询请求过大，THE System SHALL 分批发送请求
5. WHEN 所有查询完成，THE System SHALL 触发画布重新渲染

### Requirement 10: 数据持久化和状态同步

**User Story:** 作为用户，我希望编辑器能够正确跟踪修改状态，以便知道何时需要保存。

#### Acceptance Criteria

1. WHEN 用户修改工作流，THE System SHALL 设置 isDirty 标记为 true
2. WHEN 保存成功，THE System SHALL 重置 isDirty 标记为 false
3. WHEN 画布数据变更，THE System SHALL 同步更新 Zustand store
4. WHEN store 数据变更，THE System SHALL 触发画布重新渲染
5. WHEN 数据不一致，THE System SHALL 记录警告并尝试自动修复
