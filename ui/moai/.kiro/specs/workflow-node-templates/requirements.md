# 工作流节点模板 - 需求文档

## 1. 概述

为工作流编排器实现节点模板系统，允许用户从左侧面板拖拽不同类型的节点到画布上。每个节点类型基于后端 API 定义的 `NodeType` 枚举。

## 2. 用户故事

### 2.1 作为用户，我希望能够看到所有可用的节点类型
**验收标准：**
- 左侧面板显示所有节点类型的列表
- 每个节点类型显示图标、名称和描述
- 节点按类别分组（控制流、数据处理、AI、集成等）

### 2.2 作为用户，我希望能够拖拽节点到画布上
**验收标准：**
- 可以从左侧面板拖拽节点模板
- 拖拽时显示节点预览
- 释放鼠标时在画布上创建节点实例
- 新创建的节点自动生成唯一 ID

### 2.3 作为用户，我希望不同类型的节点有不同的视觉样式
**验收标准：**
- 每个节点类型有独特的颜色主题
- 节点显示对应的图标
- 节点标题栏显示节点类型名称

## 3. 节点类型定义

基于后端 API 的 `NodeType` 枚举：

### 3.1 控制流节点
- **Start** (start) - 工作流开始节点
- **End** (end) - 工作流结束节点
- **Condition** (condition) - 条件判断节点
- **Fork** (fork) - 并行分支节点
- **ForEach** (forEach) - 循环遍历节点

### 3.2 AI 节点
- **AiChat** (aiChat) - AI 对话节点，支持多种 AI 模型

### 3.3 数据节点
- **DataProcess** (dataProcess) - 数据处理节点
- **JavaScript** (javaScript) - JavaScript 代码执行节点

### 3.4 集成节点
- **Plugin** (plugin) - 插件调用节点
- **Wiki** (wiki) - 知识库查询节点

## 4. 节点字段定义

每个节点包含以下基础字段（基于 `NodeDefineItem`）：

### 4.1 基础属性
- `nodeType`: NodeType - 节点类型
- `nodeTypeName`: string - 节点类型显示名称
- `description`: string - 节点描述
- `icon`: string - 节点图标
- `color`: string - 节点颜色

### 4.2 输入输出
- `inputFields`: FieldDefine[] - 输入字段定义
- `outputFields`: FieldDefine[] - 输出字段定义
- `supportsStreaming`: boolean - 是否支持流式输出

### 4.3 特定节点属性
- **AiChat 节点**:
  - `modelId`: number - AI 模型 ID
  - `modelName`: string - AI 模型名称
  
- **Plugin 节点**:
  - `pluginId`: number - 插件 ID
  - `pluginName`: string - 插件名称
  
- **Wiki 节点**:
  - `wikiId`: number - 知识库 ID
  - `wikiName`: string - 知识库名称

## 5. 字段类型

基于 `FieldType` 枚举：
- `empty` - 空类型
- `string` - 字符串
- `number` - 数字
- `boolean` - 布尔值
- `object` - 对象
- `array` - 数组
- `dynamic` - 动态类型

## 6. 技术约束

- 使用 `@flowgram.ai/free-layout-editor` 作为画布引擎
- 节点数据结构需要兼容后端 API 格式
- 支持节点的拖拽、连接、编辑功能
- 节点 ID 使用 `{nodeType}_{timestamp}` 格式

## 7. 非功能需求

### 7.1 性能
- 画布应支持至少 100 个节点而不卡顿
- 拖拽操作应流畅（60fps）

### 7.2 可用性
- 节点面板应可折叠/展开
- 支持搜索节点类型
- 提供节点使用提示

### 7.3 可扩展性
- 节点类型定义应易于扩展
- 支持自定义节点模板
