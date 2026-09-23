# 知识库模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)、[local-dev/wiki-external-e2e.mjs](../../local-dev/wiki-external-e2e.mjs)、[local-dev/wiki-source-e2e.mjs](../../local-dev/wiki-source-e2e.mjs)

## Feature: 访问控制

```gherkin
@WK-S1 @auto:e2e
Scenario: 未登录不能访问
  When 未携带令牌查询知识库列表
  Then 返回未认证
```

## Feature: 创建知识库

```gherkin
@WK-S2 @auto:e2e
Scenario: 参数校验
  When 以空名称/超长名称/非法 teamId 创建
  Then 返回参数错误

@WK-S3 @auto:e2e
Scenario: 角色权限
  When Member 创建知识库
  Then 返回禁止
  When 非成员创建知识库
  Then 返回不存在

@WK-S4 @auto:e2e
Scenario: Admin 创建并列出
  When Admin 创建知识库
  Then 返回知识库 id
  And Member 查询列表可见该知识库且响应含 myRole
  And 非成员查询列表返回不存在
```

## Feature: 名称唯一

```gherkin
@WK-S5 @auto:e2e
Scenario: 团队作用域唯一
  When 同团队创建同名知识库
  Then 返回冲突
  When 不同团队创建同名知识库
  Then 返回成功
```

## Feature: 详情与更新

```gherkin
@WK-S6 @auto:e2e
Scenario: 详情可见性
  When 成员查询详情
  Then 返回名称与 myRole
  When 非成员或已删除 id 查询
  Then 返回不存在

@WK-S7 @auto:e2e
Scenario: 更新
  When Member 更新
  Then 返回禁止
  When Admin 更新名称与简介
  Then 返回成功且详情回显
  When 更新为同团队已有名称
  Then 返回冲突
```

## Feature: 删除

```gherkin
@WK-S8 @auto:e2e
Scenario: 软删除
  When Member 删除
  Then 返回禁止
  When Admin 删除
  Then 返回成功
  And 详情返回不存在、列表不含、同团队同名可重建
```

## Feature: 知识库门禁（公开概念已移除）

```gherkin
@WK-S10 @auto:e2e
Scenario: 非成员一律不可见
  Given Admin 创建知识库（2026-09-21 起无公开/私有之分）
  When 非成员查询详情
  Then 返回不存在（404，不泄露存在性）
  And 非成员更新/删除同样返回 404
```

## Feature: 列表统计

```gherkin
@WK-S40 @auto:e2e
Scenario: 列表项携带统计信息
  Given 团队下已有知识库
  When 成员查询团队知识库列表
  Then 每个卡片项包含文件数量、切片数量与最近文档更新时间
  And 无文档的知识库统计为零且最近更新时间为空
```

## Feature: 前端列表聚合（卡片）

```gherkin
@WK-S11 @auto:ui
Scenario: 列表只展示我加入的团队
  Given 用户在团队 A/B
  When 访问 /wiki
  Then 卡片聚合 A/B 知识库

@WK-S12 @auto:ui
Scenario: 管理入口按角色
  Given 用户在团队 A 是 Admin
  When 访问 /wiki
  Then 该团队卡片显示编辑/删除
  And 无可管理团队时不显示「新建」
```

## Feature: 向量化配置

