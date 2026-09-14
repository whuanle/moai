# 外部应用访问点设计（后端端点 + 悬浮 JS 组件）

- 日期：2026-09-14
- 状态：待评审
- 关联：[应用工作台设计](./2026-09-14-app-workspace-design.md) ｜ [外部应用·应用接入设计（前置）](./2026-09-13-external-app-and-access-design.md) ｜ [应用 SDD](../../app/sdd.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md)

## 背景与目标

外部应用当前只能创建/配置/发布，**没有任何对外调用链路**（`/external/token`、外部 JWT audience、`external` 用户表、`/external/agent/{id}/chat` 均未实现），工作台「访问点」只是占位。

本期交付「访问点」：
1. **后端端点**：每个外部应用有一个 AI 对话后端地址，第三方可自行开发前端对接。
2. **悬浮 JS 组件**：一个通用脚本 `{Server}/embed/moai-widget.js`，宿主页用 `<script data-app-id=... [data-key=...]>` 引入，页面右下角出现悬浮按钮，点击展开对话；标题/欢迎语/主题色/位置等可定制；按应用配置决定是否需要授权。

决策（已确认）：本轮**一并实现外部调用链路**；组件用**通用脚本**由后端托管；配置存**新表 `app_access_point`**；授权方式 **is_auth=true 时宿主页带 `data-key`（应用接入 key），false 时匿名**。

## 范围

**包含：**
1. 外部调用链路（采用前置设计 D3/D4/D5）：`external` 外部用户表、外部 JWT audience `<Server>|<external>` 与独立认证 scheme、`POST /external/token`、`/external/agent/{appId}/session|chat|session/list`、`/external/session/{sessionId}/messages`。
2. 访问点配置：新表 `app_access_point` + 内部 CRUD（Admin+）+ 匿名公开配置端点。
3. 通用悬浮组件：`ui/embed` 构建出单文件 IIFE，后端 `/embed/moai-widget.js` 托管；运行时拉配置、按授权模式换 token、流式对话。
4. 工作台「访问点」分区：展示后端端点地址 + 嵌入代码片段（可复制）+ 配置表单。
5. E2E（自建桩模型）+ 文档四件套同步。

**不包含（后续迭代）：**
- 组件的高级定制（自定义 CSS 注入、暗色主题切换、多语言自行扩展）、域名白名单。
- 外部 token 吊销控制台（先靠禁用 `app`/`access_app` 实现）。
- 组件以 npm 包/React 组件形式对外发布。

## 一、外部调用链路（后端）

> 细节沿用 [前置设计](./2026-09-13-external-app-and-access-design.md) 的「鉴权与外部 Token」「外部对话」两节，此处只列落地要点。

### `external` 外部用户表（新建）

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | bigint identity | 主键；承载会话 `create_user_id` 与用量 `user_id` |
| `team_id` | int | 归属团队 |
| `app_id` | uuid null | 匿名路径的来源应用 |
| `access_app_id` | uuid null | 授权路径的来源应用接入 |
| `external_user_id` | varchar(128) | 外部身份标识（宿主传入或随机临时值） |
| `nickname` | varchar(100) | 可选显示名 |
| 审计五件套 | | |

partial 唯一索引 `(access_app_id, external_user_id) WHERE access_app_id IS NOT NULL AND is_deleted = 0`：同一接入下同一外部身份复用同一条记录，从而继承聊天与消费记录。

### 外部 Token（`POST /external/token`，匿名）

请求体二选一：
- `{ accessAppKey, externalUserId?, nickname? }`（`is_auth=true`）：校验 key 存在、未删除，取该接入的 `team_id/app_ids`；`appIds` 必须都属本团队外部应用（创建接入时已保证）。提供 `externalUserId` 则按 `(access_app_id, external_user_id)` upsert；否则随机临时身份。
- `{ appId, externalUserId?, nickname? }`（`is_auth=false`）：校验 `app.is_external && !app.is_auth && publish_status=1 && !is_disable`；生成临时/绑定 `external` 行（`app_id` 记来源）。

响应：外部 JWT（`aud = "<Server>|<external>"`，`typ=external`，`sub=external.id`，附 `team_id`、`app_id?`、`access_app_id?`、`app_ids[]`、`external_user_id`），有效期 2 小时（可配）。

### 双 audience 与认证 scheme

