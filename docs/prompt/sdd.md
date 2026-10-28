# 提示词（Prompt）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../publication/sdd.md](../publication/sdd.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文只写设计。

## 目标

平台级提示词库：任何用户可创建**个人提示词**（仅本人使用）；团队 Admin 及以上可创建**团队提示词**（默认仅团队内使用）；两类提示词均可申请上架，系统管理员审批通过后进入**提示词市场**对所有用户开放。

## 组件

- `src/prompt/MoAI.Prompt.Shared|Core|Api` 三层，Maomi 模块 `PromptCoreModule` 注册进 `MainModule`，依赖 `Team.Shared`（角色事实）、`Account.Shared`（人名填充）、`Classify.Shared`（分类类型常量）。
- 接口（Controller 门禁外全部在 Handler 校验资源归属/角色）：
  - `POST /api/prompt`、`PUT /api/prompt/{id}`、`DELETE /api/prompt/{id}`
  - `POST /api/prompt/{id}/avatar`（设置头像，objectKey 需已登记，权限同更新）
  - `GET /api/prompt/my_list`（个人）、`GET /api/prompt/team_list`（团队成员）、`GET /api/prompt/{id}`（详情含内容）、`GET /api/prompt/market_list`（公开）
- 上架复用 [publication 模块](../publication/sdd.md)（`resource_type=prompt`，`resource_id=prompt.id` 数字字符串）；本模块仅消费，不改其状态机。

## 数据

沿用既有 `prompt` 表（`PromptEntity`，用户已建）：`team_id`（0=个人）、`name`(≤20)、`description`(≤255)、`content`(text，入参限 10000)、`prompt_class_id`（classify.type=prompt）、`is_public`（上架审批通过置 true）、`counter`（市场被他人查看次数）、`is_audit`（预留未用）。存量库执行 `asserts/prompt.sql`。

- `prompt.use_count integer not null default 0`：被选为专家提示词的使用次数，`CreateAppSessionCommandHandler`/`UpdateAppSessionPromptCommandHandler` 绑定时 +1（ExecuteUpdateAsync，不经实体跟踪）；`GET /api/prompt/top_used?teamId=&limit=`（1~10，默认 10）按使用次数倒序返回范围内（本人个人 + 指定团队）且 use_count>0 的提示词，供对话页输入框下方「热门专家」展示。存量库补列见 `asserts/prompt.sql`。

## 关键决策

1. **归属即权限**：`team_id=0` 时仅 `create_user_id` 本人可读写；`team_id>0` 时经 `ITeamService.GetMyRoleAsync` 要求 Admin+ 可管理、成员可读。列表/详情均按此过滤，无权限统一 404 防探测。
2. **个人上架走创建人门禁**：publication 的 apply/withdraw 原只认团队角色，`team_id=0` 时新增「`create_user_id == ContextUserId`」分支，团队提示词仍走 Admin+。
3. **待审核申请冗余回显**：`PromptItem.PendingPublicationId` 由列表查询批量联查 `publication_review`（pending）得到，前端据此显示「待审核」并直接撤回，避免个人提示词无 team_list 可查的死路。
4. **删除联动**：删除提示词同时移除其 pending 上架申请（与撤回同语义），防审核列表僵尸记录；已审批通过的记录保留历史。
5. **counter 语义**：仅当查看者既非创建人又非团队成员（即通过市场可见路径）时 +1，创建人/团队成员查看不计。
6. **IUserIdContext 传递**：所有需用户维度的命令实现 `IUserIdContext`，Controller 内 new 的命令显式 `SetUserContext`（全局 `AutoAssignUserIdFilter` 只覆盖绑定参数）。

## 前端

