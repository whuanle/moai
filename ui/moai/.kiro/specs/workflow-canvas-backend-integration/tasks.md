# Implementation Plan: 工作流画布后端集成

## Overview

本实现计划将工作流画布编辑器与后端 API 集成，实现工作流的加载、保存和动态节点定义查询功能。实现将按照增量方式进行，每个任务都构建在前一个任务的基础上，确保功能逐步完善并及时验证。

## Tasks

- [x] 1. 创建 API 服务层
  - [x] 1.1 实现 WorkflowApiService 类
    - 创建 `src/components/team/apps/workflow/workflowApiService.ts`
    - 实现 loadWorkflow 方法（调用 config API）
    - 实现 saveWorkflow 方法（调用 update API）
    - 实现节点定义缓存机制（Map 结构）
    - 添加 TypeScript 类型定义
    - _Requirements: 1.1, 1.2, 3.1, 3.2, 7.1_
  
  - [ ]* 1.2 编写 API 服务单元测试
    - 测试 loadWorkflow 成功场景
    - 测试 saveWorkflow 成功场景
    - 测试 API 错误处理
    - 测试缓存键生成逻辑
    - _Requirements: 1.1, 3.2, 6.1_
  
  - [x] 1.3 实现节点定义查询方法
    - 实现 queryNodeDefinitions 批量查询方法
    - 实现 queryNodeDefinition 单个查询方法
    - 实现缓存检查和更新逻辑
    - 添加缓存清除方法
    - _Requirements: 2.1, 2.3, 7.1, 9.1_
  
  - [ ]* 1.4 编写节点定义查询属性测试
    - **Property 3: 节点定义缓存避免重复请求**
    - **Validates: Requirements 2.3, 7.1**
    - 使用 fast-check 生成随机节点查询
    - 验证相同查询使用缓存
    - _Requirements: 2.3, 7.1_

- [x] 2. 扩展数据转换器
  - [x] 2.1 实现后端格式转换函数
    - 在 `workflowConverter.ts` 中添加 parseFunctionDesign 函数
    - 添加 toFunctionDesign 函数
    - 添加 parseUiDesign 函数
    - 添加 toUiDesign 函数
    - 处理 JSON 解析错误
    - _Requirements: 1.2, 3.1, 5.1, 5.2_
  
  - [ ]* 2.2 编写数据转换属性测试
    - **Property 1: 数据转换往返一致性**
    - **Validates: Requirements 1.2, 3.1, 5.1, 5.2**
    - 使用 fast-check 生成随机工作流数据
    - 验证往返转换保持数据一致性
    - _Requirements: 1.2, 3.1, 5.1, 5.2_
  
  - [x] 2.3 实现 NodeDesign 和 Connection 转换
    - 添加 convertToNodeDesigns 辅助函数
    - 添加 convertToConnections 辅助函数
    - 处理字段映射和类型转换
    - _Requirements: 3.1, 5.2_
  
  - [ ]* 2.4 编写转换函数单元测试
    - 测试空工作流转换
    - 测试单节点工作流转换
    - 测试复杂工作流转换
    - 测试错误数据处理
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 3. Checkpoint - 确保数据转换和 API 服务正常工作
  - 运行所有测试确保通过
  - 验证 API 服务可以正确调用后端接口
  - 验证数据转换往返一致性
  - 如有问题请询问用户


