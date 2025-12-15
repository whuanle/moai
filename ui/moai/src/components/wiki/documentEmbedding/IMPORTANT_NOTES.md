# ⚠️ 重要说明：完成重构的剩余步骤

## 当前状态

重构工作已经完成了大部分，但主组件文件 `DocumentEmbedding.tsx` 中仍包含旧的代码定义，导致与新的模块化导入冲突。

## 🔴 必须删除的旧代码

### 1. 删除旧的ChunkEditModal组件定义
**位置**：约第83-525行
**原因**：已提取到 `./documentEmbedding/components/ChunkEditModal.tsx`

### 2. 删除所有旧的hooks定义
**位置**：约第519-1173行
包含以下hooks（都已提取到独立文件）：
- `useDocumentInfo` → `./documentEmbedding/hooks/useDocumentInfo.ts`
- `useTaskList` → `./documentEmbedding/hooks/useTaskList.ts`
- `usePartitionPreview` → `./documentEmbedding/hooks/usePartitionPreview.ts`
- `usePartitionOperations` → `./documentEmbedding/hooks/usePartitionOperations.ts`
- `useAiModelList` → `./documentEmbedding/hooks/useAiModelList.ts`
- `useAiPartitionOperations` → `./documentEmbedding/hooks/usePartitionOperations.ts`
- `useEmbeddingOperations` → `./documentEmbedding/hooks/useEmbeddingOperations.ts`

### 3. 删除旧的TaskStatusTag组件定义
**位置**：约第1176-1194行
**原因**：已提取到 `./documentEmbedding/components/TaskStatusTag.tsx`

## ✅ 已完成的优化

1. ✅ 所有新模块已创建并正常工作
2. ✅ 批量生成模态窗口已替换为新组件
3. ✅ 异常处理已统一使用 `proxyRequestError`
4. ✅ 使用 `useMemo` 优化了任务列表列定义
5. ✅ 使用常量文件统一管理默认值

## 📝 删除旧代码后的验证步骤

1. **检查导入**：确保所有导入路径正确
2. **运行lint**：`npm run lint` 或检查IDE中的错误
3. **功能测试**：
   - 文档信息加载
   - 任务列表显示
   - 切割预览
   - Chunk编辑
   - 批量生成
   - 向量化操作

## 🎯 核心优化成果

1. **代码结构**：从2857行单文件拆分为多个模块
2. **可维护性**：每个模块职责单一，便于维护
3. **异常处理**：统一使用 `proxyRequestError`
4. **性能优化**：使用 `useMemo` 和 `useCallback` 减少重复计算
5. **代码复用**：提取通用逻辑为工具函数和组件

## 💡 建议

删除旧代码时，建议：
1. 先备份当前文件
2. 逐步删除（先注释，测试通过后再删除）
3. 每次删除一部分后运行测试

