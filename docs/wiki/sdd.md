# 知识库模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

- 日期：2026-09-02（一期）/ 2026-09-07（重做：卡片聚合 + 公开开关 + 占位详情页）/ 2026-09-08（向量化设置）/ 2026-09-08（修：元数据/切片参数下沉到文档级）/ 2026-09-09（重排序模型设置）/ 2026-09-10（向量化只做向量化，元数据生成独立）
- 状态：知识库 CRUD、团队级 embedding 模型绑定、可选 rerank 模型绑定已实现；元数据生成模型在切片预览区独立选择，切片大小/重叠由切割步骤动态传入
- 领域：`src/wiki`（Shared/Core/Api），前端 `ui/src/pages/wiki`

## 1. 目标

知识库是团队下的第一类资源（对齐"按团队管理"路线）。重做后交付：
- 知识库 CRUD（团队作用域），列表以**卡片网格**聚合我加入的所有团队的知识库（仅我的团队，公开但非我所在团队的不显示）。
- 知识库 `isPublic` 开关：公开后**团队外可只读**（非成员可查看详情，`myRole=0`，不能操作）。
- `/wiki/:id` 知识库详情页为**占位模板**（内容/文档/检索能力下阶段落地）。
- **（2026-09-08 修正，2026-09-10 更新）** wiki 设置页只承载 embedding 模型与维度上限；元数据生成模型由切片预览区独立的元数据生成操作选择，切片大小、切片重叠由切割步骤动态传入，均不再随向量化触发传入。

## 2. 数据模型

- `wiki`：`id / team_id / name / description / is_public / avatar_path / counter / embedding_model_id / embedding_dimensions / rerank_model_id / is_lock` + 审计（bool 软删除，同 team 约定 D1）
  - `embedding_dimensions` 上限 2000（pgvector 建 hnsw 索引的硬上限）
  - `rerank_model_id` **可空**：未绑定表示检索不做重排序；与向量化配置解耦，锁定后仍可修改
  - **无** `metadata_model_id` / `chunk_size` / `chunk_overlap` 列，这些参数属于单文档触发口径，不再由 wiki 持有
- `wiki_document` 与切片/向量表不变；`wiki_document.slice_config` 保存该文档上次触发的切片 JSON（`splitMode/chunkSize/chunkOverlap/overlapUnit/sizeUnit/tokenEncodingOrModel`），用于展示历史
- 文档内容持久化：`wiki_document_content`（按 `document_id` 唯一一行），保存最近一次触发由 Maomi.ToMarkdown 抽取出的完整 markdown，供后续编辑/重抽/溯源使用
  - 抽取入口：`Maomi.ToMarkdown.TextExtractionService`（注册名 `AddTextExtraction()`，由 `WikiCoreModule` 装配），按文件 MIME 类型路由到 `MsWordExtractor / MsExcelExtractor / MsPowerPointExtractor / PdfToMarkdownConverter / ReverseMarkdown` 等实现
  - 切割入口：普通切割使用 Maomi.ToMarkdown TextSplit，支持 `SplitByMarkdown / SplitRecursive / SplitFixedSize / SplitBySentence / SplitByParagraph`；`chunkSize` 的含义由 `sizeUnit` 决定（字符或 `Maomi.ToMarkdown.Token` token），`chunkOverlap` 的含义由 `overlapUnit` 决定（字符/句子/段落）
  - 抽取时机：完成上传后自动入库内容；切割时按用户选择的 `splitMode / chunkSize / chunkOverlap / overlapUnit / sizeUnit / tokenEncodingOrModel` 写入 `wiki_document_chunk_content`；失败时不影响已入库文档内容
  - 抽取与切分互不阻塞：抽取失败抛 `NotSupportedException`，业务层映射为 400 提示「文件类型不受支持」
- partial 唯一 `(team_id, name) WHERE is_deleted = false`：同团队未删除范围内名称唯一，删除后同名可重建；不同团队互不影响
- 索引 `idx_wiki_team_id`；DDL：`asserts/wiki.sql`、`asserts/wiki_vectorization.sql`（vectorization 文件已收缩为「去列 + 上限 CHECK 约束」）

