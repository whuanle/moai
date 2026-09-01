# 前端表单与反馈组件（components-form）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 基础组件（Page/DataTable 等）见 [../../docs/components-base/sop.md](../../docs/components-base/sop.md)。

## 1. 新增业务表单页

1. 路由：`src/router/index.tsx` 的 AppLayout children 追加（详见 [../../docs/layout-routing/sop.md](../../docs/layout-routing/sop.md)）。
2. 骨架：`<Page>` 包 `<FormPage>`（自带 720 阅读宽度，不要再加 maxWidth）。模板：`design-system/templates/FormTemplate.tsx`。
3. 字段：`Form.Item` + rules；密码类参照 `Users.tsx` 重置密码弹窗的「确认密码一致性」validator（dependencies + getFieldValue 比对）。
4. 提交：`onFinish={async (values) => { setSubmitting(true); try { await api(...); feedback.success(t('xx.success')); } catch { /* 全局中间件已提示 */ } finally { setSubmitting(false) } }}`。
5. 取消：`onCancel={() => navigate(-1)}`；文案新键同步补 zh-CN/en-US 两份 common.json。

> 弹窗内表单（Users 重置密码、OauthConnect 新建/编辑）不用 FormPage，用 `Modal + Form`，但必须 `maskClosable={false}`；okText/cancelText/confirmLoading 对应 FormPage 的 取消/提交/submitting 语义。

## 2. 新增详情页

`<Page>` 包 `<DetailPage>`：items 用 antd Descriptions 结构；loading 接数据加载态（自动出骨架）；`onBack={() => navigate(-1)}`；多列传 column。活例：`templates/DetailTemplate.tsx`。弹窗内轻量详情（Users 查看弹窗）用 `Modal + Descriptions`，不必上 DetailPage。

## 3. Feedback 使用守则

- React 组件内 `const feedback = useFeedback()`；非 React 上下文（kiota 中间件）`import { feedback } from '@/design-system'`。
- 通道选择：普通业务成功/失败 → `feedback.success/error`（[@FE-CF-S14](./bdd.md#fe-cf-s14)）；系统通知/严重告警 → `feedback.notify*`；接口错误**不要**在页面 catch 重复弹（kiota 中间件已统一 handleError，[@FE-CF-S13](./bdd.md#fe-cf-s13)）。
- 自定义错误文案：`resolveErrorMessage(err) ?? t('fallback')`。
- 禁止绕过 Feedback 直接调 antd message——暗色主题/实例一致性会失控。

## 4. 对话页

`<Page>` 包 `<Chat>`：`inputValue/onInputChange` 成对传入（受控）；`onSend` 里置 sending 防重复；空态建议传 `t('ds.chat.empty')`（内置空态为硬编码中文，见 [SDD 已知问题](./sdd.md)）。活例：`templates/ChatTemplate.tsx`。

## 5. 常见问题

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| toast 不出现且控制台 warn | FeedbackBridge 未挂到 antd App 内 | 查 AppProviders 装配 | [@FE-CF-S9](./bdd.md#fe-cf-s9)/[@FE-CF-S10](./bdd.md#fe-cf-s10) |
| FormPage 按钮出现空格 | — | 内部已关 autoInsertSpace；仍现则查是否绕过 FormPage | — |
| Chat 输入不受控 | inputValue/onInputChange 未成对传入 | 补齐受控对 | [@FE-CF-S7](./bdd.md#fe-cf-s7) |
| onFinish 的 values 类型宽泛 | 契约类型为 Record<string, unknown> | 调用侧断言或局部 interface（见 SDD 已知问题） | [@FE-CF-S1](./bdd.md#fe-cf-s1) |
| Chat 回车不能发送 | 现状仅按钮触发 | 业务自行包 keydown 或等组件增强（见 SDD 已知问题） | [@FE-CF-S7](./bdd.md#fe-cf-s7) |

## 6. 验收流程（改动后）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（定向 22 用例 + 全量 42 + lint/typecheck）。
2. 手工：`npm run dev` 按 BDD 场景走查（含 `/design-system` 展示台的 FormPage 校验段与 Feedback 段）。

## 7. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 交付验收（轮 15，as-built）**：定向 5 files / 22 tests（FormPage 3、DetailPage 3、feedback 14、bridge 1、Chat 1）全过，Duration 1.79s；全量 13 files / 42 tests；lint 0 error / 0 warning。布局常量命令复核：`grep maxWidth FormPage.tsx` → 第 29 行 720；`grep "height = 480" Chat.tsx` → 第 28 行命中；`grep -c "^export" Feedback/index.ts` → 6。
- **2026-09-01 文档标准重构回归**：定向 5 files / 22 tests 复测通过。

## 8. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 15，as-built）；同日按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-CF-S1~S14）、四件互链、职责瘦身 |
