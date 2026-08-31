# 页面原型约束

对应活例子：`ui/src/design-system/templates/`。

## 五种页面骨架
对应活例子：`ListTemplate` / `FormTemplate` / `DetailTemplate` / `DashboardTemplate` / `ChatTemplate`。

1. 列表页（`ListTemplate`）：`Page` → `QueryBar` → `DataTable`（分页/loading/空态/操作列）。
2. 表单页（`FormTemplate`）：`Page` → `FormPage`（布局、校验、提交/取消）。
3. 详情页（`DetailTemplate`）：`Page` → `DetailPage`（只读、返回/编辑）。
4. 概览页（`DashboardTemplate`）：`Page` → `Row/Col` + `StatCard` + 卡片。
5. 对话页（`ChatTemplate`）：`Page` → `Chat`。

## 自查清单（AI 生成页面后逐项勾选）
- [ ] 页面根节点使用 `Page` 包裹。
- [ ] 列表页使用 `QueryBar`+`DataTable`，未散落 antd Table。
- [ ] 颜色/间距取 token，无魔法值。
- [ ] 文案走 `useTranslation()`，无硬编码。
- [ ] 暗色模式下对比度可用（经 token 感知）。
- [ ] 接口错误经 `@/design-system` 的 `useFeedback()` 提示（生产错误用 message，严重错误/系统通知用 notification）。
- [ ] 删除、批量删除等危险操作按钮已用 `Popconfirm` 二次确认，且反馈走 `useFeedback()`。
