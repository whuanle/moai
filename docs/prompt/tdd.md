# 提示词（Prompt）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @PT-S1 | local-dev/prompt-e2e.mjs#PT-01~03 | PASS 46/46（2026-09-15） |
| @PT-S2 | local-dev/prompt-e2e.mjs#PT-02 | PASS 46/46（2026-09-15） |
| @PT-S3 | local-dev/prompt-e2e.mjs#PT-04~05 | PASS 46/46（2026-09-15） |
| @PT-S4 | local-dev/prompt-e2e.mjs#PT-06 | PASS 46/46（2026-09-15） |
| @PT-S5 | local-dev/prompt-e2e.mjs#PT-15a/b | PASS 46/46（2026-09-15） |
| @PT-S6 | local-dev/prompt-e2e.mjs#PT-07 | PASS 46/46（2026-09-15） |
| @PT-S7 | local-dev/prompt-e2e.mjs#PT-08~09a/b | PASS 46/46（2026-09-15） |
| @PT-S8 | local-dev/prompt-e2e.mjs#PT-09c/d | PASS 46/46（2026-09-15） |
| @PT-S9 | local-dev/prompt-e2e.mjs#PT-10 | PASS 46/46（2026-09-15） |
| @PT-S10 | local-dev/prompt-e2e.mjs#PT-11 | PASS 46/46（2026-09-15） |
| @PT-S11 | local-dev/prompt-e2e.mjs#PT-12 | PASS 46/46（2026-09-15） |
| @PT-S12 | local-dev/prompt-e2e.mjs#PT-13 | PASS 46/46（2026-09-15） |
| @PT-S13 | local-dev/prompt-e2e.mjs#PT-14 | PASS 46/46（2026-09-15） |
| @PT-S14 | local-dev/prompt-e2e.mjs#PT-15c/d | PASS 46/46（2026-09-15） |
| @PT-S15 | ui/src/pages/prompts/__tests__/PromptCenter.test.tsx#市场 Tab 渲染/切换我的提示词 | PASS 11/11（2026-09-16，全仓 vitest 270/270） |
| @PT-S16 | ui/src/pages/prompts/__tests__/PromptCenter.test.tsx#分类过滤/新建跳转/申请上架/删除 | PASS 11/11（2026-09-16，全仓 vitest 270/270） |
| @PT-S18 | ui/src/pages/prompts/__tests__/PromptEditor.test.tsx#所见即所得编辑保存/上限透传/空内容拦截/编辑回填/团队新建 | PASS 557/557（2026-10-26，全仓 vitest） |
| @PT-S20 | @manual（浏览器走查：双模式切换 + 内置工具栏即时成型 + 计数器，见 sop.md 第 3 节）+ PromptEditor.test.tsx#模式切换 | PASS（2026-10-26 走查：IR↔纯源码往返内容无损 1468/10000、单栏无分屏预览；vitest 561/561） |
| @PT-S19 | local-dev/prompt-e2e.mjs#PT-16 | PASS 46/46（2026-10-26） |
| @PT-S23 | ui/src/pages/prompts/__tests__/PromptEditor.test.tsx#草稿落盘恢复/编辑页草稿覆盖/保存后清除 | PASS 561/561（2026-10-26，全仓 vitest；浏览器实测刷新恢复 + toast） |
| @PT-S21 | ui/src/pages/teams/prompts/__tests__/TeamPrompts.test.tsx | PASS 3/3（2026-09-18，全仓 vitest 330/330） |
| @PT-S17 | @manual（浏览器走查，见 sop.md 第 3 节） | PASS（2026-10-26 编辑器所见即所得改版走查：新建输入即时渲染/编辑回填/暗色热切换；详情弹窗与团队分区待复走查） |
| @PT-S22 | 后端单测（绑定点计数：CreateAppSession/UpdateAppSessionPrompt Handler）+ API 实测 top_used 排序 | PASS（2026-09-20，浏览器实测 top10 返回与使用次数展示） |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                             # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test    # 前端全绿
node local-dev/prompt-e2e.mjs                                 # PT 40/40（需后端运行中）
node local-dev/publication-e2e.mjs                            # PB 34/34 回归（publication 个人分支改动）
```

## 本轮修复记录

- 2026-10-26 编辑器所见即所得改版（纯前端，参考 maomiedtor）：`PromptEditor` 由「左 Markdown TextArea + 右实时预览」改为整栏 Vditor IR 即时渲染编辑器，新增封装组件 `ui/src/components/VditorMarkdownEditor.tsx`（IR 默认、编辑模式切换 IR/所见即所得/分屏源码、主题暗色热切换、语言跟随 i18n、内置计数器 maxLength=10000 超限阻断、`cdn: '/vendor/vditor'` 本地资源）；`vite.config.ts` 新增 `copyVditorToPublic`（仿 monaco，复制 lute/i18n/icons/highlight.js/katex/css/images 子集到 `public/vendor/vditor/dist`，重型图示引擎不分发）；删除自研 `MarkdownToolbar.tsx`/`markdown.ts` 及其测试，`PromptEditor.test.tsx` 以受控 textarea 替身验证接线；i18n 清理 `prompt.tool*`/`preview*` 键并更新 `editorPlaceholder`。实踩坑：vditor 异步初始化完成（`after` 回调）前调用 `setTheme/setValue` 会因内部状态未就绪抛 `Cannot read properties of undefined (reading 'options')`，已用 `readyRef` 守卫 + `after` 内兜底补应用。
- 2026-10-26 编辑器页按钮布局收敛（用户反馈「取消/保存放最右边导致左侧大片空白」）：两按钮从 Page 页头 `extra` 移入信息行、紧跟名称输入框底对齐，名称框 240→200，编辑器高度改为 `calc(100vh - 210px)` 吸收页头释放的空间（实测 720 视口下 510px、底距 32px）。
- 2026-10-26 编辑器信息卡两行布局 + 双模式 + 浏览器草稿（用户第二轮反馈）：①描述从首行拆出独占全宽，首行为 名称+分类+取消/保存；②`VditorMarkdownEditor` 新增 `mode` 属性（`ir` 所见即所得 / `sv` 纯源码单栏 `preview.mode='editor'`），vditor 无公开 changeMode API 故 mode 变化重建编辑器（值经 props 延续），切换器为编辑器右上 Segmented（内置 edit-mode 工具项移除防状态脱节）；③浏览器草稿：名称/分类/描述/正文/模式防抖 500ms 落 `localStorage`（键 `moai-prompt-draft:edit:{id}` / `moai-prompt-draft:new:t{teamId}`），打开页面草稿叠加服务端内容并 toast（ref 守卫防 StrictMode 双 toast），保存成功先 clearTimeout 再删键（防已保存内容被写回），卸载时立即 flush；④坑：卸载后防抖定时器触发会用已销毁表单的空值覆盖草稿——值改为入队时捕获、卸载 flush；⑤新增 @PT-S23 场景与 4 个 vitest 用例（模式切换/草稿恢复/编辑页草稿覆盖/保存清草稿），i18n 增 modeWysiwyg/modeSource/draftRestored；⑥验证 vitest 561/561、typecheck/lint 绿、浏览器走查（两行布局、纯源码往返内容无损、刷新恢复 + toast）。

- 2026-09-18 团队提示词分区交互改版（纯前端）：固定卡片列表（新建/刷新并入查询行，不展示条数与视图切换）、卡片按被使用次数降序、分类筛选改为头部固定分类列表并显示 emoji（`classifyLabel`）、`PromptDetailModal` 去背景色/边框改横线分隔并新增复制按钮；提示词中心（市场/我的）同步：卡片一行 6 张、去掉「共 N 条」、分类 chip 显示 emoji；新增 [@PT-S21](./bdd.md#pt-s21) 与 TeamPrompts 组件测试；设计系统 `QueryBar` 新增 `extra` 插槽（[@FE-CB-S19](../components-base/bdd.md#fe-cb-s19)）。
- 2026-09-16 编辑保存 400（`$.promptId` 无法转 Int32）：`UpdatePromptCommand.PromptId` 暴露在 PUT 请求体 schema 中，Kiota 把前端传入的 null 原样序列化导致 System.Text.Json 绑定失败。修复：后端 `PromptId` 加 `[JsonIgnore]`（请求体不再携带，与"路由提供"决策一致）；前端 `updatePrompt` 改传真实 id 立即兼容运行中后端。**待后端重启后执行 `npm run syncapi`，再删除封装层中的 `promptId` 字段**。


- `UpdatePromptCommand` 不校验 `PromptId`（路由提供，body 携带 0 导致 PUT 400）。
- E2E PT-14 计数断言改为 `market_list` 读数（detail GET 自身会 +1）。
