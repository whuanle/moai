# Agent 运行时行为规格（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/bdd.md](../app/bdd.md)

## Feature：应用发布

### @AI-S1 @auto:e2e 管理员发布 Agent 应用
- Given 团队管理员在应用管理页
- When 点击「发布」
- Then 应用 `publish_status=1` 且记录发布时间

### @AI-S2 @auto:e2e 仅 Agent 应用可发布
- Given 一个流程应用
- When 管理员尝试发布
- Then 返回 400 且状态不变

### @AI-S3 @auto:e2e 非管理员不能发布
- Given 团队普通成员
- When 尝试发布应用
- Then 返回 403

### @AI-S4 @auto:e2e 取消发布
- Given 一个已发布应用
- When 管理员点击「取消发布」
- Then 状态回到草稿且成员无法再进入对话

## Feature：会话

### @AI-S5 @auto:e2e 成员对已发布应用开启会话
- Given 应用已发布且当前用户是团队成员
- When 创建会话
- Then 生成会话 id 且会话归属该用户

### @AI-S6 @auto:e2e 成员不能对未发布应用开启会话
- Given 应用处于草稿
- When 普通成员创建会话
- Then 返回 403

### @AI-S7 @auto:e2e 会话列表按最后消息倒序
- Given 用户已有多个会话
- When 打开对话页
- Then 只看到自己的会话且按最后消息时间倒序

### @AI-S8 @auto:e2e 会话越权访问按不存在处理
- Given 会话归属另一个用户
- When 当前用户查询其消息或续聊
- Then 返回 404

### @AI-S9 @auto:e2e 重命名与删除会话
- Given 当前用户的一个会话
- When 重命名为新标题或删除
- Then 标题更新 / 会话连同消息被删除

## Feature：对话与持久化

### @AI-S10 @auto:e2e AG-UI 流式回复
- Given 应用已配置对话模型
- When 前端发送一条消息
- Then 收到 `RUN_STARTED` → 文本增量 → `RUN_FINISHED` 的 SSE 事件序列

### @AI-S11 @auto:e2e 多轮上下文记忆
- Given 同一会话已进行一轮对话
- When 再发一条依赖上文的消息
- Then 模型回答体现对前文的记忆

### @AI-S12 @auto:e2e 对话消息持久化
- Given 一次对话完成
- When 重新打开该会话
- Then 能看到本轮消息（落库为压缩后视图）

### @AI-S13 @auto:e2e 未配置模型时给出错误
- Given 应用未选择对话模型
- When 发送消息
- Then 返回可读的运行错误而不是崩溃

## Feature：上下文策略

### @AI-S14 @auto:e2e 长对话触发压缩
- Given 会话消息超过压缩阈值
- When 运行对话
- Then 落库消息数不超过阈值且保留最近若干轮

### @AI-S15 @manual 摘要压缩可配
- Given `execution_settings.enableSummarization=true`
- When 运行对话
- Then 超阈值时调用摘要模型压缩旧对话

## Feature：知识库 RAG

### @AI-S16 @auto:e2e 绑定知识库后按需检索
- Given 应用绑定了含向量的知识库
- When 提问与知识库内容相关
- Then 模型可调用 `search_knowledge_base` 并基于检索结果回答

### @AI-S17 @manual 未绑定知识库不使用 RAG
- Given 应用未绑定知识库
- When 对话
- Then 不暴露检索工具

## Feature：用量

### @AI-S18 @auto:e2e 对话统计 token 用量
- Given 一次对话完成
- When 查看模型使用计数
- Then 按 `AiModelUseType.App` 累加本次 token 与会话累计

## Feature：对话页体验

### @AI-S19 @auto:vitest 沉浸式对话布局
- Given 成员进入已发布应用的对话页
- When 页面加载
- Then 呈现左侧会话列表 + 右侧对话流，空态展示欢迎语与建议问题
- And 不渲染页面级面包屑与大标题

### @AI-S19b @auto:vitest 明暗主题适配
- Given 用户切换明暗主题
- When 打开对话页
- Then 对话页配色随主题变化且无横向溢出

### @AI-S20 @auto:vitest 流式回复渲染
- Given 模型返回流式文本
- When 对话进行中
- Then 助手消息逐步增长并以 Markdown 渲染，附带复制与停止操作


## Feature：工具与知识库（渐进式披露）

### @AI-S21 @auto:e2e 插件转为可调用工具
- Given 应用绑定了静态/动态/MCP/OpenAPI 插件
- When 对话中需要该插件能力
- Then 该插件以工具形式可被发现并调用

### @AI-S22 @auto:e2e 知识库转为检索工具
- Given 应用绑定了知识库
- When 模型需要业务资料
- Then 存在 search_knowledge_base 工具可检索并返回命中片段

### @AI-S23 @auto:e2e 动态加载工具列表
- Given 应用绑定了若干工具
- When 调用 list_tools
- Then 返回工具的名称、说明与参数示例，不要求一次性注入全部工具定义

### @AI-S24 @auto:e2e 按名称调用工具
- Given 已通过 list_tools 取得工具名
- When 以 call_tool(toolName, argumentsJson) 调用
- Then 执行对应插件/知识库并返回结果

### @AI-S25 @auto:e2e 未绑定工具时不暴露工具
- Given 应用未绑定任何插件与知识库
- When 对话
- Then 不出现 list_tools/call_tool 元工具

### @AI-S26 @manual MCP / OpenAPI 工具调用
- Given 应用绑定了 MCP 或 OpenAPI 插件
- When 通过 call_tool 调用其函数
- Then 由 MCP 客户端或 OpenAPI HTTP 调用返回结果


## Feature：沙箱（OpenSandbox）

### @AI-S27 @auto:e2e 应用开启沙箱后暴露沙箱工具
- Given 团队管理员在应用配置中开启沙箱并保存
- When 进入对话并调用 list_tools
- Then 返回 sandbox_run_code / sandbox_run_shell / 文件读写等沙箱工具

### @AI-S28 @auto:e2e 未开启沙箱时不暴露沙箱工具
- Given 应用未开启沙箱
- When 调用 list_tools
- Then 不出现任何 sandbox_ 前缀工具

### @AI-S29 @auto:e2e 在沙箱中运行命令
- Given 应用已开启沙箱
- When 模型经 call_tool 调用 sandbox_run_shell 执行 echo
- Then 沙箱返回 stdout 且退出码为 0

### @AI-S30 @manual 沙箱代码解释器与文件读写
- Given 应用已开启沙箱
- When 依次运行代码、写入并读取文件
- Then 变量与文件在同一会话的沙箱内持续存在

### @AI-S31 @manual 沙箱按会话与 TTL 回收
- Given 同一应用的两个会话各自使用沙箱
- When 会话空闲超时或被删除
- Then 对应沙箱被销毁且不影响其他会话


## Feature：知识图谱检索工具（图检索消费层 SP-A）

### @AI-S32 @auto:e2e 知识图谱转为检索工具
- Given 应用绑定了已配置向量化模型的本团队托管知识图谱
- When 模型需要实体关系类资料（如「A 和 B 什么关系」「与 X 相关联的有哪些实体」）
- Then 存在 search_knowledge_graph 工具（Kind=graph）可检索并返回命中实体与一跳邻居（单图 topK 1-20）
- And 未配置向量化模型或不可用的绑定图计入 skipped 提示而不阻断其余图检索；返回载荷超 16KB 时截断并标记 hitsTruncated

### @AI-S33 @manual 未绑定知识图谱不暴露图检索工具
- Given 应用未绑定知识图谱
- When 对话
- Then 不出现 search_knowledge_graph 工具

