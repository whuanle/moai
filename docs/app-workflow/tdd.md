# 流程应用模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @WF-S1 | local-dev/workflow-e2e.mjs（WF-01） | PASS 36/36（2026-09-17） |
| @WF-S2 | local-dev/workflow-e2e.mjs（WF-04/06） | PASS 36/36（2026-09-17） |
| @WF-S3 | local-dev/workflow-e2e.mjs（WF-02/03） | PASS 36/36（2026-09-17） |
| @WF-S4 | local-dev/workflow-e2e.mjs（WF-07） | PASS 36/36（2026-09-17） |
| @WF-S5 | local-dev/workflow-e2e.mjs（WF-08） | PASS 36/36（2026-09-17） |
| @WF-S6 | local-dev/workflow-e2e.mjs（WF-09） | PASS 36/36（2026-09-17） |
| @WF-S7 | local-dev/workflow-e2e.mjs（WF-10） | PASS 36/36（2026-09-17） |
| @WF-S8 | local-dev/workflow-e2e.mjs（WF-11/12） | PASS 36/36（2026-09-17） |
| @WF-S9 | local-dev/workflow-e2e.mjs（WF-13） | PASS 36/36（2026-09-17） |
| @WF-S10 | local-dev/workflow-e2e.mjs（WF-15a~f，2026-09-20 起含开场白草稿/发布双轨断言） | PASS 96/96（2026-09-20） |
| @WF-S11 | local-dev/workflow-e2e.mjs（WF-18a~e） | PASS 47/47（2026-09-18） |
| @WF-S12 | local-dev/workflow-e2e.mjs（WF-18f~h） | PASS 47/47（2026-09-18） |
| @WF-S13 | local-dev/workflow-e2e.mjs（WF-18i） | PASS 47/47（2026-09-18） |
| @WF-S14 | local-dev/workflow-e2e.mjs（WF-19a~g） | PASS 54/54（2026-09-18） |
| @WF-S15 | @manual（浏览器走查：右键节点/连线菜单删除 + Ctrl+Z 撤销恢复） | PASS（2026-09-18） |
| @WF-S16 | tests WorkflowValidatorTests（多出边报错/条件放行 2 用例）+ ui utils.test（单出边用例）+ local-dev/workflow-e2e.mjs（54/54 无多出边） | PASS 23/23 / 27/27 / 54/54（2026-09-18） |
| @WF-S17 | @manual（浏览器走查：工具 Tab 列表 + 拖拽生成已绑定插件节点 + 表单内重新选择）+ ui utils.test（outputsFromPluginSchema 用例） | PASS / 28/28（2026-09-18） |
| @WF-S18 | local-dev/workflow-e2e.mjs（WF-20a~d：保存/缺分类 400/标记无效 400/发布） | PASS 60/60（2026-09-18） |
| @WF-S19 | local-dev/workflow-e2e.mjs（WF-20e~f：未配置模型节点失败挂起、下游未执行） | PASS 60/60（2026-09-18） |
| @WF-S20 | tests/MoAI.App.Workflow.Tests/QuestionClassifierNodeTests.cs（15 用例：序号/名称/兜底解析、提示词与历史截断、缺模型/分类/query 失败、校验 5 例） | PASS 38/38（2026-09-18） |
| @WF-S21 | @manual（浏览器走查：模型下拉/背景知识/聊天记录/用户问题绑定/分类增删与端口连线） | 待走查（2026-09-18） |
| @WF-S22 | local-dev/workflow-e2e.mjs（WF-21a~c：GET 参数插值/字段提取/下游引用） | PASS 70/70（2026-09-18） |
| @WF-S23 | local-dev/workflow-e2e.mjs（WF-21d：POST JSON 请求体与 Content-Type） | PASS 70/70（2026-09-18） |
| @WF-S24 | local-dev/workflow-e2e.mjs（WF-21e 非 2xx 挂起 + WF-21h 超时失败） | PASS 70/70（2026-09-18） |
| @WF-S25 | local-dev/workflow-e2e.mjs（WF-21f~g：hasError/errorMessage 输出、下游继续） | PASS 70/70（2026-09-18） |
| @WF-S26 | local-dev/workflow-e2e.mjs（WF-21i~j：非法方法/缺地址发布 400）+ tests HttpRequestNodeTests（校验用例） | PASS 70/70 / 50/50（2026-09-18） |
| @WF-S27 | @manual（浏览器走查：HTTP 节点表单/请求参数三 Tab/鉴权/报错捕获/字段提取/cURL 导入） | 待走查（2026-09-18） |
| @WF-S28 | local-dev/workflow-e2e.mjs（WF-22a~b：Member 创建会话 200/非成员 404） | PASS 82/82（2026-09-18） |
| @WF-S29 | local-dev/workflow-e2e.mjs（WF-22c~e：无模型流程对话失败可见/发布回显流程） | PASS 82/82（2026-09-18） |
| @WF-S30 | local-dev/workflow-e2e.mjs（WF-22f~h：userId/appId/conversationId/messageId/currentTime 注入、history 空）+ tests WorkflowSystemContextTests | PASS 82/82 / 53/53（2026-09-18） |
| @WF-S31 | local-dev/workflow-e2e.mjs（WF-22i~j：第二轮 history 累积、消息 user/assistant 交替落库） | PASS 82/82（2026-09-18） |
| @WF-S32 | local-dev/workflow-e2e.mjs（WF-22l：对话实例计入运行历史） | PASS 82/82（2026-09-18） |
| @WF-S33 | @manual（浏览器走查：系统设置面板「系统变量」只读分区 + 节点绑定 sys.* 提示） | 待走查（2026-09-18） |
| @WF-S34 | local-dev/workflow-e2e.mjs（WF-23a~c：问题透传流程发布/对话注入 start.question/query 镜像同值） | PASS（2026-09-20） |
| @WF-S35 | local-dev/workflow-e2e.mjs（WF-24a~c：十轮对话 sys.history 压缩有界 2≤h≤14） | PASS（2026-09-20） |
| @WF-S36 | @manual（浏览器走查：工作台头部 设计/调试/配置/运行历史 Tab 与成员可见性） | 待走查（2026-09-20） |
| @WF-S37 | @manual（浏览器走查：配置二级菜单 信息/日志/监控/外部渠道 + 信息页改头像） | 待走查（2026-09-20） |
| @WF-S38 | @manual（浏览器走查：调试分区会话历史/流式对话/开场白/未发布提示） | 待走查（2026-09-20） |
| @WF-S39 | @manual（浏览器走查：开始节点只读 question + 调试启动参数模板固定生成）+ ui utils.test（旧定义/旧草稿画布收敛 question 与 start.query 引用迁移 3 用例） | 待走查 / PASS 45/45（2026-09-20） |
| @WF-S40 | local-dev/workflow-e2e.mjs（WF-25a~g：未发布草稿调试成功/正式对话仍拒/成员携带头 403/实例记调试类型） | PASS 95/95（2026-09-20） |
| @WF-S41 | local-dev/workflow-e2e.mjs（WF-23d：旧编排草稿仅传 question 调试执行，query 镜像跑通） | PASS 97/97（2026-09-20） |
| @WF-S42 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（流程模式：UI 裁剪 + 不发起专家/用户配置请求） | PASS 16/16（2026-09-20） |
| @WF-S43 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（流程模式：发送创建会话不带专家并流式回显） | PASS 16/16（2026-09-20） |
| @WF-S44 | ui/src/pages/apps/__tests__/AppPlaza.test.tsx（公开已发布流程应用展示进入对话） | PASS 3/3（2026-09-20） |
| @WF-S45 | @manual（浏览器走查：结束节点「输出收集」绑定行增删改 + 删除残留引用） | 待走查（2026-09-20） |
| @WF-S46 | ui/src/pages/teams/apps/workflow/__tests__/utils.test.ts（删除节点后残留引用报错文案含节点名） | PASS 46/46（2026-09-20） |
| @WF-S47 | @manual（浏览器走查：AI 对话节点模型选择/系统提示词/温度 + 未配模型保存拦截） | 待走查（2026-09-20） |
| @WF-S48 | tests/MoAI.App.Workflow.Tests/AiChatNodeTests.cs（aiModelId 契约/旧键 model 兼容/输入覆盖优先级/温度归一化/缺 prompt）+ 真实模型端到端（debug-run：DeepSeek + systemPrompt + temperature completed） | PASS 5/5 / PASS（2026-09-20） |
| @WF-S49 | @manual（浏览器走查：专家提示词下拉选择填入 + 技能多选 + 沙箱开关） | 待走查（2026-09-20） |
| @WF-S50 | tests/MoAI.App.Workflow.Tests/AiChatNodeTests.cs（62/62，skillIds/sandboxEnabled 契约已移除）+ ui utils.test（历史残留键清洗丢弃）+ local-dev/workflow-e2e.mjs（118/118 回归；agentApp 节点 WF-26a~k 覆盖复杂 Agent 能力编排） | PASS 62/62 / PASS 54/54 / PASS 118/118（2026-09-21） |
| @WF-S51 | @manual（浏览器走查：Agent 应用节点选择器/循环禁选/保存校验）+ ui utils.test（agentAppId 清洗与未选应用报错） | 待走查 / PASS 50/50（2026-09-20） |
| @WF-S52 | tests/MoAI.App.Workflow.Tests/AgentAppNodeTests.cs（契约 4 用例）+ local-dev/workflow-e2e.mjs（WF-26a~k：Agent 节点执行/成环选项标记/保存与调试 400） | PASS 63/63 / PASS 108/108（2026-09-20） |
| @WF-S53 | ui/src/pages/teams/apps/workflow/__tests__/utils.test.ts（ensureCoreNodes 4 用例）+ local-dev/workflow-e2e.mjs（WF-04c~e：无开始/无结束/双开始草稿 400） | PASS 54/54 / PASS 111/111（2026-09-20） |
| 引擎行为（条件路由/跳过传播/恢复/插值/校验/条件脚本/Agent 应用节点） | tests/MoAI.App.Workflow.Tests（14 用例） | PASS 63/63（2026-09-20） |
| 引擎行为（知识库检索：变量绑定优先级/解析格式/兜底失败/输出结构） | tests/MoAI.App.Workflow.Tests/KnowledgeSearchNodeTests.cs（7 用例） | PASS 21/21（2026-09-18） |
| 引擎行为（HTTP 请求：方法/参数/头/鉴权/请求体组装、提取与报错捕获、超时、校验） | tests/MoAI.App.Workflow.Tests/HttpRequestNodeTests.cs（12 用例） | PASS 50/50（2026-09-18） |
| 引擎行为（sys 上下文注入/currentTime 内置/随实例持久化） | tests/MoAI.App.Workflow.Tests/WorkflowSystemContextTests.cs（3 用例） | PASS 53/53（2026-09-18） |
| 设计器转换层（往返/条件端口/校验/知识库检索/问题分类/HTTP 清洗与 cURL 解析/开始节点固定 question 收敛与旧引用迁移/校验错误定位节点名/aiChat 模型校验与新配置清洗含技能/沙箱/核心节点保护） | ui/src/pages/teams/apps/workflow/__tests__/utils.test.ts（54 用例） | PASS 54/54（2026-09-20） |
| 前端回归 | ui `npm run typecheck && npm run lint && npm run test` | 0 error / 0 error（存量 warning 8）/ 401/401（2026-09-20） |
| 后端构建 | `dotnet build src/MoAI/MoAI.csproj` | 0 error（2026-09-20；宿主 bin 被运行中实例锁定时用 `-o` 独立输出验证） |
| 回归对照 | local-dev/app-e2e.mjs（AP-18 契约更新为流程应用只写开场白） | PASS 121/121（2026-09-17） |

## 验证前置

1. 存量库执行 DDL：`psql -f asserts/app_workflow.sql`（新库由 EnsureCreated 自动建）。
2. 后端运行中：`cd src/MoAI && dotnet run`。
3. 执行：`node local-dev/workflow-e2e.mjs`。
| @WF-S54 | tests/MoAI.AI.Core.Tests/WorkflowAppChatClientTests.cs（事件映射/AI 节点文本过滤/classifier 过滤/回复去重/嵌套实例过滤/失败传播/退订 10 例）+ WorkflowAppChatMafIntegrationTests.cs（MAF 包装慢速 invoker 流式 + AGUI MapContent DataContent→CUSTOM 直连）+ local-dev/workflow-e2e.mjs（WF-27a~g：CUSTOM started/node/completed 序列、instanceId、AI 正文 TEXT_MESSAGE_CONTENT、回复不重复、事件先于正文） | PASS 65/65 / PASS 118/118（2026-09-20） |
