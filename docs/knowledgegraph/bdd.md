# 知识图谱模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs)

## Feature: 托管图谱（managed）

```gherkin
@KG-S1 @auto:e2e
Scenario: 创建空白图谱
  When 管理员以空白模板创建知识图谱
  Then 返回图谱 id 且来源为托管
  And 该图谱的模型为空
  When 以含特殊字符（非中文/英文/数字/下划线）的名称创建
  Then 返回参数错误（名称仅允许中文、英文、数字和下划线）

@KG-S2 @auto:e2e
Scenario: 创建模板图谱（物流运输）
  When 管理员以「物流运输」模板创建知识图谱
  Then 返回图谱 id 且模型含实体类型 港口/航段/承运商 与关系类型 出发/抵达/承运
  And 出发与抵达约束为 航段→港口、承运约束为 承运商→航段
  And 航段预置属性含 运输方式/距离公里/运输价格/时效天（距离与价格为数字）
  And 11 个示例实例与 15 条示例关系一次性写入图库，示例航段携带距离与价格
  And 图库写入失败时清理残留并整体回滚（图谱不产生）
  And 该图谱可新增节点与边

@KG-S3 @auto:e2e
Scenario: 同团队名称唯一
  When 同团队再次使用已存在的名称建图
  Then 返回冲突

@KG-S4 @auto:e2e
Scenario: 维护模型
  When 管理员新增实体类型 服务/人员/项目
  Then 返回各类型 id
  When 管理员新增带起止约束的关系类型 维护（人员→服务）与 依赖（项目→服务）
  Then 返回关系类型 id
  And 查询模型回显上述起止约束

@KG-S5 @auto:e2e
Scenario: 新增节点校验
  When 使用不存在的实体类型新增节点
  Then 返回参数错误
  When 使用已存在的实体类型新增节点
  Then 返回节点 id

@KG-S6 @auto:e2e
Scenario: 新增边校验起止约束
  Given 图谱已有 人员/服务/项目 三类节点
  When 新增 维护 边但起点类型不符
  Then 返回参数错误
  When 新增 维护 边但终点类型不符
  Then 返回参数错误
  When 按 人员→服务 新增 维护 边
  Then 返回边 id

@KG-S7 @auto:e2e
Scenario: 节点与边分页
  Given 图谱有 5 个节点、2 条边
  When 按每页 2 条查询节点
  Then 首屏返回 2 条且总数为 5
  And 第三屏返回 1 条且总数为 5
  When 按每页 1 条查询边
  Then 返回 1 条且总数为 2
  And 按关系类型筛选边仍返回总数 2

@KG-S8 @auto:e2e
Scenario: 删除被引用的实体类型被拒
  Given 实体类型下仍有节点
  When 管理员删除该实体类型
  Then 返回冲突

@KG-S9 @auto:e2e
Scenario: 删除托管图谱清空图数据
  When 管理员删除托管图谱
  Then 返回成功
  And 该图谱详情返回不存在且团队列表不再包含
  And 图库中该图谱的节点与边被清空
```

## Feature: 外部接入图谱（connected）

```gherkin
@KG-S10 @auto:e2e
Scenario: 接入未知库名按方言处理
  When 管理员接入一个未知的库名
  Then neo4j 方言（多库实例）返回参数错误
  And memgraph 方言（社区版单库）登记成功且库名仅作标识，随后可删除

@KG-S11 @auto:e2e
Scenario: 接入现有数据库并内省模型
  Given 目标图数据库实例可达
  When 管理员接入其默认数据库
  Then 返回图谱 id
  And 模型来源为接入、只读为真且库名正确
  And 内省标签含 KgNode、关系类型含 KG_REL、属性键含 kgId
  And 详情同样为接入且只读

@KG-S12 @auto:e2e
Scenario: 接入图谱只读
  Given 图谱来源为外部接入
  When 尝试新增节点
  Then 返回冲突且提示只读
  When 尝试新增实体类型
  Then 返回冲突且提示只读
```

## Feature: 团队图谱列表工具栏搜索

```gherkin
@KG-S31 @auto:vitest
Scenario: 工具栏关键字搜索过滤卡片
  Given 团队下已有多个知识图谱
  When 在工具栏搜索框输入关键字（名称/描述子串，不区分大小写）并点「查询」或回车
  Then 卡片仅保留命中项，「共 N 条」按过滤后数量回显
  And 无命中时展示筛选空态（区别于完全无数据的空态）
  And 输入未触发查询前列表不过滤
  And 新建知识图谱按钮仍在操作区（Admin+，未开启能力时禁用）
```

## Feature: 团队图谱列表卡片布局（实时计数已退役，列表不触碰图库）

```gherkin
@KG-S32 @auto:vitest
Scenario: 列表卡片布局（实时资源数量展示已退役改写）
  Given 团队下已有知识图谱
  When 成员查询团队知识图谱列表
  Then 列表接口只读业务库（设置开关 + 图谱行），不触碰图数据库——响应项不再携带实体/关系数量（entityCount/relationCount 已随实时计数退役移除），数量在进入详情/模型页时按需统计
  And 卡片名称下方元信息行仅展示接入图徽标（外部接入 · 只读）
  And 卡片与知识库列表同构：头像+名称+元信息行、两行截断描述、分隔线页脚（创建时间+编辑/删除），列宽与知识库一致（超宽屏 1/6）
```

