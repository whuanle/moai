# 页面原型约束

对应活例子：`ui/src/design-system/templates/`。

## 五种页面骨架
1. 列表页：`Page` → `QueryBar` → `DataTable`（分页/loading/空态/操作列）。
2. 表单页：`Page` → `FormPage`（布局、校验、提交/取消）。
3. 详情页：`Page` → `DetailPage`（只读、返回/编辑）。
4. 概览页：`Page` → `Row/Col` + `StatCard` + 卡片。
5. 对话页：`Page` → `Chat`。

## 自查清单（AI 生成页面后逐项勾选）
- [ ] 页面根节点使用 `Page` 包裹。
- [ ] 列表页使用 `QueryBar`+`DataTable`，未散落 antd Table。
- [ ] 颜色/间距取 token，无魔法值。
- [ ] 文案走 `useTranslation()`，无硬编码。
- [ ] 暗色模式下对比度可用（经 token 感知）。
- [ ] 接口错误经 antd `App.useApp()` 的 message/notification 提示。