- [x] 4. 扩展 Zustand Store
  - [x] 4.1 添加 API 集成状态字段
    - 在 `workflowStore.ts` 中添加 appId、teamId 字段
    - 添加 isLoading、loadError 字段
    - 添加 isDraft 字段
    - 添加 apiService 实例字段
    - 更新 WorkflowState 接口
    - _Requirements: 1.4, 6.2, 8.4_
  
  - [x] 4.2 实现 loadFromApi 方法
    - 在 `useWorkflowStore.ts` 中实现 loadFromApi
    - 调用 apiService.loadWorkflow
    - 解析并设置 backend 和 canvas 数据
    - 处理空工作流初始化（Start + End 节点）
    - 设置加载状态和错误状态
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  
  - [ ]* 4.3 编写 loadFromApi 单元测试
    - 测试成功加载场景
    - 测试空工作流初始化
    - 测试草稿版本优先加载
    - 测试加载失败处理
    - _Requirements: 1.1, 1.3, 1.5_
  
  - [ ]* 4.4 编写草稿优先加载属性测试
    - **Property 2: 草稿版本优先加载**
    - **Validates: Requirements 1.5, 8.2**
    - 生成包含草稿和已发布版本的数据
    - 验证总是加载草稿版本
    - _Requirements: 1.5, 8.2_
  
  - [x] 4.5 实现 saveToApi 方法
    - 实现 saveToApi 方法
    - 调用 validate 验证工作流
    - 转换数据为后端格式
    - 调用 apiService.saveWorkflow
    - 更新 isDirty 和 isSaving 状态
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  
  - [ ]* 4.6 编写 saveToApi 单元测试
    - 测试成功保存场景
    - 测试验证失败阻止保存
    - 测试保存失败处理
    - 测试 isDirty 重置
    - _Requirements: 3.3, 3.4, 3.5_

- [x] 5. 实现节点定义查询功能
  - [x] 5.1 添加节点定义查询方法到 Store
    - 实现 queryAndUpdateNodeDefinition 方法
    - 实现 batchQueryNodeDefinitions 方法
    - 更新节点的 inputFields 和 outputFields
    - 处理查询失败情况
    - _Requirements: 2.1, 2.2, 2.4, 9.1, 9.2_
  
  - [ ]* 5.2 编写节点定义更新属性测试
    - **Property 4: 节点定义更新字段**
    - **Validates: Requirements 2.2**
    - 生成随机节点定义响应
    - 验证字段正确更新到节点
    - _Requirements: 2.2_
  
  - [ ]* 5.3 编写批量查询分配属性测试
    - **Property 12: 批量查询结果正确分配**
    - **Validates: Requirements 9.2**
    - 生成多个节点的批量查询
    - 验证每个定义分配给正确的节点
    - _Requirements: 9.2_
  
  - [x] 5.4 实现实例变更处理
    - 添加节点实例变更监听
    - 清除旧定义缓存
    - 查询新实例定义
    - 更新节点配置
    - _Requirements: 2.5_
  
  - [ ]* 5.5 编写实例变更属性测试
    - **Property 5: 实例变更清除旧定义**
    - **Validates: Requirements 2.5**
    - 模拟实例变更操作
    - 验证旧定义被清除且新定义被查询
    - _Requirements: 2.5_

- [x] 6. Checkpoint - 确保 Store 集成和节点定义查询正常工作
  - 运行所有测试确保通过
  - 验证 Store 可以正确加载和保存工作流
  - 验证节点定义查询和缓存机制
  - 如有问题请询问用户


- [x] 7. 集成 React 组件
  - [x] 7.1 更新 WorkflowConfig 组件
    - 修改 `WorkflowConfig.tsx` 添加 appId 和 teamId props
    - 添加 useEffect 在组件挂载时加载工作流
    - 实现 loadWorkflowData 函数
    - 添加加载状态渲染（Spin 组件）
    - 添加错误状态渲染（Empty + 重试按钮）
    - _Requirements: 1.1, 1.4, 6.2_
  
  - [x] 7.2 实现保存功能
    - 添加 handleSave 函数
    - 调用 store.saveToApi
    - 显示保存成功/失败消息
    - 更新工具栏保存按钮状态
    - 添加草稿/未保存标签显示
    - _Requirements: 3.2, 3.3, 3.4, 6.3, 8.4_
  
  - [x] 7.3 实现节点实例选择处理
    - 添加 handleNodeInstanceChange 函数
    - 调用 store.queryAndUpdateNodeDefinition
    - 显示查询成功/失败消息
    - 更新节点配置面板
    - _Requirements: 2.1, 2.2, 2.4_
  
  - [x] 7.4 添加未保存更改警告
    - 实现 beforeunload 事件监听
    - 检查 isDirty 状态
    - 显示确认对话框
    - _Requirements: 6.5_
  
  - [ ]* 7.5 编写组件集成测试
    - 测试组件挂载时加载工作流
    - 测试保存按钮点击
    - 测试错误状态显示
    - 测试加载状态显示
    - _Requirements: 1.1, 3.2, 6.2, 6.3_