```gherkin
@KG-S34 @auto:e2e
Scenario: 图库不可用时列表秒回（限幅降级已退役改写：列表不触碰图库）
  Given 系统设置已开启知识图谱且团队下已有图谱，但图数据库连接不可用（Bolt 端口拒绝连接）
  When 成员查询团队知识图谱列表
  Then 接口毫秒级返回 200（列表纯业务库查询，原「单图统计限幅 3 秒 + 超时任务旁路泄漏」方案已整体移除，不再随图谱数产生图库探测任务）
  And enabled/myRole 正常回显，Admin+ 新建按钮随之出现
  And 图库不可用由具体操作端点（建图/画布/CRUD）连接失败自动报错（503），列表不承担探测职责
```

```gherkin
@KG-S47 @auto:e2e
Scenario: 列表响应不携带实时计数字段
  Given 系统设置已开启知识图谱且团队下已有图谱
  When 成员查询团队知识图谱列表
  Then 响应项不含 entityCount/relationCount 字段（KnowledgeGraphItem 已删除两属性），enabled 开关仍随响应回显供前端拦截
```

## Feature: 图览画布与权限（v2）

```gherkin
@KG-S13 @auto:e2e
Scenario: 画布有界子图
  Given 托管图谱已有节点与边
  When 查询画布子图（limit 为加载上限，后端钳制 1-500，默认 200）
  Then 返回节点与节点集内部的边，超出上限时 truncated=true
  When 按名称关键字过滤
  Then 仅返回命中的节点

@KG-S14 @auto:e2e
Scenario: 一跳邻接展开
  Given 图谱中存在一条维护边
  When 对起点节点查询一跳邻接
  Then 返回邻居节点与相连的边
  When 对不存在的节点查询邻接
  Then 返回不存在

@KG-S15 @manual
Scenario: 成员对图谱全只读
  Given 普通成员可浏览图谱、模型与节点
  When 成员尝试新增节点或边
  Then 返回禁止（v2 权限收紧：节点/边写操作仅限管理员及以上，v1 曾放开成员）

@KG-S16 @auto:e2e
Scenario: 图谱名称全局唯一
  Given 其他团队已存在同名图谱
  When 本团队使用该名称建图
  Then 返回冲突

@KG-S25 @manual
Scenario: 图览交互（拖动/详情/关系信息）
  Given 图览画布已加载节点与边
  When 拖动节点
  Then 节点位置随拖动调整且松手后保持
  When 点击节点
  Then 展开一跳邻接并弹出详情抽屉（类型/描述/属性按模型定义排序）
  When 点击边
  Then 弹出关系详情（关系类型名与起止节点）
  And 边中段显示关系类型名标签
  When 编辑态从节点连接桩拖线到另一节点（React Flow 连接桩，v3.1 起替代两段式点击）
  Then 弹出建立关系弹窗且起止节点回显正确

@KG-S27 @manual
Scenario: 图览页结构与工具栏
  Given 进入托管图/接入图的图览页（画布引擎为 React Flow + d3-force，v3.1 起）
  Then 头部不显示「定义模型>录入实例」步骤条，也无 AI 导入按钮
  And 左侧为二级菜单，右侧画布占据全部剩余宽度与视口剩余高度（小屏保底 320px）
  And 画布内置缩放/适应控件（左下）与原生全屏按钮（右上，全屏后铺满整屏，图标切换为退出全屏）
  And 节点为卡片（类型色点 + 名称 + 类型名），边带箭头与关系名标签
  And 初次加载与重新加载/筛选/搜索/调整上限后视口适配完整显示全图；点选展开邻接不重置视口
  When 图库节点量超过加载上限（可选 100/200/500，默认 200）
  Then 展示截断提示（含当前上限值），可用筛选/搜索缩小范围

@KG-S28 @auto:vitest
Scenario: 维护页步骤条承载模型/实例/关系
  Given 托管图菜单点击「维护」进入维护页
  Then 头部为步骤条：① 定义模型 → ② 录入实例 → ③ 连接关系
  When 点击步骤或带 ?step= 进入
  Then 内容区渲染对应维护组件（模型/实例/关系）
  And 实例步骤不再提供 AI 导入入口（v3.2 起统一收敛至「导入」菜单页）
  And 详情菜单为 图览/维护/导入/MCP/设置（v3.2 新增导入与 MCP）
  And 页内互跳（查看实例/查看关系/去定义模型/关联）均指向维护页对应步骤并保留过滤参数
  And 画布编辑（v3.1）：右键画布空白新建实体、右键节点/边删除、编辑态节点连接桩拖线建关系

@KG-S23 @auto:vitest
Scenario: 进入图谱默认图览且菜单收敛
  Given 团队知识图谱列表已有图谱
  When 点击图谱卡片进入详情（未带分区路由）
  Then 默认处于图览且画布子图接口被调用
  And 托管图菜单收敛为 图览/维护/设置（模型/实例/关系不再作为独立菜单）
  And 接入图详情同样默认图览（仅图览/模型/设置可选）
  When 访问旧版独立菜单路径 /entities、/relations、/schema（托管图）
  Then 重定向到维护页对应步骤（?step=entities|relations|schema），旧链接不 404
```