```gherkin
@WK-S13 @auto:vitest
Scenario: 管理员配置向量化参数
  Given 管理员打开未锁定知识库的设置页
  When 选择团队可用 embedding 模型并设置 1-2000 之间的整数维度
  Then 配置保存成功并回显服务端最新配置

@WK-S14 @auto:vitest
Scenario: 已向量化知识库锁定配置
  Given 知识库已有完成向量化的文件
  When 管理员打开设置页
  Then embedding 模型与维度不可修改
  And 元数据模型/切片长度/切片重叠不显示在 wiki 设置页

@WK-S15 @auto:e2e
Scenario: 普通切割必须传切片参数
  Given 文档已提取内容
  When 团队成员提交普通切割但未传切片长度，或按字符重叠时切片重叠 >= 切片长度
  Then 返回 400

@WK-S16 @auto:e2e
Scenario: 普通切割生成切片
  Given 知识库已绑定 embedding 模型与维度，文档已提取内容
  When 团队成员提交普通切割并选择切割方式、长度单位、重叠单位、切片长度与切片重叠
  Then 任务接受并写入 wiki_document_chunk_content，SliceOrder 从 1 自增

@WK-S17 @auto:vitest
Scenario: 向量维度上限 2000
  Given 管理员在未锁定知识库的设置页
  When 输入维度 2001 / 4097 / 0 / -1 / 1536.5
  Then 表单校验不通过，不调用保存接口

@WK-S23 @auto:vitest
Scenario: 重排序模型可选绑定
  Given 管理员在知识库设置页
  When 选择团队可用的 rerank 模型并保存
  Then 保存成功并回显该模型
  And 未选择模型直接保存时提交 null（解绑，不使用重排序）

@WK-S24 @auto:vitest
Scenario: 锁定后仍可修改重排序模型
  Given 知识库已锁定（isLock=true，向量模型与维度禁用）
  When 管理员更换或清空重排序模型并保存
  Then 保存成功，rerank_model_id 更新
  And 向量模型与维度保持不可编辑
```

## Feature: 文档操作页（提取 → 切割 → 向量化）

```gherkin
@WK-S18 @auto:vitest
Scenario: 文档操作页切割与向量化表单
  Given 团队成员进入文档操作页
  And 文档没有历史切割配置时，普通切割表单按向量维度填入推荐配置
  And 句子优先/段落优先切割会自动使用对应的重叠单位
  When 普通切割表单选择 Maomi.ToMarkdown 切割方式、长度单位、重叠单位并填写 chunkSize / chunkOverlap
  Then 提交时完整切割配置送至普通切割接口
  And AI 切割表单选择对话模型后提交，aiModelId 送至 AI 切割接口
  And 向量化表单仅含 isEmbedSourceText / isEmbedMetadata 两个开关，不再展示元数据生成模型选择器，也不含切片参数
  And 两项向量化开关均未勾选时阻止提交

@WK-S25 @auto:vitest
Scenario: 切片预览生成元数据
  Given 团队成员进入已有切片的文档操作页
  When 选择元数据生成模型并对单个切片点击生成元数据
  Then 单个切片元数据生成接口被调用
  When 对全部切片点击生成元数据
  Then 批量元数据生成接口被调用
```

## Feature: 文件提取与内容入库（Maomi.ToMarkdown）

```gherkin
@WK-S19 @auto:e2e
Scenario: 上传完成后 markdown 自动入库
  Given 团队成员上传 .md 文档并完成上传登记
  When CompleteWikiDocument 事务提交
  Then 文档记录与 wiki_document_content 同时落库且 Content 非空
  And 查询文档操作状态 isContentExtracted = true
  And 若自动提取失败，文档仍创建成功，操作页可手动「提取内容」重试

@WK-S20 @auto:vitest
Scenario: 不支持的文件类型自动提取被拒
  Given 文档后缀不在 Maomi.ToMarkdown 支持范围
  When 上传完成触发自动提取
  Then 自动提取抛错被捕获，文档创建不受影响（isContentExtracted = false）
  And 操作页展示「内容未提取」，可手动重试

@WK-S21 @auto:e2e
Scenario: 向量化前置校验
  Given 知识库已绑定 embedding 模型与维度
  When 团队成员在未提取内容或未切割时触发向量化
  Then 返回 409

@WK-S22 @auto:e2e
Scenario: 向量化复用已有内容与切片
  Given 知识库已绑定 embedding 模型与维度，文档已提取内容并切割
  When 团队成员勾选原文切片/已有元数据并触发向量化
  Then 任务接受并写入消息队列，wiki 实体不发生变更
```

## Feature: 系统设置：知识库上传大小上限（v2）

```gherkin
@WK-S26 @auto:e2e
Scenario: 超管设置知识库最大文件大小并全局生效
  When 超管保存设置项 WIKI_MAX_FILE_SIZE_MB（MB，默认 50，0 表示不限制）
  Then 团队成员访问 upload-limit 查询接口返回该上限（任意登录用户可读，供上传前预检）
  When 团队成员预上传超过上限的文件（网页端与外部开放接口同一入口）
  Then 返回 400（文件大小超过知识库上限）
  When 预上传恰好等于上限或恢复默认 50（或设为 0 不限制）后的文件
  Then 预上传通过（平台硬上限 1GB 仍然生效）
```

