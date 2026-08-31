# 消息提示与通知（Feedback）

对应代码：`ui/src/design-system/components/Feedback/`。

## 统一反馈出口

业务页面一律从 `@/design-system` 导入 `useFeedback()`，禁止散落 antd 原生 `message` / `notification`。

```tsx
import { useFeedback } from '@/design-system'

const feedback = useFeedback()
```

## 路由约定

| 场景 | 通道 | 方法 |
|---|---|---|
| 普通业务错误（4xx、表单校验等） | message（轻量） | `feedback.error(msg)` |
| 普通业务成功 / 警告 / 信息提示 | message | `feedback.success/ warning/ info(msg)` |
| 消息提示、系统通知 | notification | `feedback.notify(msg)` |
| 严重错误（服务器 500、网络异常） | notification | `feedback.notifyError(msg)` |

### API 错误自动路由

API 层（`src/api/kiota.ts`）已接入 `feedback.handleError()`，无需每个页面重复处理：

- `5xx` / 网络异常 → `notification`（`notifyError`）
- 业务 `4xx`（非 401）→ `message`（`error`）
- `401` 仅在登录态场景跳转 `/login`，不做 toast（登录失败由登录页单独处理）

因此接口错误无需在页面 `catch` 中再弹一次，避免重复提示。

## 非组件场景

`feedback` 是全局对象，当 antd `App` 已挂载 `<FeedbackBridge />` 后，可在组件外部调用（store、API 层等）：

```ts
import { feedback, isNetworkError } from '@/design-system'
```

未注册（`FeedbackBridge` 未挂载）时，调用会安全降级为 no-op 并抛出 console 警告。

## 建议

- 普通业务反馈用 `message`，保持轻量；会影响会话/数据的严重错误用 `notification`，给用户明确告警。
- 文案尽量走 `useTranslation()` 的 `t()`；Feedback 内置的 `serverError` / `networkError` 等默认文案位于 `locales/*/common.json` 的 `feedback` 键下。