## Feature: AI 导入文件生成图谱（v2.9；v4.1 异步任务化）

```gherkin
@KG-S26 @auto:e2e
Scenario: AI 导入文件生成图谱（异步任务）
  Given Owner/Admin 已为托管图定义模型（实体类型/关系类型）且团队有可用对话模型
  When 在「导入」菜单页 AI 智能导入页签上传文档并选择模型发起导入
  Then 登记导入任务并返回任务 id（不再同步等待）
  And 任务依次经历 提取文本 → AI 抽取 → 合并落库 到达终态 done
  And 文件经 Maomi.ToMarkdown 提取内容（超上限截断），对话模型按图谱现有模型抽取实体与关系
  And 仅模型已定义的类型被写入，关系起止约束校验通过后入图
  And 任务结果携带导入统计（新建/合并/跳过）且画布可见新节点与边
  When 对非公开目录的 objectKey 发起导入
  Then 返回参数错误
  When 对没有实体类型的图谱发起导入
  Then 返回冲突（409）并提示先定义模型
```

```gherkin
@KG-S42 @auto:e2e
Scenario: 导入任务状态机与治理
  Given 托管图存在至少一条导入任务
  When 查询导入任务列表（分页倒序）与任务详情
  Then 返回状态（pending/extracting/parsing/merging/done/failed/cancelled）、进度（done/total）、错误与结果统计
  When 对 done 任务发起重试或取消
  Then 返回冲突（409）
  When 对 failed/cancelled 任务发起重试
  Then 任务重置为排队中并重新执行（幂等合并不产生重复数据）
  When 对排队/执行中任务发起取消
  Then pending 立即取消，执行中在阶段间隙生效

@KG-S43 @auto:e2e
Scenario: 重复导入幂等合并（B1 证据链）
  Given 同一文档已完成一次导入
  When 再次导入同一文档
  Then 任务终态 done 且统计为 新建 0 / 合并 2 / 新边 0
  And 命中节点描述去重拼接、缺失属性补齐、来源证据追加（FIFO 上限 20 条）
  And 命中同三元组的边跳过不重建
  And 图库节点与边总数不变
```

```gherkin
@KG-S44 @auto:e2e
Scenario: 检索邻居按度数排序与预算裁剪（B3）
  When 语义检索或召回测试携带邻居数参数（0=默认 10，1-30）
  Then 每个命中实体的一跳邻居按度数降序取 Top 进入上下文（邻居行携带度数），同一邻居的多条关系逐边成行
  When 邻居数越界（>30 或负数）
  Then 返回参数错误（400）
  And 命中文本化按预算（8192 字符）整段组装，预算耗尽丢弃尾部低分命中并置 textTruncated 标记

@KG-S45 @auto:e2e
Scenario: 默认抽取模型三级回退（B5）
  Given 管理员在图谱设置页配置了默认抽取模型（团队可用对话模型）
  When AI 导入未显式指定模型
  Then 使用图谱默认抽取模型执行任务
  When 请求级指定模型
  Then 请求级优先于图谱默认
  When 两者皆未提供
  Then 返回参数错误（400）提示选择模型或配置默认
  When 配置的模型不是对话模型或团队不可用
  Then 配置返回参数错误（400）
```

```gherkin
@KG-S48 @manual
Scenario: 画布问图（Text2Cypher，B7/D31）
  Given 托管图有可用对话模型（或已配默认抽取模型）
  When 画布搜索框切到「AI 问图」输入自然语言问题并回车
  Then 模型按图谱结构生成只读 Cypher（模型三级回退：请求级 &gt; 图谱默认 &gt; 400）
  And 生成的 Cypher 通过只读守卫（写关键字/多语句/超长/字符串字面量剥除后校验）并由 $kgId 隔离执行
  And 结果卡展示生成的 Cypher（可复制）与行数（500 行封顶，超出置截断）
  And 结果行中的本图节点 id 在画布上高亮（缺失节点可双击展开邻接补拉）
  When 生成的 Cypher 未通过守卫或执行失败
  Then 展示教学式错误原因与生成的 Cypher 原文
  When 对接入图发起画布问图
  Then 返回冲突（409，接入图问图走 kg_cypher_query 插件）
```

```gherkin
@KG-S49 @manual
Scenario: 画布样式规则（B8）
  Given 托管图已定义带属性的实体类型
  When 管理员在设置页「画布样式规则」为某类型选择追加显示内容（属性值/类型名/不显示）并保存
  Then 规则持久化到图谱（captions 键=实体类型 id，值=属性名或空串）
  When 图览画布加载该类型节点
  Then 节点 label 以「名称 · 所选内容」追加显示（所选属性缺失时仅显示名称；未配置规则的类型保持纯名称）
  When 规则包含不存在的实体类型 id 或超限（>50 条/属性名>100）
  Then 返回参数错误（400）
  When 接入图配置样式规则或普通成员操作
  Then 返回冲突（409）/权限错误（403）
  When Captions 传 null（前端全选「不显示」保存）
  Then 清除全部规则回默认
```

> JSON 结构化导入场景（@KG-S29，导入页 /import-json）：与外部 /import 共用管线（类型名引用/自动建类型/业务 key upsert/端点引用/逐条报告）。

