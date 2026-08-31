# MoAI 设计系统

> 做业务页面前必读本文档与下列分册。设计系统代码统一从 `@/design-system` 导入。

## 必读顺序
1. `tokens.md` —— 令牌与取用规则
2. `components.md` —— 组件与组合约束矩阵
3. `pages.md` —— 页面原型约束与自查清单
4. `theming.md` —— 主题切换与新增预设

## 目录索引
- 代码：`ui/src/design-system/`
- 模板活例子：`ui/src/design-system/templates/`
- 根规范：`../frontend-conventions.md`

## 核心红线
- 页面禁止直接 `import { Table } from 'antd'`，必须用 `@/design-system` 的 `DataTable`。
- 颜色/间距一律取 token，禁止魔法值与 `#hex` 硬编码。
- 文案一律走 `useTranslation()`，禁止硬编码。
