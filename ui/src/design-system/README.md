# MoAI Design System

`@/design-system` 是 MoAI 前端设计系统的唯一公共出口。

## 组成
- `theme`：主题 tokens、预设注册、antd 配置。
- `components`：有主见的强约束共享组件。
- `templates`：可复制的页面骨架示例。

## 约定
- 业务页面一律从 `@/design-system` 导入组件，禁止直接散落 antd 原始 Table。
- 颜色/间距一律取 token，禁止 magic number。
- 页头工具行（筛选/操作按钮）统一用 `PageToolbar`：有筛选则筛选在左、操作在右，无筛选则操作左对齐。详见 `ui/docs/design-system/pages.md`。
- 主题切换由 store 的 `themeKey` 驱动，详见 `ui/docs/design-system/theming.md`。