```gherkin
@KG-S29 @auto:e2e
Scenario: JSON 结构化导入（导入页）
  When 提交非法 JSON 文本
  Then 返回 400 且消息含解析定位
  When nodes 与 edges 同时为空
  Then 返回 400
  When 以 validateOnly 预检（含新类型名/重复 key/坏端点）
  Then 返回预测计数与逐条结果，不建类型不落库
  When 正式导入（autoCreateTypes）
  Then 类型自动创建并回显、节点边落库、坏行逐条报告（类型不存在/重复 key/端点未命中）
  When 以页面控件 mode=upsert 重复导入
  Then 已有节点更新、等价边幂等跳过（控件值覆盖 JSON 内同名字段）
  When 提交 501 个节点
  Then 超限 400
  When 点击「下载示例 JSON」
  Then 下载包含 nodes/edges 结构（key/类型名引用/属性/端点两种引用形态）的示例文件
  When 图谱已配置向量化模型且开启疑似重复检测
  Then 新建节点与图谱已有节点（同名不同类型等）或本批次其他行向量相似度超阈值时，响应返回疑似重复对（名称/来源/得分）
  But 关闭 detectDuplicates 或图谱未配置向量化时不检测（结果为空），检测失败不影响导入
```

@KG-S30 @auto:vitest
Scenario: MCP 地址页
  Given 托管图详情左侧菜单含「MCP」（接入图无此菜单）
  When 进入 MCP 菜单
  Then 展示该图谱的 MCP 端点地址（serverinfo serviceUrl + /api/teamres/knowledge-graph/{kgId}/mcp）并可复制
  And 展示四个只读工具清单与鉴权提示（勾选「知识图谱 MCP」范围的接入 key）
```


## Feature: 接入图动态识别（v2.1）

```gherkin
@KG-S17 @auto:e2e
Scenario: 接入图画布有界子图
  Given 外部服务已向图库写入节点与边
  When 查询接入图画布子图
  Then 返回带标签（entityLabel）的节点与带关系类型名（relationName）的边
  When 按标签过滤
  Then 仅返回该标签的节点

@KG-S18 @auto:e2e
Scenario: 接入图一跳邻接展开
  Given 接入图画布已返回节点
  When 对某节点按 elementId 查询一跳邻接
  Then 返回邻居节点与相连的边
  When 对不存在的节点查询邻接
  Then 返回不存在

@KG-S19 @auto:e2e
Scenario: 内省缓存与强制刷新
  Given 接入图 schema 已查询过一次
  When 五分钟内再次查询 schema
  Then 命中 Redis 缓存（fromCache=true）
  When 以 refresh=true 强制刷新
  Then 跳过缓存重新内省（fromCache=false），并与基线 diff 出新增/消失的标签与关系类型
```

## Feature: 图谱头像（v2.2）

```gherkin
@KG-S20 @auto:e2e
Scenario: 设置图谱头像
  Given Owner/Admin 已通过存储直传完成图片上传并登记
  When 以 objectKey 设置图谱头像
  Then 设置成功，详情与列表回显 avatarPath
  When 以未登记的 objectKey 设置头像
  Then 返回不存在（404，防伪造 objectKey）

@KG-S24 @auto:vitest
Scenario: 新建图谱时上传头像
  Given Owner/Admin 打开新建知识图谱弹窗
  When 选择本地图片（仅图片、不超过 5MB）并提交创建
  Then 图谱创建成功且头像经存储直传登记（登记失败不阻断创建）
  And 团队图谱列表卡片与图谱详情回显该头像
  When 未选择头像直接提交
  Then 图谱正常创建且不调用头像登记

@KG-S33 @auto:vitest
Scenario: 编辑图谱时上传头像
  Given Owner/Admin 打开编辑知识图谱弹窗（列表卡片编辑入口）
  Then 弹窗展示头像上传控件，已有头像回显当前头像预览
  When 选择本地图片（仅图片、不超过 5MB）并提交保存
  Then 基础信息保存成功且头像经存储直传登记（登记失败不阻断保存）
  And 团队图谱列表卡片回显新头像
  When 不选择头像直接提交
  Then 仅保存基础信息且不调用头像登记，已有头像保持不变
```

## Feature: 模型属性设置（v2.3）

```gherkin
@KG-S21 @auto:e2e
Scenario: 实体类型属性定义与实例属性
  Given Owner/Admin 在模型页为实体类型定义属性（名称/类型/必填/说明）
  When 查询 schema
  Then 回显属性定义；属性名重复或类型非法返回 400
  When 录入实例并填写属性值
  Then 属性值以 JSON 存储在图库节点（propsJson）并在实例列表回显
  When 编辑实例属性
  Then 新值覆盖旧值并回显
```

## Feature: 超管系统设置（v2.4）

```gherkin
@KG-S22 @auto:vitest
Scenario: 设置卡片折叠与图数据库类型排版
  Given 超管打开系统设置
  Then 知识图谱卡片默认展开，点击卡片标题可折叠（连接字段随折叠隐藏）与再展开
  When 开启知识图谱
  Then 「图数据库类型」下拉与说明文案纵向排列
  And 用户名与密码输入框同行展示
  And 开启区操作行含「测试连接」按钮（用表单当前值实测，无需先保存）
