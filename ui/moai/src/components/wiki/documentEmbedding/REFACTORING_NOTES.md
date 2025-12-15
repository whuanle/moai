# DocumentEmbedding 重构说明

## 重构目标
1. 代码结构优化 - 按功能模块拆分
2. 可读性提升 - 清晰注释、语义化命名
3. 性能优化 - 减少重复计算、冗余请求
4. 规范兼容 - ESLint/Prettier
5. 健壮性增强 - 边界条件处理
6. 可维护性提升 - 提取通用逻辑
7. 统一异常处理 - 使用 proxyRequestError

## 文件结构
```
documentEmbedding/
├── constants.ts          # 常量定义
├── types.ts              # 类型定义
├── hooks/                # 自定义Hooks
│   ├── useDocumentInfo.ts
│   ├── useTaskList.ts
│   ├── usePartitionPreview.ts
│   ├── usePartitionOperations.ts
│   ├── useEmbeddingOperations.ts
│   ├── useAiModelList.ts
│   └── index.ts
├── components/           # 子组件
│   ├── ChunkEditModal.tsx
│   ├── BatchGenerateModal.tsx
│   ├── TaskStatusTag.tsx
│   ├── PartitionPreviewCard.tsx
│   └── index.ts
├── utils/               # 工具函数
│   └── aiGenerationHelper.ts
└── DocumentEmbedding.tsx # 主组件（待重构）
```

## 核心优化点

### 1. 模块化拆分
- 将2857行的单文件拆分为多个模块
- 每个模块职责单一，便于维护

### 2. 统一异常处理
- 所有catch块使用 proxyRequestError
- 移除直接使用 messageApi.error 的异常处理

### 3. 性能优化
- 使用 useCallback 缓存函数
- 使用 useMemo 缓存计算结果
- 减少不必要的重新渲染

### 4. 代码复用
- 提取通用逻辑为工具函数
- 提取重复组件为独立组件

## 待完成工作
- [ ] 完成主组件重构
- [ ] 修复所有lint错误
- [ ] 测试所有功能
- [ ] 性能测试

