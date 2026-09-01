# 页面原型约束

对应活例子：`ui/src/design-system/templates/`。

## 五种页面骨架
对应活例子：`ListTemplate` / `FormTemplate` / `DetailTemplate` / `DashboardTemplate` / `ChatTemplate`。

1. 列表页（`ListTemplate`）：`Page` → `PageToolbar`（筛选栏/操作按钮流） → `DataTable`（分页/loading/空态/操作列）。
2. 表单页（`FormTemplate`）：`Page` → `FormPage`（布局、校验、提交/取消）。
3. 详情页（`DetailTemplate`）：`Page` → `DetailPage`（只读、返回/编辑）。
4. 概览页（`DashboardTemplate`）：`Page` → `Row/Col` + `StatCard` + 卡片。
5. 对话页（`ChatTemplate`）：`Page` → `Chat`。

## 页头工具行布局规则（Dify）

参考 Dify 工作室 / 列表页顶部：**同一行内**，左侧是筛选控件，右侧是操作按钮。溢出时右侧跟随行尾、左侧可换行。

统一使用 `PageToolbar` 组件落实规则：

- **有筛选功能**：`filters`（筛选栏）放左，`actions`（操作按钮）放右。
  ```tsx
  <PageToolbar
    filters={<><Select /><Input /></>}
    actions={<Button type="primary">新建</Button>}
  />
  ```
- **无筛选栏**：仅操作按钮，则 `actions` **直接左对齐**，放在最左侧。
  ```tsx
  <PageToolbar actions={<Button type="primary">新建</Button>} />
  ```
- 约定：不要手动用 `Space` 拼装左右两段，一律交给 `PageToolbar` 的 `filters` / `actions` 两个插槽。
- 其余独立（不依赖 `PageToolbar`）的查询表单用 `QueryBar`，用于展示「字段 + 查询/重置」的脱离工具行场景。

## 自查清单（AI 生成页面后逐项勾选）
- [ ] 页面根节点使用 `Page` 包裹。
- [ ] 列表页工具行使用 `PageToolbar`（筛选在左、操作在右；无筛选时操作左对齐），未手动拼 `Space`。
- [ ] 列表页使用 `DataTable`，未散落 antd Table。
- [ ] 颜色/间距取 token，无魔法值。
- [ ] 文案走 `useTranslation()`，无硬编码。
- [ ] 暗色模式下对比度可用（经 token 感知）。
- [ ] 接口错误经 `@/design-system` 的 `useFeedback()` 提示（生产错误用 message，严重错误/系统通知用 notification）。
- [ ] 删除、批量删除等危险操作按钮已用 `Popconfirm` 二次确认，且反馈走 `useFeedback()`。