```

```gherkin
@KG-S46 @auto:e2e
Scenario: 图数据库连通性预检端点（开启前测试，仅 root）
  Given 系统设置已保存图数据库连接信息
  When 超管以空请求体调用 POST /api/knowledge-graph/test-connection
  Then 回退已保存连接实测：可达返回 200 {ok:true, latencyMs, dialect}（临时驱动不复用运行时单例，探活语句与建接入图同口径：memgraph RETURN 1 / neo4j CALL db.labels()）
  When 超管以错误地址（bolt://127.0.0.1:1）覆盖调用
  Then 8 秒整体限幅内返回 200 {ok:false, error=原因}（连接超时 5 秒；驱动不响应取消令牌，限幅任务竞争裁决后 Dispose 中断，不 5xx 不挂起）
  When 超管以非法地址（not-a-uri）或非法方言（mysql）调用
  Then 返回 400（IModelValidator 校验：bolt/neo4j scheme、方言白名单）
  When 未认证或普通用户调用
  Then 分别返回 401 / 403（角色门禁只在 Controller）
  And 设置页保存流程：开启时先保存连接项再实测，通过才落 KG_ENABLED=true；测试未通过不落开关、表单值保留可改后重试；关闭时仅写开关不写连接项
```

> 外部开放接口（`/api/teamres/knowledge-graph`）场景编号沿用证据脚本 `kg-external-e2e.mjs` 的 KX-\* 体系（KX-01~KX-15），不复用 KG-\*。授权模型：moai-ac- 接入 key/token 即团队级授权（kg_read/kg_write 分档已随 scope 体系退役），moai-tr- 图资源 key 受包围圈约束（[@KX-15](#kx-15)），设计见 [sdd.md §5.1](./sdd.md#51-外部开放接口apexternalknowledge-graph)。

## Feature: 外部开放接口（应用 token，KX-*）

```gherkin
@KX-01 @auto:e2e
Scenario: 应用 token 换取与团队级图谱列表
  When 以应用接入 key 换取应用 token
  Then 返回类型为「应用」的 token 对
  When 查询外部图谱列表
  Then 仅返回本团队托管（managed）图谱，不含他团队与接入图
  When 另一团队的应用 token 查询列表
  Then 仅返回该团队自己的图谱

@KX-02 @auto:e2e
Scenario: 跨团队与不存在资源一律不存在
  When 对他团队图谱发起任意读写
  Then 返回不存在（404，不泄露资源存在性）
  When 以随机 id 查询 schema 或节点列表
  Then 返回不存在

@KX-03 @auto:e2e
Scenario: 实体类型与关系类型维护
  When 新增实体类型与带起止约束的关系类型
  Then schema 回显类型与约束
  When 更新与删除类型
  Then schema 同步且仍被引用的类型删除返回冲突（409）

@KX-04 @auto:e2e
Scenario: 节点增删改查与分页邻接
  When 新增节点、分页查询、查详情与一跳邻接、更新、删除
  Then 全部成功且 schema 计数随写入回退
  And 删除节点连带其边（DETACH 级联）

@KX-05 @auto:e2e
Scenario: 边增删改查与起止约束
  When 新增符合约束的边
  Then 返回边 id 且分页/详情正确
  When 新增违反起止约束的边
  Then 返回参数错误（400）

@KX-06 @auto:e2e
Scenario: 节点与边批量导入整批拒绝
  When 批量导入 ≤200 条且全部合法
  Then 逐条落库并回写 id
  When 任一条类型/端点非法或超过 200 条上限
  Then 整批拒绝（400）且已有数据不变

@KX-07 @auto:e2e
Scenario: 接入图对外部只读
  When 对接入（connected）图谱发起节点或类型写操作
  Then 返回冲突（409，接入图只读）

@KX-08 @auto:e2e
Scenario: 外部接口仅接受应用 token
  When 无 token、伪造 token 或内部用户 JWT 调用外部接口
  Then 分别返回未认证/未认证/未认证或禁止

@KX-09 @auto:e2e
Scenario: 全量凭证读写（kg_read/kg_write 分档已退役改写）
  When 以 moai-ac- 接入 key 换取的应用 token 查询模式与节点列表
  Then 放行
  And 建实体类型等写接口与跨资源组（知识库 list）均放行（无范围分档）
  When 以 moai-ac- key 直连读写
  Then 读与写均放行（接入创建不传/传 scopes 均被忽略）

@KX-10 @auto:e2e
Scenario: 外部批量导入（类型名引用 + 业务 key + 名称引用 + 逐条失败报告）
  When 以 autoCreateTypes 导入携带业务 key、实体类型名称与属性的节点行，及关系类型名称 + 端点按 key/名称引用的边行
  Then 类型不存在时自动创建（实体类型属性定义取该类型各行属性键并集；关系类型约束为任意）并在响应回显创建清单
  And 节点与边逐条落库，响应按 created/failed 计数并逐行回 id 或失败原因
  But 类型不存在且未开启自动创建、key 重复、端点 key 未命中的行仅逐条失败，不阻断其他行
  When 按 key 查询节点详情
  Then 回显名称与属性；不存在的 key 返回 404