## 3. 权限（复用 Team 领域角色，Handler 层判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 创建/更新/删除 | ✅ | 403 | 404 |
| 列表 | ✅ | ✅ | 404 |
| 详情（私有库） | ✅ | ✅ | 404 |
| 详情（公开库） | ✅ | ✅ | ✅ 只读（`myRole=0`） |
| 更新 wiki embedding 模型 / 维度 | ✅ | 403 | 404 |
| 更新 wiki rerank 模型（含解绑） | ✅ | 403 | 404 |
| 触发文档向量化 | ✅ | ✅ | 404 |

- 角色判定注入 `MoAI.Team.Shared` 的 `ITeamService`（跨域接口复用，实现由 Team 模块注册）
- 列表/详情响应携带 `myRole`；公开库对非成员返回 `myRole=0`，前端据此隐藏管理操作
- 管理动作（建/改/删）仍要求 Admin+，公开库不放开操作
- 触发文档向量化只要求「团队成员」即可（不再硬性要求 Admin），元数据模型授权校验与 embedding 模型一致

## 4. API

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| POST | `/api/wiki` | 创建 `{teamId, name, description?, isPublic?}` | `SimpleLong` |
| GET | `/api/wiki/list?teamId=` | 团队知识库列表（含 myRole、isPublic、avatarPath） | `QueryWikisCommandResponse` |
| GET | `/api/wiki/{id}` | 详情（含 myRole、isPublic、avatarPath、embeddingModelName、embeddingDimensions、rerankModelId/Name、isLock） | `QueryWikiCommandResponse` |
| PUT | `/api/wiki/{id}` | 更新 `{name, description?, isPublic?}` | Empty |
| DELETE | `/api/wiki/{id}` | 软删除 | Empty |
| POST | `/api/wiki/{id}/avatar` | 设置头像 `{objectKey}`，仅 Admin+ | Empty |
| GET | `/api/wiki/model-options?teamId=` | 可用模型候选（公开 + 已授权 conversation / embedding / rerank），仅团队成员 | `QueryWikiModelOptionsCommandResponse` |
| PUT | `/api/wiki/{id}/embedding-config` | 绑定向量模型 + 维度（1-2000），仅 Admin+；已锁定 wiki 返回 409 | Empty |
| PUT | `/api/wiki/{id}/rerank-model` | 绑定/更换重排序模型（`rerankModelId` 可空），仅 Admin+；**已锁定 wiki 仍可修改** | Empty |
| POST | `/api/wiki/{id}/documents/preupload` | 预上传 | `PreUploadWikiDocumentCommandResponse` |
| POST | `/api/wiki/{id}/documents/complete` | 完成上传 | Empty |
| POST | `/api/wiki/{id}/documents/list` | 文档分页 | `QueryWikiDocumentsCommandResponse` |
| DELETE | `/api/wiki/{id}/documents` | 批量删除 | Empty |
| GET | `/api/wiki/{id}/documents/{documentId}/download` | 下载地址 | `SimpleString` |
| PUT | `/api/wiki/{id}/documents/{documentId}/rename` | 重命名 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/partition` | 普通切割，传 `splitMode/chunkSize/chunkOverlap/overlapUnit/sizeUnit/tokenEncodingOrModel?`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/ai-partition` | AI 切割，传 `aiModelId/promptTemplate?`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/chunks/{chunkId}/metadata/generate` | 为单个切片生成/重生成元数据，传 `metadataModelId`，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/chunks/metadata/generate` | 为文档切片批量生成/重生成元数据，传 `metadataModelId`，`chunkIds` 为空表示全部切片，仅团队成员 | Empty |
| POST | `/api/wiki/{id}/documents/{documentId}/embedding` | 触发向量化，传 `isEmbedSourceText/isEmbedMetadata`（至少一项为真），仅团队成员 | `{ taskId }` |
| GET | `/api/wiki/{id}/documents/{documentId}/embedding` | 查询文档向量化详情（含 wiki embedding 模型/维度、上次切割配置、切片列表） | `QueryWikiDocumentEmbeddingCommandResponse` |