## Feature: 默认工作流与批量处理（多选文件一键执行）

```gherkin
@WK-S27 @auto:e2e
Scenario: 默认工作流配置权限、保存与校验
  When Member 保存知识库默认工作流配置
  Then 返回禁止
  When Admin 保存切割（普通/AI 模式）/生成元数据（策略可多选）/向量化三步预设
  Then 保存成功且知识库详情回读 workflowConfig 一致
  And 保存时校验元数据模型存在且为团队可用对话模型：模型不存在返回不存在，向量化两项开关全关返回参数错误

@WK-S28 @auto:e2e
Scenario: 批量全流程（切割 + 生成元数据 + 向量化）
  Given 团队成员在文件列表多选两个已上传文档
  When 一次提交勾选切割、生成元数据（指定部分策略）、向量化三步的批量工作流
  Then 切割同步完成且逐文档返回成功与异步任务 id
  And 任务完成后两文档均已向量化且元数据数大于 0

@WK-S29 @auto:e2e
Scenario: 批量单步·只切割
  When 多选文档仅勾选切割提交批量工作流
  Then 同步完成且不创建异步任务
  And 文档有切片、无元数据、未向量化

@WK-S30 @auto:e2e
Scenario: 批量单步·只生成元数据
  Given 文档已有切片
  When 仅勾选生成元数据提交批量工作流
  Then 返回异步任务 id
  And 任务完成后元数据数大于 0 且文档仍未向量化

@WK-S31 @auto:e2e
Scenario: 批量单步·只向量化
  Given 文档已切割且元数据就绪
  When 仅勾选向量化提交批量工作流
  Then 返回异步任务 id 且任务完成后文档已向量化

@WK-S32 @auto:e2e
Scenario: 批量错误隔离
  When 批量工作流包含一个不存在的文档 id
  Then 该文档逐文档失败并携带原因
  And 其余文档正常执行不受影响

@WK-S33 @auto:e2e
Scenario: 批量参数校验
  When 空文档列表 / 三步全不勾 / 勾选元数据未选模型 / 勾选切割未传切片长度 / 超过 50 个文档
  Then 返回参数错误

@WK-S34 @auto:e2e
Scenario: 批量前置校验
  When 未切割文档仅勾选向量化
  Then 该文档逐文档失败并提示先切割
  When 未绑定向量模型的知识库勾选向量化
  Then 返回冲突
  When 非成员提交批量工作流
  Then 返回不存在

@WK-S35 @auto:e2e
Scenario: 批量 AI 切割
  When AI 切割未选择对话模型
  Then 返回参数错误
  When 仅选 AI 切割但未勾选切割步骤
  Then 返回参数错误
  When 勾选切割（AI 模式）并可同时组合元数据/向量化提交批量工作流
  Then 无需切片参数且返回异步任务 id，AI 切割在后台任务执行

@WK-S36 @auto:e2e
Scenario: 多选生成策略
  Given 文档已有切片
  When 提交元数据生成并勾选部分生成策略
  Then 提交成功并返回异步任务 id
  And 任务仅按所选策略生成对应元数据类型，未勾选策略不生成
```