@KX-11 @auto:e2e
Scenario: 幂等重导与按 key 维护（upsert / key 收养 / 只读门禁 / 批删）
  When 以相同数据重导（upsert 模式），其中一行改用新 key 但（类型+名称）与现有节点相同
  Then 已有节点按 key 更新（updated 计数、不新建），边按（起点,关系,终点）幂等跳过
  And 改名行整体覆盖名称与属性（未传属性视为清空）；无 key 行按（类型+名称）匹配并收养新 key
  When 以 moai-ac- 全量 token 调导入或批删
  Then 导入预检与批删均放行（原 kg_read 只读 token 403 断言随 scope 退役删除）、按 key 查询放行
  When 按业务 key 批量删除节点（含不存在的 key）
  Then 实际删除数准确返回、不存在的 key 忽略，节点连带其边删除（DETACH），按 key 查询转 404

@KX-12 @auto:e2e
Scenario: 接入图外部导入只读
  When 对接入（connected）图谱发起批量导入
  Then 返回冲突（409，接入图只读）

@KX-13 @auto:e2e
Scenario: 按 key 同步闭环（key 收养 / keys 枚举 / 按引用删边）
  When 单节点创建或修改时携带业务 key
  Then 按 key 可查；对无 key 节点 PUT 附 key 即收养
  When 分页枚举图内已落 key 的节点（keys/list）
  Then 返回 key/nodeId/名称/类型且分页正确，可用于全量比对找删除差集
  When 以关系类型（id/名称）+ 端点（nodeId/key/名称）引用批量删除边
  Then 命中的边删除（含同三元组平行边）且计数准确；重复删除幂等计 0；端点 key 未命中的行失败不阻断其他行
  When 按「先删边后删点」顺序清理同步数据
  Then keys/list 回到空基线

@KX-14 @auto:e2e
Scenario: 导入预检（validateOnly 只读预测）
  When 以 validateOnly=true 提交导入（含新类型名/重复 key/自环边/坏端点）
  Then 返回与真实导入同形的预测计数与逐条结果，created 行 id 为空、将创建的类型名照常回显
  But 不建类型（schema 不变）、不写图库（by-key 404）、不发向量增量
  When 以同体（validateOnly=false）真实导入
  Then 类型创建、节点边落库，坏行仍逐条报告

@KX-15 @auto:e2e
Scenario: 图资源 key（moai-tr-）包围圈
  When 团队管理员为本图生成资源 key
  Then 返回 moai-tr- 前缀 key（get-or-create 幂等）
  When 以资源 key 直连本图 REST 读接口
  Then 放行
  And 团队级知识图谱列表返回 200 且仅含绑定图（OnlyGraphId 过滤）
  But 越 wiki 端点或越圈其它图返回 403 insufficient_scope
```

> 知识图谱 MCP 服务器场景编号沿用证据脚本 `kg-mcp-e2e.mjs` 的 KGM-\* 体系，不复用 KG-\*/KX-\*。与知识库 MCP 同构：moai-ac- 接入 key/token（kg_mcp 范围已随 scope 体系退役）+ 本图 moai-tr- 资源 key（包围圈内）+ kgId 归属门禁，仅托管图参与，工具域隔离。

## Feature: 知识图谱 MCP 服务器（KGM-*）

```gherkin
@KGM-S1 @auto:e2e
Scenario: 鉴权门禁
  When 无凭证、伪造 key 调用 MCP 端点
  Then 返回未认证（401）
  When 以 moai-ac- 应用接入 key（Bearer/x-api-key，范围概念已退役）或本图 moai-tr- 资源 key initialize
  Then 握手成功
  But 以 wiki 的 moai-tr- 资源 key 越域连接 403（insufficient_scope）；kgId 不存在与跨团队一律 404

@KGM-S2 @auto:e2e
Scenario: 工具域隔离
  When 查询 KG MCP 端点工具列表
  Then 恰好返回四个只读工具（图谱列表/schema/节点搜索/向量召回）且不含知识库工具
  And 知识库 MCP 端点工具列表仍恰好为三个知识库工具（反向隔离）
  When 在 KG 端点调用知识库工具
  Then 被门禁拒绝

@KGM-S3 @auto:e2e
Scenario: 图谱列表
  When 调用 list_knowledge_graphs
  Then 返回本团队托管图谱（不含他团队）

@KGM-S4 @auto:e2e
Scenario: 图谱 schema
  When 调用 get_knowledge_graph_schema（不传 kgId 默认用接入地址中的图谱）
  Then 返回实体类型（含属性定义与必填标记）与关系类型（含起止类型约束）
  But 跨团队 kgId 参数拒绝

@KGM-S5 @auto:e2e
Scenario: 节点搜索
  When 按关键字/实体类型/分页搜索节点
  Then 返回节点 id、名称、描述与实体类型名；跨团队 kgId 拒绝

@KGM-S6 @auto:e2e
Scenario: 向量召回
  When 空查询或非法阈值
  Then 逐条拒绝
  When 图谱未配置向量化模型时召回
  Then 返回空结果与 skippedHint 可读说明（不报错）
  When 图谱已配置向量化后召回
  Then 命中实体带类型名/得分（降序）与一跳邻居（方向/关系名/名称）；阈值与 top 生效