- 内部 JWT 保持 `aud = Server`；外部 JWT `aud = "<Server>|<external>"`，同一 RSA 私钥签发。
- `ConfigureAuthorizaModule` 增注册 `AddJwtBearer("External", ...)`（`ValidAudience = "<Server>|<external>"`，其余与内部一致），并加授权策略 `External`（要求该 scheme）。内部端点用默认 scheme，**天然拒绝**外部 token；外部端点用 `External` 策略。
- `UserContextProvider.Parse()` 已按 `Typ` claim 还原 `UserType`（`external` 字符串）；`external.id` 落入 `UserId(long)`。

### 外部对话端点

| 方法 | 路由 | 鉴权 | 说明 |
|---|---|---|---|
| POST | `/external/token` | 匿名 | 见上 |
| POST | `/external/agent/{appId:guid}/session` | External | 校验 `appId` 在 token 允许范围且 `app.is_external`；写 `app_agent_session`（`user_type=External`、`create_user_id=external.id`） |
| POST | `/external/agent/{appId:guid}/chat` | External | AG-UI SSE，复用 `AppAgentDispatcher`（同一 keyed Agent + `AppAgentSessionStore`），端点要求 External 策略 + `appId ∈ app_ids` |
| GET | `/external/agent/{appId:guid}/session/list` | External | 该 `external` 身份的会话列表 |
| GET | `/external/session/{sessionId:guid}/messages` | External | 会话消息（按 `external.id` 归属校验） |
| GET | `/external/app/{appId:guid}/access-point` | 匿名 | 组件用的公开配置（见 §三） |

- 端点注册：在 `AppAgentEndpointMapper` 增 `MapAGUIServer(AgentName, "/external/agent/{appId:guid}/chat").RequireAuthorization("External")`（同一 Agent 名与 store）；其余外部端点由新的 `ExternalController` 提供。
- 会话归属：`AppAgentDispatcher.ResolveInnerAsync` 的 `create_user_id == userId` 校验对 `external.id` 同样成立，无需改派发器。

## 二、访问点配置（新表 `app_access_point`）

| 列 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `id` | uuid PK | | |
| `team_id` | int | | 冗余，团队维度过滤 |
| `app_id` | uuid | | 外部应用 id（partial 唯一 `is_deleted=0`，1:1） |
| `title` | varchar(100) | null | 面板标题（空则用应用名） |
| `subtitle` | varchar(255) | null | 欢迎语/副标题 |
| `placeholder` | varchar(100) | null | 输入框占位 |
| `primary_color` | varchar(20) | null | 主题色（如 `#1677ff`） |
| `position` | varchar(20) | `bottom-right` | `bottom-right` / `bottom-left` |
| `launcher_text` | varchar(50) | null | 悬浮按钮文案（空则用图标） |
| `avatar` | varchar(255) | null | 头像 objectKey（走存储） |
| `panel_width` | int | 380 | 面板宽 |
| `panel_height` | int | 560 | 面板高 |
| `default_open` | bool | false | 默认展开 |
| `enabled` | bool | true | 是否启用访问点 |
| 审计五件套 | | | |

**内部接口（Admin+，仅外部应用）**
- `GET /app/{id}/access-point`：返回配置（未保存过返回空配置/默认值，不 404）。
- `PUT /app/{id}/access-point`：整体保存配置（`IModelValidator` 校验长度、颜色格式、位置枚举、宽高范围）。

## 三、公开配置（组件用）

`GET /external/app/{appId:guid}/access-point`（匿名）返回**只读公开字段**：
`{ appName, avatarUrl, title, subtitle, placeholder, primaryColor, position, launcherText, panelWidth, panelHeight, defaultOpen, isAuth, enabled }`
——`isAuth`/`enabled` 由 `app` 决定；配置不存在时返回默认值。应用不存在/非外部 → 404。

## 四、通用悬浮组件

### 交付与托管
- 新增 `ui/embed/`（独立 Vite lib 入口，输出 IIFE 单文件，无外部运行时依赖，样式用 **Shadow DOM** 隔离）。
- 构建脚本 `npm run build:embed` → 产物输出到 `src/MoAI/wwwroot/embed/moai-widget.js`。
- 后端已 `UseStaticFiles()`（`Program.cs:44-45`），直接托管 `/embed/moai-widget.js`。（`MapFallbackToFile("index.html")` 保持现状，不影响 `/embed`。）

