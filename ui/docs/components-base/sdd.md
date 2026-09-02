# 前端基础组件（components-base）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[frontend-conventions.md](../frontend-conventions.md)、[design-system/components.md](../design-system/components.md)、[../theme/sdd.md](../theme/sdd.md)（令牌） ｜ 下游：[../../docs/components-form/sdd.md](../../docs/components-form/sdd.md)、[../../docs/layout-routing/sdd.md](../../docs/layout-routing/sdd.md) ｜ 证据：[components 单测](../../src/design-system/components/)
> 规范：[DOC-STANDARD](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-CB-Sxx），本文不重复。

## 目标

把「页面不重复标题、内容撑满宽度、表格操作栏左对齐、文案走 i18n、颜色走 token」等红线固化在组件层，业务页只通过 props 表达差异。表单/反馈类组件见 [components-form](../../docs/components-form/sdd.md)。

## 结构

`ui/src/design-system/components/{Page,Card,DataTable,QueryBar,PageToolbar}/`（各含 index.ts 与 `__tests__/`），经 `@/design-system` 桶导出，业务页一律从桶导入。

## 组件契约

### Page（页面根容器）

| Prop | 必填 | 说明 |
|---|---|---|
| title / subtitle | 否 | Typography.Title level=3（20px）/ Text secondary（14px） |
| breadcrumb | 否 | antd Breadcrumb items，位于头部最上方 |
| extra | 否 | 头部右侧区（与标题 space-between） |
| children | 否 | 页面主体 |

- 头部惰性渲染：四者全不传则不渲染任何头部（[@FE-CB-S3](./bdd.md#fe-cb-s3)），头部 marginBottom 24。
- 根容器 width:100%，**无 maxWidth 收口、无居中**——业务页不得自行加 maxWidth 或 `margin: '0 auto'`（[@FE-CB-S3](./bdd.md#fe-cb-s3)）。
- 组合约束（components.md）：只允许做页面根节点，禁止嵌套于卡片/表格内。

### Card / StatCard

- **Card**：antd Card 透传封装，无必填 props。固定 borderless 变体 + `1px solid rgba(16,24,40,.08)` 边框 + `0 1px 2px rgba(16,24,40,.05)` 阴影；body padding 20、header 底边框 600 字重；调用方 style/styles 后到覆盖，其余 CardProps 原样透传（[@FE-CB-S4](./bdd.md#fe-cb-s4)/[@FE-CB-S5](./bdd.md#fe-cb-s5)）。
- **StatCard**（基于 Card）：必填 title/value；可选 icon（44×44 圆角 10 容器）/suffix/loading（透传骨架）/trend（number：0/正/负 → Minus/Rise/Fall 图标 + 绝对值百分数，次要色文字）。左（title/value/trend）右（icon）space-between。

### DataTable（全仓库唯一合法 Table 出口）

| Prop | 必填 | 说明 |
|---|---|---|
| columns / dataSource | 是 | antd 同构 |
| rowKey | 否 | **默认 'id'**（业务语义默认值，非 antd 索引 key） |
| pagination | 否 | false 关闭；否则合并默认 showSizeChanger + showTotal（`ds.table.total` 插值） |
| toolbar / onRefresh / refreshLoading | 否 | 工具区自定义节点 / 刷新按钮及回调 / 刷新 loading |

- 红线：业务页禁止直接 `import { Table } from 'antd'`。
- 工具区仅当 `toolbar || onRefresh` 存在才渲染；左对齐 + gap 16（不用 space-between）；按钮包 ConfigProvider 关闭 autoInsertSpace。
- 其余 TableProps（loading/scroll/rowSelection/onChange 等）经 `...rest` 透传。

### QueryBar（独立查询表单）

| Prop | 必填 | 说明 |
|---|---|---|
| form | 否 | 受控实例；缺省内部 Form.useForm() |
| onSearch / onReset | 否 | 查询回调（带当前字段值）/ 重置回调（字段已被 resetFields 清空后触发，调用方负责重拉列表） |
| loading | 否 | 「查询」按钮 loading |
| children | 否 | Form.Item 筛选字段（inline 布局） |

- 「查询」为 primary submit 按钮，点击与输入框回车两条提交路径等效（preventDefault 后手动取字段值，[@FE-CB-S14](./bdd.md#fe-cb-s14)）。
- 组合约束：用于列表页顶部 / Page 内；禁止放弹窗、详情页。

### PageToolbar（页头工具行）

| Prop | 必填 | 说明 |
|---|---|---|
| filters | 否 | 左侧筛选区（flex:1 + minWidth:0，可换行） |
| actions | 否 | 操作按钮组；有 filters 靠右（flexShrink 0），无 filters 直接左对齐 |

- 禁止手写 `Space` 拼左右两段，一律走两个插槽。

## i18n 键（zh-CN/common.json，en-US 同步）

| 键 | 值 | 使用处 |
|---|---|---|
| ds.query.search / ds.query.reset | 查询 / 重置 | QueryBar |
| ds.table.refresh / ds.table.total | 刷新 / 共 {{total}} 条 | DataTable |

## 红线映射（规范 → 落实处）

| 红线 | 落实处 |
|---|---|
| 禁止直接 import Table | DataTable 唯一出口 |
| 颜色/间距走 token，禁魔法值 | 组件内统一 spacing.*（例外见已知问题 2） |
| 页面头部不重复标题 | Page 头部惰性渲染 |
| 页面内容撑满宽度 | Page 无 maxWidth/居中 |
| 表格操作栏左对齐 | DataTable 工具区、PageToolbar 无筛选时 |
| 文案走 t() | 内置文案全部走 i18n |

## 已知问题（as-built，未改代码）

1. tokens.md 令牌值与 tokens.ts 不同步（#4A9EFF/#00B578/radius 4 等 vs 实际 #2970FF/#17B26A/radius 6-8-12），以代码为准，详见 [../theme/sdd.md](../theme/sdd.md) 已知问题。
2. Card/StatCard 存在硬编码色值：Card 边框/阴影 `rgba(16,24,40,.08/.05)`、StatCard 图标 `#2970FF`（与 colorPrimary 同值但未引 token）与背景 `#EFF4FF`；浅色系边框/阴影在暗色主题下未做感知切换。
3. PageToolbar/QueryBar 无 loading 级联：PageToolbar 纯布局无状态；QueryBar 的 loading 仅作用于查询按钮，重置无 loading 态。
4. DataTable 的 showTotal 插值依赖 i18n 已初始化——测试必须 `import '@/i18n'`，否则渲染原始键名。