> 原文档层接口（`/documents/{id}` GET）已随实体重做移除；内容/文档能力下阶段按文件接入模型重建。

## 5. 前端「知识库」页与团队模式上下文

- `/wiki` 页：**纯只读卡片网格**，聚合我加入的所有团队的知识库（前端 `getMyTeams()` → 逐团队 `getWikis(teamId)` 合并，附 `teamName`、`myRole`、`isPublic`）。仅我的团队；公开但非我所在团队的不展示。**不提供新建/编辑/删除入口**（管理功能在团队详情）。
- `/wiki` 页卡片点击 → `/team/{teamId}/wiki/{wikiId}`：知识库详情页，左侧菜单（文件列表 / 召回测试 / 设置）；设置页可编辑名称/简介/公开、上传头像（仅 Admin+），以及绑定 embedding 模型与向量维度（1-2000）。**元数据模型 / 切片参数不在 wiki 设置页**。文件列表与召回测试为占位。**详情路由为团队嵌套**（`/team/:teamId/wiki/:wikiId/:section?`）。
- `/team/{teamId}/wiki/{wikiId}/document/{documentId}/embedding` 文档级页：展示文档内容、普通/AI 切割与向量化；普通切割可选择 Maomi.ToMarkdown 切割方式、长度单位（字符/Token）、重叠单位、切片长度与切片重叠；无历史切割配置时，前端按 wiki 向量维度默认推荐 Markdown 感知 + Token 计量 + 句子重叠 1 的最佳配置，并提供「应用推荐值」恢复推荐；选择句子优先/段落优先切割时，前端自动切换并锁定对应重叠单位；选择 Token 时前端下拉提供后端明确支持的编码；AI 切割提供可直接使用的默认提示词模板，不再额外展示说明提示；切片预览可选择对话模型后对单个切片或全部切片生成/重生成元数据；向量化仅选择是否对原文切片/已生成元数据向量化，两项均未勾选时阻止提交，元数据生成模型选择器不在向量化表单中。
- **团队详情「知识库」tab（`TeamWikis`）**：真正的知识库管理（新建/修改/删除），仅 Owner/Admin 可操作，Member 只读。数据源 `getWikis(teamId)` 的 `myRole` 判定。
- `store/app.ts` 现有 `currentTeamId`（persist）与 `myTeams`（内存）保持；`Teams.tsx` 增删后同步 `myTeams`。

## 6. 关键决策

