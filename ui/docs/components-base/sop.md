# 前端基础组件（components-base）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 参照实例：`ui/src/pages/users/Users.tsx`（搜索 + 服务端分页列表）、`ui/src/pages/oauthconnect/OauthConnect.tsx`（工具栏 + 全量表格）。

## 1. 组件速查

| 组件 | 一句话用途 | 必填 props |
|---|---|---|
| Page | 页面根容器（可选页头） | 无 |
| PageToolbar | 页头工具行（筛选左/操作右） | 无 |
| QueryBar | 独立「字段 + 查询/重置」表单 | 无 |
| DataTable | 表格（唯一合法 Table 出口） | columns、dataSource |
| Card | 通用卡片 | 无 |
| StatCard | 统计指标卡 | title、value |

全部从 `@/design-system` 导入。

## 2. 拼一个新列表页（标准套路，五步）

1. **骨架**：根节点 `<Page>`。常规管理页可不传 title/subtitle（顶部导航已标识页面，`OauthConnect.tsx` 即无头用法）；确有价值的个性化头部才传（`Users.tsx` 传了 users.title/subtitle，两种均为现行代码事实）。
2. **筛选形态**：字段多 → `QueryBar`（onSearch 里 trim 关键字、重置页码到 1 重拉，照抄 Users 的 handleSearch/handleReset）；轻量筛选 → `PageToolbar`（filters 左 / actions 右；无筛选只传 actions）。禁止手写 `Space` 拼左右两段。
3. **表格**：一律 `DataTable<T>`，禁止直接 `import { Table } from 'antd'`。rowKey 默认 'id'；服务端分页传 `pagination={{ current, pageSize, total, showSizeChanger, onChange }}`；全量数据 `pagination={false}`；「新建」入口传 `toolbar`。
4. **危险操作**：删除/禁用/授权必须包 antd `Popconfirm`（title 描述后果、文案走 t()、按钮 danger）。接口错误不要在页面 catch 重复弹提示——api 层中间件已统一走全局反馈，`catch {}` 留注释即可。
5. **文案与取色**：用户可见文案走 `useTranslation()`（新键同步补 zh-CN/en-US 两份 common.json）；间距用 spacing.*、颜色走 token。

新增页面接线（路由/菜单）见 [../../docs/layout-routing/sop.md](../../docs/layout-routing/sop.md) 第 1 节。

## 3. 拼一个概览/仪表区块

- 指标行：`Row/Col` + `StatCard`（trend 传数字自动出涨跌角标）。活例：`DesignSystemPreview.tsx` CardsSection、`templates/DashboardTemplate.tsx`。
- 内容分组：`Card`（可带 title）；组合约束按 [design-system/components.md](../design-system/components.md) 组合矩阵自查（Page 只做根节点、Card 不嵌 DataTable）。

## 4. 常见问题

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| 表格按钮两汉字间多空格 | antd autoInsertSpace | 组件内已关；页面自写按钮参照处理 | — |
| 「共 N 条」显示成原始键名 | i18n 未初始化 | 入口/测试确认 `import '@/i18n'` | [@FE-CB-S13](./bdd.md#fe-cb-s13) |
| QueryBar 回车查询拿不到值 | — | 组件已兼容；异常时检查受控 form 是否匹配 | [@FE-CB-S14](./bdd.md#fe-cb-s14) |
| 重置后列表没刷新 | onReset 只清字段 | 在 onReset 里重置页码并重拉（Users handleReset） | [@FE-CB-S15](./bdd.md#fe-cb-s15) |
| 页面内容没有撑满/被居中 | 自行了加 maxWidth 或 margin auto | 移除——Page 契约即无 maxWidth 收口 | [@FE-CB-S3](./bdd.md#fe-cb-s3) |
| 想用 antd Table 的 scroll/rowSelection | — | DataTable 透传全部 TableProps（除三项收口 props），直接传 | — |

## 5. 验收流程（新页面/改组件后）

1. 自查清单：根节点 Page；工具行用 PageToolbar/QueryBar 未手拼 Space；表格用 DataTable 无散落 antd Table；颜色间距取 token；文案走 t()；接口错误走全局反馈；危险操作有 Popconfirm。
2. 自动化：跑 [TDD 回归命令](./tdd.md)（定向 11 用例 + 全量 42 + lint/typecheck）。
3. 手工走查：`npm run dev` 后按 BDD 场景逐条过（含暗色对比度、`/design-system` 展示台目检）。

## 6. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 交付验收（轮 14，as-built）**：定向 5 files / 11 tests（Page 2、StatCard 1、DataTable 4、QueryBar 2、PageToolbar 2）全过；全量 13 files / 42 tests；`npm run lint` 0 error / 0 warning（Duration ~3.9s）。发现并记录两处规范偏差（未改代码）：tokens.md 令牌值与 tokens.ts 不同步；Card/StatCard 硬编码色值（详见 [SDD 已知问题](./sdd.md)）。
- **2026-09-01 文档标准重构回归**：定向 5 files / 11 tests 复测通过。

## 7. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 14，as-built）；同日按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-CB-S1~S18）、四件互链、职责瘦身 |