- [x] 8. 实现缓存优化
  - [x] 8.1 添加缓存共享逻辑
    - 实现多节点共享缓存机制
    - 优化缓存键生成策略
    - 添加缓存统计日志
    - _Requirements: 7.2_
  
  - [ ]* 8.2 编写缓存共享属性测试
    - **Property 10: 多节点共享缓存定义**
    - **Validates: Requirements 7.2**
    - 创建多个使用相同实例的节点
    - 验证它们共享同一份缓存数据
    - _Requirements: 7.2_
  
  - [x] 8.3 实现保存防抖
    - 使用 lodash.debounce 或自定义防抖函数
    - 设置防抖延迟（500ms）
    - 确保最后一次保存请求被执行
    - _Requirements: 7.4_
  
  - [ ]* 8.4 编写防抖属性测试
    - **Property 11: 快速操作防抖保存**
    - **Validates: Requirements 7.4**
    - 模拟快速连续保存请求
    - 验证只执行最后一次请求
    - _Requirements: 7.4_
  
  - [x] 8.5 实现缓存过期机制
    - 添加缓存时间戳记录
    - 实现缓存过期检查
    - 自动清除过期缓存
    - _Requirements: 7.5_

- [x] 9. 实现批量查询优化
  - [x] 9.1 添加批量查询分批逻辑
    - 检测查询数量是否超过阈值（20 个）
    - 自动分批发送请求
    - 合并批量查询结果
    - _Requirements: 9.4_
  
  - [ ]* 9.2 编写分批查询属性测试
    - **Property 13: 大批量请求自动分批**
    - **Validates: Requirements 9.4**
    - 生成超过阈值的查询请求
    - 验证自动分批发送
    - _Requirements: 9.4_
  
  - [x] 9.3 实现部分失败处理
    - 使用 Promise.allSettled 处理批量请求
    - 仅更新成功的节点定义
    - 记录失败的查询
    - 显示部分成功提示
    - _Requirements: 9.3_
  
  - [x] 9.4 优化加载时批量查询
    - 在 loadFromApi 后自动批量查询所有实例节点
    - 收集所有需要查询的节点
    - 调用 batchQueryNodeDefinitions
    - 触发画布重新渲染
    - _Requirements: 9.1, 9.5_

- [x] 10. Checkpoint - 确保所有功能集成完成
  - 运行所有测试确保通过
  - 手动测试完整工作流（加载、编辑、保存）
  - 验证缓存和批量查询优化
  - 验证错误处理和用户反馈
  - 如有问题请询问用户


- [x] 11. 实现数据一致性和状态同步
  - [x] 11.1 添加数据一致性验证
    - 实现 validateDataConsistency 增强版本
    - 检查节点 ID 匹配
    - 检查连接引用有效性
    - 自动修复可修复的不一致
    - _Requirements: 5.3, 5.4, 5.5_
  
  - [ ]* 11.2 编写无效连接移除属性测试
    - **Property 9: 无效连接自动移除**
    - **Validates: Requirements 5.5**
    - 生成包含无效连接的工作流
    - 验证无效连接被自动移除
    - _Requirements: 5.5_
  
  - [x] 11.3 实现画布变更同步
    - 监听画布编辑器的 onContentChange 事件
    - 调用 syncEditorChanges 转换数据
    - 更新 Zustand store
    - 设置 isDirty 标记
    - _Requirements: 10.3_
  
  - [ ]* 11.4 编写画布同步属性测试
    - **Property 15: 画布变更同步 Store**
    - **Validates: Requirements 10.3**
    - 模拟画布数据变更
    - 验证 Store 被正确更新
    - _Requirements: 10.3_
  
  - [x] 11.5 实现 Store 变更触发渲染
    - 使用 Zustand 的订阅机制
    - 监听 backend 和 canvas 数据变更
    - 触发画布编辑器重新渲染
    - 避免循环更新
    - _Requirements: 10.4_
  
  - [ ]* 11.6 编写 Store 渲染触发属性测试
    - **Property 16: Store 变更触发渲染**
    - **Validates: Requirements 10.4**
    - 模拟 Store 数据变更
    - 验证画布重新渲染
    - _Requirements: 10.4_
  
  - [x] 11.7 实现修改操作脏标记
    - 在所有修改操作中设置 isDirty = true
    - 包括 addNode、deleteNode、updateNodeConfig 等
    - 在 saveToApi 成功后重置 isDirty
    - _Requirements: 10.1, 10.2_
  
  - [ ]* 11.8 编写脏标记属性测试
    - **Property 14: 修改操作设置脏标记**
    - **Validates: Requirements 10.1**
    - 执行各种修改操作
    - 验证 isDirty 被设置为 true
    - _Requirements: 10.1_

