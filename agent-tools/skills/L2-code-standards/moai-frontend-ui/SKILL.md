---
name: moai-frontend-ui
description: Frontend page standards for MoAI ui/ (React 19, antd 5 design-system, Kiota client, zustand, i18next). Use when adding/modifying pages, API wrappers, i18n keys, or frontend tests in ui/. 仅限 MoAI 项目。Use only for the MoAI project.
---

# MoAI 前端页面规范（L2）

## PROJECT SCOPE

只服务 MoAI 前端 `ui/`。**REQUIRED REFERENCE：改代码前必读 [ui/docs/frontend-conventions.md](../../../ui/docs/frontend-conventions.md)（唯一真源）与 [ui/docs/design-system/README.md](../../../ui/docs/design-system/README.md)（组件/主题契约）。** 本 skill 只写执行要点与实踩坑。

## WHEN

- 新增/修改页面、API 封装、i18n 文案、组件测试
- 涉及 `ui/src/pages|api|design-system|i18n` 的改动

## WHAT

产出合规范的页面接线（Kiota 封装 → design-system 页面 → 双语 i18n → vitest），三件套全绿。

## HOW

### 0. API 客户端（Kiota）

- 后端起在 `:5210` 后：`cd ui && npm run syncapi http://127.0.0.1:5210/openapi/v1.json`（脚本先删后生成）
- `src/api/client/` **禁止手改**（Kiota 锁定 `1.0.0-preview.93`，勿用 caret 升级）
- 手写封装放 `src/api/<module>.ts`：业务请求用 `getApiClient()`；登录/注册/serverinfo 等匿名接口用 `getAnonymousClient()`
- 密码传输：`getServerInfo().rsaPublic` → `rsaEncrypt` → 提交，禁止明文

### 1. 页面规则（真源 §页面编写规范，全条强制）

1. **页面头部不重复标题/解释**：管理/设置类页面用裸 `<Page>`（不传 title/subtitle——顶部导航已标识页面）。唯一例外：Dashboard 的个性化问候
2. **内容撑满宽度**：不加 maxWidth / margin auto 居中
3. **表格工具栏从左排列**：toolbar 与刷新按钮依次从左（不用 space-between 分居两端）；筛选+操作的标准布局用 `PageToolbar`（filters 左、actions 右）
4. **表单 Modal 一律 `maskClosable={false}`**，防误触丢内容

### 2. 列表页统一约定（2026-09-02 起）

- 操作列：图标按钮（`type="text" size="small"`）+ Tooltip + `aria-label`；危险操作 Popconfirm 且 `okButtonProps={{ danger: true }}`；列 `fixed: 'right'`
- `<DataTable sticky>`（页面滚动表头常驻）；列宽以真实数据验证（手机号 130、`YYYY-MM-DD HH:mm` 时间 150 + nowrap）
- 用户/资源列可合并展示：头像（`resolveStorageUrl`，无则首字母）+ 名称，次行灰字辅助信息
- 颜色/间距只用 `@/design-system/theme` 的 token（`neutralColors`/`brandColors`/`spacing`/`radius`），禁止硬编码 hex/rgba；跟随主题的背景用 antd token（`theme.useToken()` 的 `colorBgLayout`/`colorPrimaryBg`），**禁止 `${color}14` alpha 叠加**（暗色主题会翻车）

### 3. 状态 / 路由 / 反馈

- 登录态读 `useAppStore`（`userInfo.isAdmin/isRoot` 驱动权限渲染——前端只是体验层，后端才是防线）
- 反馈用 design-system `feedback`/`useFeedback()`，不用静态 message
- 新页面：受保护页挂 `AppLayout` children；catch 块统一注释"错误已由全局请求中间件提示"

### 4. i18n

- `locales/{zh-CN,en-US}/common.json` **双语同步改**，业务前缀分组（users./settings.）；文案一律 `t()`，禁止硬编码

### 5. 测试

- `src/pages/<page>/__tests__/*.test.tsx`；mock `@/api/*` 与 `@/api/auth`
- ⚠️ 图标按钮断言用 `aria-label`（Tooltip 不渲染进 DOM，textContent 拿不到）
- 权限矩阵测试：root 行无危险操作、非 admin 重定向

## REFERENCE

正例：`ui/src/pages/users/Users.tsx`（列表 + 图标操作列 + sticky + 详情弹窗 Skeleton + 双弹窗）；反例与修复史见各 sop 存档。

## LIMITS

- 不含后端规范（`L2-code-standards/moai-cqrs-backend`）；审查清单（`L3-fix-standards/moai-cqrs-review`）
- 不改 design-system 组件 API 语义（改组件 = 全站影响，需走仓库提交评审）
