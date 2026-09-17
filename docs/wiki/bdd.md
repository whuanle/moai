# 知识库模块行为场景（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)、[local-dev/wiki-external-e2e.mjs](../../local-dev/wiki-external-e2e.mjs)

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

## Feature: 公开知识库（详细页只读）

```gherkin
@WK-S9 @auto:e2e
Scenario: 公开库非成员只读
  Given Admin 创建公开知识库
  When 非成员查询详情
  Then 返回 200 且 myRole=0 且 isPublic=true
  And 非成员创建/更新/删除仍返回 404

@WK-S10 @auto:e2e
Scenario: 私有库保持团队门禁
  Given Admin 创建私有知识库（isPublic=false）
  When 非成员查询详情
  Then 返回 404
```

## Feature: 前端列表聚合（卡片）

```gherkin
@WK-S11 @auto:ui
Scenario: 列表只展示我加入的团队
  Given 用户在团队 A/B
  When 访问 /wiki
  Then 卡片聚合 A/B 知识库
  And 公开但用户不在其团队的知识库不展示

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

> 外部开放接口（`/api/external/wiki`）场景编号沿用证据脚本 `wiki-external-e2e.mjs` 的 WX-\* 体系（WX-01~WX-06），不复用 WK-\*。授权模型：应用 token 即团队级授权，设计见 [sdd.md §4.1](./sdd.md#41-外部开放接口apexternalwiki)。

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
