# 工作流路由修复

## 问题
将 `workflow-new` 目录重命名为 `workflow` 后，路由导入失败。

## 修复内容

### 1. TeamPageRouter.tsx
```typescript
// 修改前
import WorkflowConfig from "./apps/workflow/WorkflowConfig";

// 修改后
import { WorkflowEditor } from "./apps/workflow";
```

路由配置：
```typescript
{
  path: "apps/:appId/workflow",
  Component: WorkflowEditor,  // 原来是 WorkflowConfig
}
```

### 2. App.tsx
```typescript
// 修改前
import WorkflowConfig from "./components/team/apps/workflow/WorkflowConfig";
import ChatAppConfig from "./components/team/apps/chatapp/ChatAppConfig"; // 重复导入

// 修改后
import { WorkflowEditor } from "./components/team/apps/workflow";
```

路由配置：
```typescript
<Route path=":id/manage_app/workflow/:appId" element={<WorkflowEditor />} />
```

## 工作流目录结构

```
src/components/team/apps/workflow/
├── api.ts                  # API 服务
├── ConfigPanel.tsx         # 配置面板
├── constants.ts            # 常量定义
├── index.ts               # 模块导出（导出 WorkflowEditor）
├── NodePanel.tsx          # 节点面板
├── README.md              # 文档
├── store.ts               # Zustand 状态管理
├── Toolbar.tsx            # 工具栏
├── types.ts               # 类型定义
├── utils.ts               # 工具函数
├── WorkflowEditor.tsx     # 主编辑器组件
└── *.css                  # 样式文件
```

## 访问路径

- 团队工作流编辑器：`/app/team/:id/apps/:appId/workflow`
- 独立工作流编辑器：`/app/team/:id/manage_app/workflow/:appId`

## 验证

路由修复后，应该能够：
1. 正常访问工作流编辑器页面
2. 创建和编辑节点
3. 连接节点
4. 保存工作流（nextNodeKeys 正确填充）
5. 重新加载后画布状态正确还原