- 一级菜单单一入口「提示词市场」`/prompt-market`；`PromptCenter` 页头 Tab 切换「提示词市场 / 我的提示词」，`/prompts` 与 `/prompt-market` 为同一组件的两个 Tab 路由（切换 Tab 即路由跳转并重置筛选/分页），侧边菜单不再单列「我的提示词」。两个 Tab 均为分类列表（CheckableTag，`classifyLabel` 展示 emoji）+ 关键字搜索筛选，卡片列表（xl/xxl 一行 6 张）+ 客户端分页（列表接口暂无分页参数），不展示条数统计，不使用表格。
- **独立编辑器页** `PromptEditor`：新建/编辑不再用弹窗，路由 `/prompts/new`、`/prompts/:promptId/edit`、`/team/:teamId/prompt/new`、`/team/:teamId/prompt/:promptId/edit`；顶部信息卡两行（第一行：头像角标上传 + 名称（窄框）+ 分类 + 保存·取消按钮同行底对齐；第二行：描述独占全宽）+ 编辑器右上 Segmented 双模式切换 + 下方整栏编辑器 `VditorMarkdownEditor`（`ui/src/components/`，默认 Vditor IR 即时渲染：直接输入 `#`/`**`/列表语法即时成型，Typora 式体验）；工具栏为 Vditor 内置（撤销/重做/标题/加粗/斜体/删除线/行内代码/列表/任务/引用/分隔线/代码块/表格/链接 + 全屏）；内置计数器按 `maxLength=10000` 展示并在超限后阻断输入（与后端 `Content` 上限一致）；主题/语言跟随全局设置（暗色热切换走 `setTheme`，不重建编辑器）；编辑器初始化完成前（`after` 回调前）不得调用 `setTheme/setValue`（vditor 内部状态未就绪会抛错），外部赋值（编辑页详情回填）经 `emittedRef` 判定后在 `after`/value effect 中补应用。运行时按需资源（lute WASM/i18n/icons/highlight.js/katex）由 `vite.config.ts#copyVditorToPublic` 复制到 `public/vendor/vditor/dist`（编辑器 `cdn: '/vendor/vditor'` 本地加载，不依赖外网；mermaid/mathjax 等重型图示引擎不分发）。头像在编辑器内上传（新建先传存储拿 objectKey 随创建提交，编辑直接调 avatar 接口）。
- **编辑器双模式与浏览器草稿**：`VditorMarkdownEditor` 支持 `ir`（即时渲染所见即所得，默认）/ `sv`（纯源码，`preview.mode='editor'` 关掉分屏预览为单栏源码）两模式；vditor 无公开模式切换 API，mode 变化走重建（值经 value 属性延续，撤销栈不保留），切换器为编辑器右上的图标 Segmented（`EyeOutlined`/`CodeOutlined` + Tooltip 文案，内置 `edit-mode` 工具项已移除避免状态脱节）。浏览器草稿：未保存内容（名称/分类/描述/正文/模式）防抖 500ms 落 `localStorage`，键 `moai-prompt-draft:edit:{id}` / `moai-prompt-draft:new:t{teamId}`；打开页面时草稿叠加在服务端内容之上并 toast「已恢复上次未保存的草稿」（StrictMode 双调用以 ref 守卫只弹一次），保存成功先取消防抖定时器再删键（防已保存内容被重新写回草稿），组件卸载时立即 flush 保留最后几百毫秒输入。
- 团队详情「提示词」分区（成员可见，`TeamPrompts` 按 `canManage` 收敛管理入口）：固定卡片网格（形态与提示词中心一致，客户端分页 12/24/48，按 `counter` 被使用次数降序、同次数保持后端顺序），新建/刷新按钮经 `QueryBar` 的 `extra` 并入查询行（查询/重置右侧），不展示条数统计与表格视图；分类筛选为头部固定分类列表（`Tag.CheckableTag`，`classifyLabel` 展示 emoji）。
- 共享 `PromptDetailModal`（头像标题 + 描述 + `Divider` 横线分隔的 Markdown 内容区，内容不用背景色/边框容器，标题行右侧复制按钮走 clipboard + feedback）供我的/团队/市场三处复用。
- 封装层 `ui/src/api/prompt.ts`，头像走 `uploadImageWithKey`（存储三段直传）+ `setPromptAvatar`；上架复用 `ui/src/api/publication.ts`；分类选项复用 `classifyApi.getClassifies('prompt')` 并以 `classifyLabel`（emoji + 名称）展示（团队分区为头部固定分类列表，编辑器为下拉）。

## 关键决策（补充）

7. **头像存 objectKey**：与团队/用户头像同规则——数据库只存 `avatar_path`（objectKey 或绝对地址），前端 `resolveStorageUrl` 拼展示地址；后端校验 objectKey 必须是已完成上传并登记（`files.is_uploaded`）的文件，防伪造。

## 已知问题

- `UpdatePromptCommand.PromptId` 已加 `[JsonIgnore]`：PUT `/{id}` 的请求体不再暴露 promptId（由路由提供）；存量 Kiota 客户端若仍发送该字段需为可绑定 int，不能为 null。



- 应用 Agent 配置的 Prompt 仍是自由文本，尚未接入提示词库选择器（后续独立需求）。
- `is_audit` 字段为建表遗留，业务未使用。
- 提示词列表无分页（与 variable/publication 同策略），数据量大时需补。
