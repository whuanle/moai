# 前端表单与反馈组件（components-form）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../docs/components-base/sdd.md](../../docs/components-base/sdd.md)（基础组件）、[../../docs/api-layer/sdd.md](../../docs/api-layer/sdd.md)（错误归一化来源）、[../design-system/feedback.md](../design-system/feedback.md) ｜ 证据：[components 单测](../../src/design-system/components/)
> 规范：[DOC-STANDARD](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-CF-Sxx），本文不重复。

## 目标

表单页壳（FormPage）、详情页（DetailPage）、对话页（Chat）与全局反馈子系统（Feedback），固化「提交态、骨架加载、错误统一路由、文案走 i18n」约束；Feedback 是全站唯一 message/notification 出口。

## 结构（`ui/src/design-system/components/`）

| 目录 | 内容 |
|---|---|
| FormPage/ | FormPage.tsx + `__tests__`（3 例） |
| DetailPage/ | DetailPage.tsx + `__tests__`（3 例） |
| Chat/ | Chat.tsx + `__tests__`（1 例） |
| Feedback/ | feedback.ts（单例 + handleError 路由）、useFeedback.ts（hook 入口）、FeedbackBridge.tsx（antd App 内注册实例）、instances.ts（实例注册表）、error.ts（错误归一化）、index.ts（6 组导出）+ `__tests__`（feedback 14 + bridge 1） |

全部经 `@/design-system` 桶导出。

## 组件契约

### FormPage（表单页壳）

`FormPageProps extends Omit<FormProps,'onFinish'|'title'|'children'>`：

| Prop | 必填 | 说明 |
|---|---|---|
| onFinish | **是** | 校验通过后的提交回调（类型收窄为 `Record<string, unknown>`，泛型表单需调用侧断言） |
| title | 否 | Typography.Title level=4 |
| onCancel / submitting | 否 | 「取消」按钮 / 「提交」按钮 loading（只作用提交按钮） |
| footerExtra / actionTop | 否 | 底部操作区追加节点 / 表单上方附加区块 |
| 其余 FormProps | 否 | form/initialValues 等透传 |

契约：容器 `maxWidth:720 居中`（组件自身的表单阅读宽度；「不加 maxWidth」约束的是 Page 容器，用法 `<Page><FormPage/></Page>`）；vertical 布局；操作区固定表单尾部；按钮组关闭 autoInsertSpace。

### DetailPage（详情页）

必填 `items`（antd Descriptions items）；可选 title / loading（Skeleton rows 6，不渲染 Descriptions）/ column（**默认 1**）/ onBack（普通按钮）/ onEdit（primary）/ extra（头部操作组追加）。头部仅当 title || onEdit || onBack || extra 才渲染，标题左、操作右 space-between；正文 `Descriptions bordered`。

### Chat（对话页）

`ChatMessage { id; role:'user'|'assistant'|'system'; content; time? }`；必填 messages，可选 inputValue（默认 ''）/onInputChange/onSend/sending/empty/height（默认 480）。契约：受控输入；容器固定 height、边框圆角全 token；user 气泡右对齐品牌色白字、assistant/system 左对齐浅底；气泡 maxWidth 75%、pre-wrap；TextArea 1-4 行自适应 + 「发送」按钮（**仅按钮触发，无回车发送**）。活例：`templates/ChatTemplate.tsx`、展示台 ChatSection。

## Feedback 子系统

- 通道：`success/error/warning/info/loading`（message 轻量 toast）；`notify/notifySuccess/notifyWarning/notifyError`（notification 系统通知/严重错误）。
- 未注册实例时安全降级 no-op + console.warn 提示挂 FeedbackBridge（[@FE-CF-S9](./bdd.md#fe-cf-s9)）。
- React 侧推荐 `useFeedback()`；非 React 上下文（kiota 中间件）用 `feedback` 单例。

`handleApiError` 路由（feedback.ts + error.ts）：

| 错误形态 | 通道与文案（i18n 键） |
|---|---|
| 无状态码 且 网络异常 | notification.error：feedback.networkError |
| 无状态码 且 非网络异常 | notification.error：feedback.unknownError |
| 状态码 ≥ 500 | notification.error：feedback.serverError（Desc 取 detail，缺省 serverErrorDesc） |
| 4xx | message.error：resolveErrorMessage 结果，缺省 feedback.businessError |

错误归一化（error.ts，源自 [../../docs/api-layer/sdd.md](../../docs/api-layer/sdd.md)）：`getHttpStatus`（Response.status 优先，其次 responseStatusCode/responseStatus/status，≥100 才认）；`isNetworkError`（无状态码且 TypeError 或网络文案正则）；`extractErrorMessage`（过滤 Kiota 通用文案与网络文案）；`resolveErrorMessage` 优先级 **detail > 首个字段校验错误 > 过滤后 message**；`parseApiErrorResponse`（容忍空/非 JSON 响应体产出 NormalizedApiError）。

## 接线调用链

`main.tsx` → AppProviders（ConfigProvider → antd `<App>` → `<FeedbackBridge/>`）→ App。`api/kiota.ts` 的 FilterRequestHandler：请求抛错 → `feedback.handleError(error)`（非网络错误再 clearUserInfo）；**401 且 URL 不含 login** → clearUserInfo + 跳 `/login`（不发 toast）；非 2xx → parseApiErrorResponse → handleError → 重新 throw。因此页面 `catch {}` 只留注释不重复弹错。

## i18n 键（zh-CN/common.json）

| 键 | 值 |
|---|---|
| ds.form.submit / ds.form.cancel | 提交 / 取消（FormPage） |
| ds.detail.back / ds.detail.edit | 返回 / 编辑（DetailPage） |
| ds.chat.placeholder / send / empty | 输入消息... / 发送 / 开始一段对话吧。（empty 仅展示台在用） |
| feedback.businessError / serverError(Desc) / networkError(Desc) / unknownError | handleError 路由文案 |

## 已知问题（as-built，未改代码）

- Chat 默认空态文案硬编码中文「暂无消息」，未走 t()（`ds.chat.empty` 键已存在）；`ChatMessage.time` 字段已定义但未渲染；仅按钮发送、无回车发送。
- FormPage 无 destroyOnHidden 类卸载清理（表单状态由使用方持有）；onFinish 类型收窄需调用侧断言。
- Feedback 单例未考虑跨实例（微前端等 React 并发场景）。
- `handleError` 的 401 行为与 feedback.md 分册表述有出入：分册写「401 仅登录态场景跳转」，实际 kiota 中间件为 401 且非 login 接口即清态跳转；请求异常且非网络错误时也会 clearUserInfo（以代码为准）。