- [x] 12. 实现节点默认配置和布局
  - [x] 12.1 增强节点添加逻辑
    - 在 addNode 中应用节点模板默认配置
    - 设置默认位置、尺寸
    - 设置默认字段定义
    - _Requirements: 4.1_
  
  - [ ]* 12.2 编写节点默认配置属性测试
    - **Property 6: 节点添加应用默认配置**
    - **Validates: Requirements 4.1**
    - 测试所有节点类型
    - 验证默认配置被正确应用
    - _Requirements: 4.1_
  
  - [x] 12.3 实现节点位置冲突检测
    - 添加位置重叠检测函数
    - 实现自动位置调整算法
    - 在 addNode 和 copyNode 中应用
    - _Requirements: 4.4_
  
  - [ ]* 12.4 编写位置冲突属性测试
    - **Property 7: 节点位置冲突自动调整**
    - **Validates: Requirements 4.4**
    - 生成重叠位置的节点
    - 验证位置被自动调整
    - _Requirements: 4.4_
  
  - [x] 12.5 增强节点复制逻辑
    - 确保复制节点生成新 ID
    - 保留所有配置和字段定义
    - 应用位置偏移
    - _Requirements: 4.5_
  
  - [ ]* 12.6 编写节点复制属性测试
    - **Property 8: 节点复制保留配置生成新 ID**
    - **Validates: Requirements 4.5**
    - 复制各种类型的节点
    - 验证 ID 唯一且配置保留
    - _Requirements: 4.5_

- [x] 13. 完善错误处理和用户反馈
  - [x] 13.1 统一错误处理
    - 确保所有 API 调用使用 proxyRequestError
    - 添加详细的错误日志
    - 分类错误类型（网络、验证、服务器）
    - _Requirements: 6.1, 6.2_
  
  - [x] 13.2 实现验证错误高亮
    - 在验证失败时高亮有问题的节点
    - 显示具体的验证错误信息
    - 提供修复建议
    - _Requirements: 6.4_
  
  - [x] 13.3 优化加载状态显示
    - 添加全局加载遮罩
    - 显示加载进度提示
    - 添加节点定义查询加载状态
    - _Requirements: 6.3_
  
  - [ ]* 13.4 编写错误处理集成测试
    - 测试各种错误场景
    - 验证错误消息正确显示
    - 验证错误恢复机制
    - _Requirements: 6.1, 6.2, 6.4_

- [x] 14. Final Checkpoint - 完整功能验证
  - 运行所有单元测试和属性测试
  - 手动测试完整用户流程
  - 验证所有需求都已实现
  - 检查代码质量和文档
  - 如有问题请询问用户

## Notes

- 标记 `*` 的任务为可选测试任务，可以跳过以加快 MVP 开发
- 每个任务都引用了具体的需求编号以确保可追溯性
- Checkpoint 任务确保增量验证和及时发现问题
- 属性测试使用 fast-check 库，每个测试至少运行 100 次迭代
- 所有 API 调用都使用 proxyRequestError 进行错误处理
- 组件使用 Ant Design 5 的 UI 组件和 MoAI 样式规范