@KGM-S7 @auto:e2e
Scenario: 协议行为
  When 未知工具调用
  Then 被拒（JSON-RPC error 或 isError 结果）
  When GET 访问端点
  Then 405；无状态模式免 initialize 直接调用成功；通知返回 202

@KGM-S8 @auto:e2e
Scenario: 绑定图资源 key 访问本图 MCP
  When 以本图的 moai-tr- 资源 key initialize
  Then 握手成功并返回服务器信息
```

> Text2Cypher 查图插件消费场景编号沿用证据脚本 `kg-text2cypher-e2e.mjs` 的 KT-\* 体系（@KT-S1~S10），不复用 KG-\*/KX-\*。插件本体与安全设计见 [Text2Cypher 设计文档](../superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md)。**E2E 已通过 15/15（2026-09-22，真实后端）**。

## Feature: Text2Cypher 查图插件消费（KT-S*）

```gherkin
@KT-S1 @auto:e2e
Scenario: 实例创建与模板注册
  Given 动态插件注册表已自动扫描到内置模板 kg_cypher_query（isDynamic）
  When 团队管理员以 templeteKey=kg_cypher_query 绑定本团队托管图创建实例
  Then 创建成功且团队动态模板列表含 kg_cypher_query

@KT-S2 @auto:e2e
Scenario: 越团队绑定图谱被拒
  Given 其他团队已存在一张托管图
  When 本团队创建 kg_cypher_query 实例时绑定该他团队图谱
  Then 返回禁止（403，实例保存校验图谱归属）

@KT-S3 @auto:e2e
Scenario: schema 自描述
  Given 实例已绑定托管图且图内已有实体类型与节点
  When 以 {"schema":true} 运行实例
  Then 返回摘要含实体类型（≥1）、usage 指引（含 $kgId 用法）

@KT-S4 @auto:e2e
Scenario: 合法只读查询
  When 提交含 {kgId: $kgId} 过滤的只读 Cypher
  Then 返回结果表格且行数 ≥1

@KT-S5 @auto:e2e
Scenario: 缺 $kgId 的查询被拒
  When 提交未含 $kgId 占位符的查询（托管图）
  Then 返回失败且错误文案含 $kgId 教学指引

@KT-S6 @auto:e2e
Scenario: 写语句全部拒绝
  When 分别提交 CREATE/MERGE/DETACH DELETE/SET/CALL 语句
  Then 全部返回失败且错误提示只读（守卫先于连接，不依赖图库可达）

@KT-S7 @auto:e2e
Scenario: 行数超上限截断
  Given 图内节点数大于实例 maxRows（如 maxRows=2 而图内 4 节点）
  When 提交不带 LIMIT 的全量查询
  Then 返回行数等于 maxRows 且 truncated=true

@KT-S8 @manual
Scenario: 超时配置生效
  Given 实例 timeoutSeconds 配置为极小值
  When 执行注入负载的慢查询
  Then 查询按超时失败且会话不挂起（依赖慢查询负载，人工验证）

@KT-S9 @auto:e2e
Scenario: 接入图查询无需 $kgId
  Given 实例绑定的图谱为外部接入图（整库即图，天然隔离）
  When 提交不含 $kgId 的只读 Cypher
  Then 查询成功（图库不可达时本场景自动降级 SKIP）

@KT-S10 @auto:e2e
Scenario: 非成员团队运行实例被拒
  When 其他团队以本团队实例 key 运行插件
  Then 返回不存在（404）或失败，不泄露实例与图数据
```

> 图检索消费层场景编号沿用证据脚本 `kg-search-e2e.mjs` 的 KGS-\* 体系（@KGS-S1~S9），不复用 KG-\*/KX-\*/KT-\*。设计见 [图检索消费层设计文档](../superpowers/specs/2026-09-22-kg-graph-search-design.md)。**E2E 40/40 已通过（2026-09-22，真实后端 + 本地 embeddings 桩，零 SKIP）**。

## Feature: 图检索消费层（KGS-S*）

```gherkin
@KGS-S1 @auto:e2e
Scenario: 托管图配置向量化模型
  Given 本地 embeddings 桩渠道与向量化模型已授权本团队
  When 创建托管图并配置向量化模型（维度 1024）
  Then 配置成功且重复配置幂等
  And 图谱详情回读向量化模型与维度

@KGS-S2 @auto:e2e
Scenario: 节点向量化与语义检索命中
  Given 托管图已配置向量化模型
  When 新增实体类型、带起止约束的关系类型与节点
  Then 轮询检索命中节点且命中项含 图谱 id/节点 id/类型 id/类型名/score
  And 无边节点的邻居为空数组且响应含 contents/text/skippedHints 结构
  When 建边后检索新节点
  Then 命中且邻居含方向（out/in）、关系类型名与描述

@KGS-S3 @auto:e2e
Scenario: 改名后向量替换
  When 修改一个已可检索命中节点的名称
  Then 新名可检索命中且旧名不再命中（向量替换幂等）

@KGS-S4 @auto:e2e
Scenario: 删节点后向量移除
  When 删除一个已可检索命中的临时节点
  Then 轮询检索不再命中该节点