### 使用方式
```html
<script src="https://<server>/embed/moai-widget.js"
        data-app-id="<appId>"
        data-key="<accessAppKey>"        <!-- is_auth=true 时必填（应用接入 key） -->
        data-external-user-id="<可选，绑定身份以继承会话>"
        data-nickname="<可选>"></script>
```
- `data-server` 可选；缺省从 `<script src>` 的 origin 推导。
- 运行时：拉 `GET /external/app/{appId}/access-point` → 渲染悬浮按钮 → 点击展开面板。
  - `enabled=false` → 不渲染。
  - `isAuth=true` 且无 `data-key` → 面板提示「需要访问密钥」。
  - 换 token：`POST /external/token`（授权带 `accessAppKey`，匿名带 `appId`）→ 外部 token。
  - 会话：`POST /external/agent/{appId}/session` 拿 `sessionId` 作 `threadId`；对话走 `POST /external/agent/{appId}/chat`（AG-UI SSE）流式渲染。
- 定制：标题/副标题/欢迎语/占位/主题色/位置/头像/宽高/默认展开均来自公开配置；`data-position` 可覆盖位置。
- 样式：Shadow DOM + CSS 变量注入主题色，避免与宿主页冲突。

## 五、工作台「访问点」分区（内部 Admin+）

`AppAccessSection.tsx`（仅外部应用渲染，替换占位）：
- **后端端点**：展示并复制 `POST {Server}/external/agent/{appId}/chat`（及 session/token 端点）。
- **嵌入代码**：展示上文 `<script>` 片段（`is_auth=true` 时含 `data-key` 占位提示），一键复制。
- **配置表单**：标题/副标题/占位/主题色/位置/按钮文案/头像/宽高/默认展开/启用 → 保存（`PUT /app/{id}/access-point`）。
- **预览**：可选（iframe 载入组件或按配置渲染静态预览）。

## 六、鉴权与权限矩阵

| 操作 | 内部 Admin+ | Member | 外部身份 | 匿名 |
|---|---|---|---|---|
| 访问点配置读写 | ✅ | 403 | — | — |
| 公开配置 | ✅ | ✅ | ✅ | ✅ |
| `/external/token`（匿名） | — | — | — | ✅（凭 key 或 `is_auth=false` 的 appId） |
| `/external/agent/...` 对话 | — | — | ✅（token 有效且 appId 在允许范围） | — |

失败：key 无效/删除 → 401；`appIds` 命中失败 → 403；app 未发布/禁用/`is_auth` 不匹配 → 403；token 过期/aud 不符 → 401；非外部应用 → 404。

## 七、验证

- 后端：`dotnet build src/MoAI/MoAI.csproj` 0 error。
- E2E：扩展 `local-dev/app-e2e.mjs`（或新增 `local-dev/access-point-e2e.mjs`，自带 OpenAI 兼容 SSE 桩模型）：匿名换 token（`is_auth=false`）、key 换 token（`is_auth=true`）与身份绑定继承、`/external` 建会话与对话（SSE）、越权/未授权/未发布拒绝、公开配置、访问点配置 CRUD。
- 前端：`npm run typecheck && npm run lint && npm run test`；`npm run build:embed` 产物存在。
- 手动：静态 HTML 引入 `/embed/moai-widget.js`，浏览器走查悬浮按钮与对话（`sop.md` 记录）。
- 文档：新增 `docs/access-point/*` 四件套（或并入 app），`rounds-log.md` 记账。

## 八、分期

1. **4a 访问点配置**：`app_access_point` 表 + 实体/配置 + 内部 CRUD + 公开配置端点 + 工作台「访问点」UI（端点/片段/表单）。
2. **4b 外部链路**：`external` 表 + 双 audience/scheme + `/external/token` + `/external` 会话/对话端点 + E2E。
3. **4c 悬浮组件**：`ui/embed` 构建 + 后端托管 + 鉴权/流式对话 + 手动走查 + E2E。

每期独立可验证后再进入下一期。

## 九、关键决策

- **D34 通用脚本而非每应用脚本**：`/embed/moai-widget.js` 一份，靠 `data-app-id` 区分；配置运行时拉取，缓存友好、后端只托管一份。
- **D35 访问点配置独立成表**：`app_access_point`（与外部应用 1:1），便于扩展与独立校验，不动 `app`。
- **D36 组件授权沿用应用接入 key**：`is_auth=true` 由宿主页带 `data-key`；`false` 匿名。不在组件里做登录态。
- **D37 复用 AG-UI 运行时**：外部对话复用同一 `AppAgentDispatcher`/`AppAgentSessionStore`，仅换认证 scheme 与路由，不重写链路。
- **D38 Shadow DOM 隔离**：组件样式与宿主页互不污染；主题色经 CSS 变量注入。

## 十、后续迭代

1. 组件高级定制与域名白名单；npm/React 版本。
2. 外部 token 吊销控制台与接入用量看板。
3. 访问点预览与 A/B 样式。