- **D1** 权限复用团队角色：wiki 不引入新角色体系，Admin+ 可管理
- **D2** 跨域接口复用：Core 引用 `MoAI.Team.Shared`（接口），不引用其实现项目
- **D3** 名称唯一性为团队作用域（团队间允许同名）
- **D4** 公开库非成员只读：详情返回 `myRole=0`，不放开任何写操作
- **D5** `/wiki` 列表只在「我的团队」作用域聚合；公开库不进入非成员列表（仅可通过详情路径只读访问）
- **D6** 权限位置拆分：一级菜单 `/wiki` 只读；知识库管理（建/改/删）收敛到「团队详情 → 知识库」tab
- **D7** 详情路由团队嵌套：`/team/{teamId}/wiki/{wikiId}/:section`，知识库隶属于团队作用域
- **D8** embedding 模型与维度由 wiki 持有（pgvector 表结构强绑）；元数据生成模型由切片预览区的独立生成操作选择，切片参数在切割步骤（普通/AI）动态传入
- **D9** 首次向量化后 wiki embedding 模型 + 维度被锁定：避免同一 wiki 产生维度不兼容的向量数据；元数据生成模型与切片参数不受锁定影响（分属独立步骤，已在 @WK-S14 明确）
- **D10** `embedding_dimensions` 上限 2000：pgvector 在 2000 以内才可建 hnsw 索引，超过则只支持精确检索；用户需自行权衡
- **D11** 文件抽取统一走 `Maomi.ToMarkdown`（自维护包，1.0.0-alpha.2），不做第二套抽取实现：保证 PDF/Docx/Xlsx/Pptx/HTML/纯文本一致输出 markdown；扩展新格式只需在 `Maomi.ToMarkdown` 内追加 extractor，`WikiCoreModule` 已通过 `AddTextExtraction()` 一次性注册
- **D12** 抽取得到的 markdown 完整存入 `wiki_document_content`：不仅用于切片的"原料"，也是后续文档级编辑/再抽取/审计的依据；同一 `document_id` 始终只有一行，提取/重提取时 upsert
- **D14** 重排序模型（`rerank_model_id`）为 wiki 级**可选**配置：绑定后检索结果先经该模型重排；未绑定则不做重排序。模型类型新增 `rerank`（`AIModelKind.Rerank`，字符串 `rerank`；`AIModelMetaMapper` 按 id/family 含 rerank/reranker 推导，须先于 embedding 判定）
- **D15** 重排序模型与向量化配置解耦：**不受 IsLock 限制**，知识库锁定后仍可绑定/更换/解绑——它不参与向量维度，不会破坏已有向量数据；权限与授权校验（模型启用、类型匹配、公开/团队授权）与 embedding 一致
- **D16** rerank 模型目前只做配置绑定，检索链路（召回测试 / 检索接口）接入为下阶段工作
- **D13** 内容提取自动随上传完成：`CompleteWikiDocumentCommandHandler` 在文档登记成功并提交事务后，立即同步调用 `WikiDocumentProcessingService.ExtractAsync` 用 Maomi.ToMarkdown 抽取 markdown 入库，用户无需在操作页手动提取；提取失败不回滚文档创建（记日志），操作页保留「提取内容/重新提取」作兜底重试。切割与向量化仍是显式步骤：切割（`PartitionAsync` 普通 / `AiPartitionAsync` AI）生成切片后，向量化（`WikiEmbeddingService.ProcessAsync`）复用已有内容+切片，未提取/未切割时 409
- **D17** 普通切割直接暴露 Maomi.ToMarkdown TextSplit 能力：默认保持 Markdown 感知 + 字符计量 + 字符重叠以兼容旧行为；用户可切换递归、固定长度、句子、段落模式，并通过 `Maomi.ToMarkdown.Token` 按 token 计量 chunkSize。AI 切割仍保留独立页签，不与普通切割参数混用
- **D18** 切片元数据生成可独立于向量化执行：切片预览区支持单片与全部切片生成/重生成元数据，结果直接保存到 `wiki_document_chunk_metadata`；向量化勾选元数据时直接复用这些已生成元数据，不再生成元数据
- **D19** 普通切割默认推荐值由 wiki 向量维度决定：`<=384 → 320`、`<=768 → 512`、`<=1024 → 700`、`<=1536 → 900`、`>1536 → 1100`（单位均为 Token）；默认重叠为 1 个句子，用户仍可自由覆盖
- **D20** 向量化只做向量化：文档操作页的向量化表单仅保留「对原文切片向量化 / 对元数据向量化」两个开关（至少选一项），删除元数据生成模型选择器；元数据生成是切片预览区的独立步骤与接口，向量化触发请求体只含 `isEmbedSourceText` 与 `isEmbedMetadata`

## 7. 已知问题 / 下阶段

- 内容/文档层：`wiki_document` 等实体已重做为文件接入模型（ObjectKey/FileName/向量化），相关 API 与前端已落地
- 旧文本文档接口（`/documents`、`/document/{id}`）及前端文档页/编辑器已移除
- `wiki-e2e.mjs` 覆盖知识库 CRUD + 文档 CRUD；`wiki-embedding-e2e.mjs` 覆盖 提取→切割→向量化（WK-S15/S16/S19/S21/S22）已加脚本，待后端可运行时联调
- 文档操作页（提取/切割/切片预览/向量化）已落地；召回测试与文档检索为下阶段
- **D11/D12/D13** 抽取 → `wiki_document_content` → `wiki_document_chunk_content` → `wiki_embedding_{wikiId}` 的整条流水线已拆分为三步独立操作，抽取/切割在 `WikiDocumentProcessingService`，向量化在 `WikiEmbeddingService`