> 外部开放接口（`/api/external/wiki`）场景编号沿用证据脚本 `wiki-external-e2e.mjs` 的 WX-\* 体系（WX-01~WX-06），不复用 WK-\*。授权模型：应用 token 即团队级授权，设计见 [sdd.md §4.1](./sdd.md#41-外部开放接口apexternalwiki)。

## Feature: 召回测试（文档范围过滤 + AI 优化问题 + AI 生成回答）

```gherkin
@WK-S37 @auto:e2e
Scenario: 参数校验与团队门禁
  When 团队成员以空查询/空白查询/非法文档 id 调用召回测试
  Then 返回参数错误
  When 返回条数 top 传 0 或 51，或相似度阈值 minScore 传 1.5
  Then 返回参数错误
  When 开启 AI 优化问题但未传对话模型 id
  Then 返回参数错误
  When 非团队成员调用召回测试
  Then 返回不存在（404，不泄露存在性）

@WK-S38 @auto:e2e
Scenario: 向量召回、文档范围过滤与相似度阈值
  Given 知识库已绑定向量模型且两文档已完成向量化
  When 团队成员执行全库召回
  Then 返回命中项且得分降序、携带文档名/切片 id/元数据类型
  When 以 documentIds 限定单一文档范围召回
  Then 命中项全部来自指定文档，限定不存在文档 id 时 0 命中
  When 相似度阈值设为 1
  Then 0 命中；阈值设为 0 时不丢命中

@WK-S39 @auto:e2e
Scenario: AI 优化问题与 AI 生成回答
  Given 知识库可召回且已选团队可用对话模型
  When 开启 AI 优化问题执行召回
  Then 返回优化后查询文本且按优化文本召回命中，响应携带原始查询
  When 开启 AI 生成回答执行召回
  Then 基于命中内容返回非空回答
  When 优化与回答同时开启
  Then 两者结果同时返回
```

## Feature: 外部开放接口（应用 token，WX-*）

```gherkin
@WX-01 @auto:e2e
Scenario: 团队级知识库可见性
  When 以应用接入 key 换取应用 token 并查询外部知识库列表
  Then 仅返回 token 所属团队的知识库
  When 访问他团队知识库详情或文档列表
  Then 返回不存在（404，不泄露存在性）

@WX-02 @auto:e2e
Scenario: 绑定向量化配置
  When 设置维度 0 或 2001
  Then 返回参数错误
  When 绑定团队可用 embedding 模型与合法维度
  Then 成功且详情回显；未授权给团队的模型返回禁止

@WX-03 @auto:e2e
Scenario: 文件三段式上传与删除
  When 预上传获取预签名 URL 并直传后完成登记
  Then 文档出现在列表且同 SHA 重复上传走秒传
  When 上传非法格式文件或完成他团队 fileId 的登记
  Then 分别返回参数错误与不存在
  When 删除文档后以同 SHA 重新上传
  Then 可重新入库

@WX-04 @auto:e2e
Scenario: 重命名与内容提取
  When 重命名文档
  Then 列表回显新名
  When 未提取内容时读取正文
  Then 返回不存在
  When 触发提取
  Then 提取成功后正文可读

@WX-05 @auto:e2e
Scenario: 切割与向量化全链路
  When 普通切割已提取文档
  Then 切片生成
  When 触发向量化
  Then 返回任务 id；进行中重复触发返回冲突
  When 轮询任务状态
  Then 完成后切片数大于 0、顺序连续且含原文片段（向量化复用既有 WorkerTask 管线）

@WX-06 @auto:e2e
Scenario: 外部接口仅接受应用 token
  When 无 token、伪造 token 或内部用户 JWT 调用外部接口
  Then 分别返回未认证/未认证/未认证或禁止
```

## Feature: 外部源（飞书文档 / 网页爬虫）

```gherkin
@WS-S1 @auto:e2e
Scenario: 未登录与非成员不可见
  Given 某团队的知识库
  When 未携带令牌访问其外部源
  Then 返回未认证
  When 非该团队成员查看/创建/修改/删除外部源或触发同步
  Then 一律返回不存在

@WS-S2 @auto:e2e
Scenario: 普通成员只读
  Given 团队普通成员
  When 查询外部源列表或已同步文档
  Then 返回成功且只读展示
  When 创建、修改、删除外部源或触发同步
  Then 返回禁止

@WS-S3 @auto:e2e
Scenario: 管理员创建时的参数校验
  When 提交空名称、超长名称、非法定时表达式
  Then 返回参数错误
  When 爬虫源缺少配置、起始地址不是 http(s)、单轮页数超上限
  Then 返回参数错误
  When 飞书文档源未选择任何绑定方式、同时给了两种绑定方式、或缺少节点 token
  Then 返回参数错误

@WS-S4 @auto:e2e
Scenario: 创建后立即拉取一次
  Given 管理员配置了一个网页爬虫外部源
  When 提交创建
  Then 创建成功并立即完成一轮拉取，文档数大于 0 且同步状态为成功
  When 再次查看该外部源
  Then 类型、起始地址与配置原样回显

@WS-S5 @auto:e2e
Scenario: 抓取范围受路径前缀约束
  Given 目标站点存在同域名但不在路径前缀下的页面
  When 执行同步
  Then 仅收录前缀内的页面
  And 文档标题取自页面标题，子目录页面同样被收录

@WS-S6 @auto:e2e
Scenario: 内容未变化不重复写入，但新页面仍会被发现
  Given 该外部源已完成一轮拉取
  When 目标站点新增一个子页面后再次同步
  Then 内容未变化的页面不重复处理
  And 新页面被创建入库

@WS-S7 @auto:e2e
Scenario: 内容变化才更新
  Given 某篇已同步的外部文档内容发生变化
  When 再次同步
  Then 仅该文档被更新，其余标记为无变化
  When 以强制全量方式再次同步
  Then 所有文档被重新处理

@WS-S8 @auto:e2e
Scenario: 已同步文档清单
  When 分页查看某外部源已同步的文档
  Then 分页大小生效且总数正确
  When 按标题关键字筛选
  Then 仅返回命中的文档

@WS-S9 @auto:e2e
Scenario: 名称在知识库内唯一
  When 在同一知识库下创建同名外部源
  Then 返回冲突
  When 在另一知识库下创建同名外部源
  Then 创建成功

@WS-S10 @auto:e2e
Scenario: 更新外部源
  When 修改名称、描述与定时表达式
  Then 回显新值，未提交的爬虫配置保持不变
  When 提交空的定时表达式
  Then 定时任务被关闭
  When 提交非法的定时表达式
  Then 返回参数错误

@WS-S11 @auto:e2e
Scenario: 停用后不再同步
  When 停用某个外部源
  Then 手动触发同步返回冲突
  When 重新启用
  Then 可以再次同步

@WS-S12 @auto:e2e
Scenario: 飞书文档源的凭证不入链表
  Given 管理员用尚未验证的飞书应用凭证创建飞书文档源
  When 创建提交
  Then 外部源创建成功，首次拉取的失败落到同步状态上而不阻断创建
  When 查看该外部源
  Then 类型与节点 token 回显，且响应中不含任何密钥
  When 删除该外部源
  Then 飞书渠道绑定被解除

@WS-S13 @auto:e2e
Scenario: 删除外部源不删除已入库文档
  Given 某外部源已同步若干文档
  When 删除该外部源
  Then 列表不再包含它，再次同步或查看文档返回不存在
  And 已同步进知识库的文档仍然保留

@WS-S14 @auto:e2e
Scenario: 抓取频率受请求间隔约束
  Given 管理员配置了一个网页爬虫外部源，请求间隔为若干秒
  When 提交创建并完成一轮抓取
  Then 抓取串行执行且相邻两次请求的间隔不小于配置值
  And 请求间隔、超时与页数上限在配置回显中保持

@WS-S15 @auto:e2e
Scenario: 非爬虫源提交不触发爬虫校验
  Given 管理员提交一个飞书文档源（未携带任何爬虫配置）
  When 创建该外部源
  Then 创建成功，不出现空引用错误
  When 更新该飞书源的名称与定时表达式（未携带爬虫配置）
  Then 更新成功且响应为成功
  When 更新该外部源的爬虫配置（携带完整爬虫配置）
  Then 更新成功且爬虫配置回显为新值

@WS-S16 @auto:e2e
Scenario: 无正文页面被跳过且不计入失败
  Given 目标站点存在一个正文为空的页面
  When 执行同步
  Then 该页面被标记为跳过，且不生成知识库文档
  And 该轮失败页数为零

@WS-S17 @auto:e2e
Scenario: 连续失败时提前熔断
  Given 目标站点在若干页面后开始持续返回错误
  When 执行同步
  Then 连续失败达到阈值后本轮抓取提前中止
  And 已成功抓取的页面保持入库结果
```

> 飞书事件订阅的成功路径依赖真实开放平台长连接，本期未纳入自动化（见 [sdd.md §6 D22](./sdd.md#6-关键决策)）。

```gherkin
@WK-S99 @manual
Scenario: 新建知识库上传头像
  Given 团队 Admin+ 打开新建知识库弹窗
  When 选择图片（≤5MB）并提交
  Then 创建成功后立即登记头像，卡片列表显示头像
  When 编辑弹窗中更换头像
  Then 直传后立即设置成功（avatarPath 更新）
```
