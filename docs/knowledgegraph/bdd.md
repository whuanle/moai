# 知识图谱模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs)

## Feature: 托管图谱（managed）

```gherkin
@KG-S1 @auto:e2e
Scenario: 创建空白图谱
  When 管理员以空白模板创建知识图谱
  Then 返回图谱 id 且来源为托管
  And 该图谱的模型为空

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

## Feature: 图览画布与权限（v2）

```gherkin
@KG-S13 @auto:e2e
Scenario: 画布有界子图
  Given 托管图谱已有节点与边
  When 查询画布子图
  Then 返回节点与节点集内部的边
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
  When 编辑态点击起点节点再点击终点节点
  Then 弹出建立关系弹窗且起止节点回显正确

@KG-S23 @auto:vitest
Scenario: 进入图谱默认图览
  Given 团队知识图谱列表已有图谱
  When 点击图谱卡片进入详情（未带分区路由）
  Then 默认处于图览且画布子图接口被调用
  And 接入图详情同样默认图览（仅图览/模型/设置可选）
```

## Feature: AI 导入文件生成图谱（v2.9）

```gherkin
@KG-S26 @auto:e2e
Scenario: AI 导入文件生成图谱
  Given Owner/Admin 已为托管图定义模型（实体类型/关系类型）且团队有可用对话模型
  When 上传文档并选择模型发起导入
  Then 文件经 Maomi.ToMarkdown 提取内容（超上限截断）
  And 对话模型按图谱现有模型抽取实体与关系
  And 仅模型已定义的类型被写入，关系起止约束校验通过后入图
  And 返回导入统计且画布可见新节点与边
  When 对非公开目录的 objectKey 发起导入
  Then 返回参数错误
  When 对没有实体类型的图谱发起导入
  Then 返回冲突（409）并提示先定义模型
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
```

> 外部开放接口（`/api/external/knowledge-graph`）场景编号沿用证据脚本 `kg-external-e2e.mjs` 的 KX-\* 体系（KX-01~KX-08），不复用 KG-\*。授权模型：应用 token 即团队级授权（等价团队 Admin 作用于本团队托管图谱），设计见 [sdd.md §5.1](./sdd.md#51-外部开放接口apexternalknowledge-graph)。

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
```