@KGS-S5 @auto:e2e
Scenario: 相似度阈值过滤
  When 以高于全部命中 score 的 minScore 检索
  Then 返回命中为空
  When 缺省 minScore 检索
  Then 返回命中非空

@KGS-S6 @auto:e2e
Scenario: 未配向量化模型检索被拒
  When 创建第二张托管图且不配置向量化模型，对其发起检索
  Then 返回冲突（409）且文案含「向量化」

@KGS-S7 @auto:e2e
Scenario: 向量化配置边界校验
  When 以维度 0 配置向量化模型
  Then 返回参数错误
  When 以不存在的模型配置向量化
  Then 返回参数错误
  And 非法配置不破坏原配置（检索仍命中）

@KGS-S8 @auto:e2e
Scenario: 应用配置绑定知识图谱
  When Agent 应用配置绑定他团队知识图谱
  Then 返回参数错误且文案含「知识图谱」
  When 绑定本团队接入图
  Then 返回参数错误（仅托管图可绑定）
  When 绑定本团队托管图
  Then 保存成功且配置回读含该图谱

@KGS-S9 @auto:e2e
Scenario: 工作流图检索节点
  When 流程草稿的图检索节点引用他团队图谱
  Then 保存草稿返回参数错误且文案含「知识图谱」
  When 改为本团队图谱后保存草稿、发布并调试执行
  Then 运行完成且节点输出 count≥1、text 非空

@KGS-S10 @auto:e2e
Scenario: 召回测试（/recall-test）
  When 空查询、相似度阈值越界或开启 AI 增强但未选对话模型
  Then 返回参数错误（400）
  When 输入问题发起召回测试
  Then 返回向量召回的实体命中（得分/类型名/一跳邻居）且 topK 生效
  When 开启 AI 优化问题与 AI 回答并选择对话模型
  Then 返回优化后查询与基于召回事实的回答
```

## Feature: 图览画布探索工作台（v4，G6 重写）

```gherkin
Background:
  Given 团队已开启知识图谱能力且存在托管图谱

@KG-S35 @manual
Scenario: 双击展开与收起邻居
  When 画布上双击某节点
  Then 拉取其一跳邻接入图（邻接接口携带节点度数）并标记该节点为已展开
  When 再次双击该节点
  Then 仅因本次展开引入且无其它连边的邻居被回收，其余节点保留

@KG-S36 @manual
Scenario: 图例多选过滤与搜索定位
  Given 画布已加载多个实体类型的节点
  When 在左下图例面板点击某类型
  Then 该类型节点及相连边即时隐藏（画布内过滤，不重新请求）
  When 再次点击恢复显示或点「显示全部」
  Then 全部类型恢复
  When 搜索框输入名称并回车
  Then 画布内命中节点高亮并居中定位；未命中时提示按关键字重新加载子图

@KG-S37 @manual
Scenario: 实体合并（画布多选 → 合并对话框）
  Given 管理员在画布 Ctrl+多选 2~20 个实体并点「合并」
  When 在合并对话框选择保留目标并确认
  Then 源实体被删除、其关系重挂到目标（同三元组已存在跳过、两端均为源的自环丢弃）、描述与属性合并进目标、目标向量重建且源向量删除
  When 起止类型约束不满足或影响关系数 ≥20 未带确认标记
  Then 返回参数错误（400）或要求二次确认（409）
  When 普通成员或接入图调用合并接口
  Then 返回权限错误（403）或冲突（409）

@KG-S38 @auto:e2e
Scenario: 画布子图深度（depth=2）
  When 画布查询携带 depth=2
  Then 以种子节点集再向外扩一跳并入结果（受 limit 总预算约束，超限置 truncated）
  When depth 超出 1~2
  Then 返回参数错误（400）

@KG-S39 @manual
Scenario: 召回结果在画布查看
  Given 召回测试已返回命中实体
  When 点击「在画布查看」
  Then 跳转图览画布并高亮命中节点（缺失节点经邻接接口补拉后聚焦首个命中）

@KG-S40 @auto:e2e
Scenario: 邻接查询携带节点度数
  When 请求节点一跳邻接
  Then 返回的邻居节点携带度数（邻接边总数），供画布节点大小与详情侧栏展示
```

## Feature: AI 文件导入支持 42 种文档与文本格式（v4.2）

> 提取分层（原生/自研 EPUB·ODT·RTF/纯文本兜底）与知识库同口径，设计见 [../wiki/sdd.md](../wiki/sdd.md) D47；导入异步任务状态机见本模块 [sdd.md](./sdd.md)。

```gherkin
@KG-S41 @auto:e2e
Scenario: AI 文件导入放开 42 种提取能力格式
  When 用户经知识图谱导入专用预上传端点上传代码文本文件（.py，≤20MB）
  Then 预上传通过且文件落公开 chat 目录（导入命令校验不变）
  When 上传不在白名单内的文件（如 .exe）
  Then 预上传拒绝（400）且错误信息携带支持格式清单
  When 经对话附件通用上传端点上传同一 .py 文件
  Then 仍被拒绝（400）——对话附件白名单不受图谱导入扩容影响
  When 对已直传的 .py 文件发起 AI 导入
  Then 导入任务完成后成功抽取实体（任务内按纯文本兜底提取，与知识库同提取层）
```
