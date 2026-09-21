import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

export interface WikiItem {
  /** 后端 long 序列化为字符串 */
  wikiId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  /** 文档数量 */
  documentCount?: number | null
  /** 已切片数量（切片内容表行数） */
  chunkCount?: number | null
  /** 最近一次文档更新时间，无文档时为 null */
  lastDocumentUpdateTime?: string | null
  /** 知识库头像的 ObjectKey（空串=未设置），前端用 resolveStorageUrl 转可访问地址 */
  avatarPath?: string | null
  createTime?: string | null
}

export interface WikisResult {
  teamId?: string | number | null
  /** 0=Owner 1=Admin 2=Member */
  myRole?: number | null
  items?: WikiItem[] | null
}

/** /wiki 卡片列表项：把团队信息合并进知识库项（前端聚合用，非后端模型） */
export interface WikiCardItem extends WikiItem {
  /** 所属团队名称 */
  teamName?: string | null
  /** 我在所属团队中的角色：0=Owner 1=Admin 2=Member */
  myRole?: number | null
}

export async function getWikis(teamId: number): Promise<WikisResult> {
  const client = getApiClient()
  const res = await client.api.wiki.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/** 查询知识库上传文件大小上限（MB），0 表示不限制 */
export async function getWikiUploadLimit(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.uploadLimit.get()
  return Number(res?.maxFileSizeMb ?? 0)
}

export function getWikiDetail(wikiId: number) {
  const client = getApiClient()
  return client.api.wiki.byId(String(wikiId)).get()
}

export async function createWiki(payload: { teamId: number; name: string; description?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.post({ teamId: String(payload.teamId), name: payload.name, description: payload.description })
  return Number(res?.value ?? 0)
}

export async function updateWiki(wikiId: number, payload: { name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).put({ name: payload.name, description: payload.description })
}

export async function deleteWiki(wikiId: number): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).delete()
}

export async function setWikiAvatar(wikiId: number, objectKey: string): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).avatar.post({ objectKey })
}

/** 上传图片并设为知识库头像（走存储直传管线），返回公开访问地址 */
export async function uploadWikiAvatar(wikiId: number, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  await setWikiAvatar(wikiId, objectKey)
  return url
}

// ==================== 知识库文档 ====================

export interface WikiDocumentItem {
  documentId?: number | null
  wikiId?: number | null
  fileId?: number | null
  fileName?: string | null
  fileSize?: number | null
  contentType?: string | null
  isEmbedding?: boolean | null
  chunkCount?: number | null
  metadataCount?: number | null
  isContentExtracted?: boolean | null
  contentLength?: number | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
}

export interface WikiDocumentsResult {
  items?: WikiDocumentItem[] | null
  total?: number | null
  pageNo?: number | null
  pageSize?: number | null
}

export interface QueryWikiDocumentsParams {
  pageNo: number
  pageSize: number
  query?: string
  isEmbedding?: boolean | null
}

interface PreUploadResult {
  fileId?: number | null
  isExist?: boolean | null
  uploadUrl?: string | null
  expiration?: string | null
}

/** 计算文件 SHA-256（小写十六进制） */
async function sha256(buffer: ArrayBuffer): Promise<string> {
  const hash = await crypto.subtle.digest('SHA-256', buffer)
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

/** 查询知识库文档列表 */
export async function getWikiDocuments(wikiId: number, params: QueryWikiDocumentsParams): Promise<WikiDocumentsResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.list.post({
    wikiId: String(wikiId),
    pageNo: params.pageNo,
    pageSize: params.pageSize,
    query: params.query || undefined,
    isEmbedding: params.isEmbedding ?? null,
  })
  return {
    items: res?.items ?? [],
    total: res?.total ?? 0,
    pageNo: res?.pageNo ?? params.pageNo,
    pageSize: res?.pageSize ?? params.pageSize,
  }
}

/** 预上传文档，返回预签名上传地址 */
export async function preUploadWikiDocument(wikiId: number, file: File): Promise<PreUploadResult | undefined> {
  const buffer = await file.arrayBuffer()
  const shA256 = await sha256(buffer)
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.preupload.post({
    wikiId: String(wikiId),
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    fileSize: file.size,
    shA256,
  })
  if (!res) return undefined
  return {
    fileId: res.fileId != null ? Number(res.fileId) : null,
    isExist: res.isExist,
    uploadUrl: res.uploadUrl,
    expiration: res.expiration,
  }
}

/** 完成文档上传（登记为知识库文档） */
export async function completeWikiDocument(wikiId: number, payload: { fileId: number; fileName: string; isSuccess: boolean }): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.complete.post({
    wikiId: String(wikiId),
    fileId: String(payload.fileId),
    fileName: payload.fileName,
    isSuccess: payload.isSuccess,
  })
}

/** 删除知识库文档 */
export async function deleteWikiDocuments(wikiId: number, documentIds: number[]): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.delete({
    wikiId: String(wikiId),
    documentIds: documentIds.map((id) => String(id)),
  })
}

/** 获取文档下载地址 */
export async function downloadWikiDocument(wikiId: number, documentId: number): Promise<string> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).download.get()
  return res?.value ?? ''
}

/** 重命名知识库文档 */
export async function renameWikiDocument(wikiId: number, documentId: number, fileName: string): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).rename.put({
    wikiId: String(wikiId),
    documentId: String(documentId),
    fileName,
  })
}

// ==================== 文档向量化 ====================

export interface WikiDocumentChunkMetadataItem {
  /** 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段 */
  metadataType?: number | null
  metadataContent?: string | null
}

export type WikiMetadataGenerationStrategy = 'outlineGeneration' | 'questionGeneration' | 'keywordSummaryFusion' | 'semanticAggregation'

export interface WikiDocumentEmbeddingChunkItem {
  chunkId?: string | null
  sliceOrder?: number | null
  sliceContent?: string | null
  metadataCount?: number | null
  metadatas?: WikiDocumentChunkMetadataItem[] | null
}

export interface WikiDocumentEmbeddingResult {
  wikiId?: number | null
  wikiName?: string | null
  embeddingModelId?: string | null
  embeddingModelName?: string | null
  embeddingDimensions?: number | null
  isLock?: boolean | null
  splitMode?: string | null
  chunkSize?: number | null
  chunkOverlap?: number | null
  overlapUnit?: string | null
  sizeUnit?: string | null
  tokenEncodingOrModel?: string | null
  documentId?: number | null
  fileName?: string | null
  isEmbedding?: boolean | null
  embeddingCount?: number | null
  isContentExtracted?: boolean | null
  contentLength?: number | null
  /** detail 返回的内容预览（截断后的 markdown，最多 ContentPreviewLimit 字） */
  content?: string | null
  /** 内容预览的实际字符长度（= min(contentLength, 预览上限)） */
  contentPreviewLength?: number | null
  items?: WikiDocumentEmbeddingChunkItem[] | null
}

export interface UpdateWikiEmbeddingConfigPayload {
  embeddingModelId: string
  embeddingDimensions: number
}

/** 查询知识库文档向量化详情 */
export async function getWikiDocumentEmbedding(wikiId: number, documentId: number): Promise<WikiDocumentEmbeddingResult | undefined> {
  const client = getApiClient()
  return client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).embedding.get()
}

/** 触发知识库文档向量化（复用已提取内容 + 已切割切片及已生成元数据，元数据生成在独立模块完成） */
export async function triggerDocumentEmbedding(
  wikiId: number,
  documentId: number,
  payload: {
    isEmbedSourceText?: boolean
    isEmbedMetadata?: boolean
  },
): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).embedding.post({
    wikiId: String(wikiId),
    documentId: String(documentId),
    isEmbedSourceText: payload.isEmbedSourceText ?? true,
    isEmbedMetadata: payload.isEmbedMetadata ?? true,
  })
}

/** 提取知识库文档内容（Maomi.ToMarkdown 抽取 markdown 并入库），切割前必须先执行 */
export async function extractWikiDocumentContent(wikiId: number, documentId: number): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).extract.post()
}

/** 读取知识库文档已提取的完整内容（markdown），用于内容区「全部加载」 */
export async function getWikiDocumentContent(wikiId: number, documentId: number): Promise<string> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).content.get()
  return res?.value ?? ''
}

export type WikiDocumentPartitionSplitMode = 'recursive' | 'fixedSize' | 'sentence' | 'paragraph' | 'markdown'
export type WikiDocumentPartitionOverlapUnit = 'character' | 'sentence' | 'paragraph'
export type WikiDocumentPartitionSizeUnit = 'character' | 'token'

/** 普通切割知识库文档（Maomi.ToMarkdown 多模式切分） */
export async function partitionWikiDocument(
  wikiId: number,
  documentId: number,
  payload: {
    splitMode: WikiDocumentPartitionSplitMode
    chunkSize: number
    chunkOverlap: number
    overlapUnit: WikiDocumentPartitionOverlapUnit
    sizeUnit: WikiDocumentPartitionSizeUnit
    tokenEncodingOrModel?: string | null
  },
): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).partition.post({
    wikiId: String(wikiId),
    documentId: String(documentId),
    splitMode: payload.splitMode,
    chunkSize: payload.chunkSize,
    chunkOverlap: payload.chunkOverlap,
    overlapUnit: payload.overlapUnit,
    sizeUnit: payload.sizeUnit,
    tokenEncodingOrModel: payload.sizeUnit === 'token' ? (payload.tokenEncodingOrModel || null) : null,
  })
}

/** AI 智能切割知识库文档（对话模型按语义输出 JSON 字符串数组） */
export async function aiPartitionWikiDocument(
  wikiId: number,
  documentId: number,
  payload: { aiModelId: string; promptTemplate?: string | null },
): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).aiPartition.post({
    wikiId: String(wikiId),
    documentId: String(documentId),
    aiModelId: payload.aiModelId,
    promptTemplate: payload.promptTemplate ?? null,
  })
}

/** 为单个切片生成元数据 */
export async function generateWikiDocumentChunkMetadata(wikiId: number, documentId: number, chunkId: string, metadataModelId: string, appendExisting = false, strategyType?: WikiMetadataGenerationStrategy): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).chunks.byChunkId(chunkId).metadata.generate.post({
    wikiId: String(wikiId),
    documentId: String(documentId),
    metadataModelId,
    chunkIds: [chunkId],
    appendExisting,
    strategyType,
  })
  return res?.value ?? 0
}

/** 为文档全部切片批量生成元数据 */
export async function generateWikiDocumentChunksMetadata(wikiId: number, documentId: number, metadataModelId: string, chunkIds?: string[], strategyType?: WikiMetadataGenerationStrategy): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.byDocumentId(String(documentId)).chunks.metadata.generate.post({
    wikiId: String(wikiId),
    documentId: String(documentId),
    metadataModelId,
    chunkIds: chunkIds ?? [],
    strategyType,
  })
  return res?.value ?? 0
}

/** 更新知识库向量化配置 */
export async function updateWikiEmbeddingConfig(wikiId: number, payload: UpdateWikiEmbeddingConfigPayload): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).embeddingConfig.put({
    wikiId: String(wikiId),
    embeddingModelId: payload.embeddingModelId,
    embeddingDimensions: payload.embeddingDimensions,
  })
}

// ==================== 向量化配置（设置页） ====================

export interface WikiModelOptionItem {
  id?: string | null
  name?: string | null
  modelKind?: string | null
}

export interface WikiModelOptionsResult {
  embeddingModels?: WikiModelOptionItem[] | null
  conversationModels?: WikiModelOptionItem[] | null
  rerankModels?: WikiModelOptionItem[] | null
}

/** 更新知识库重排序模型配置（可选；传 null 表示解绑）。锁定状态下同样允许修改 */
export async function updateWikiRerankModel(wikiId: number, rerankModelId: string | null): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).rerankModel.put({
    wikiId: String(wikiId),
    rerankModelId,
  })
}

/** 查询团队可用的向量化/对话/重排序模型选项（公开模型 + 已授权模型） */
export async function getWikiModelOptions(teamId: number): Promise<WikiModelOptionsResult | undefined> {
  const client = getApiClient()
  return client.api.wiki.modelOptions.get({ queryParameters: { teamId } })
}

// ==================== 召回测试 ====================

export interface WikiRecallTestHit {
  documentId?: number | null
  documentName?: string | null
  chunkId?: string | null
  /** 0=原文切片 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段 */
  metadataType?: number | null
  content?: string | null
  /** 相似度得分（越大越相似） */
  score?: number | null
}

export interface WikiRecallTestResult {
  query?: string | null
  /** AI 优化后的查询文本；未开启优化时为空 */
  optimizedQuery?: string | null
  /** 基于召回内容生成的 AI 回答；未开启或无命中内容时为空 */
  answer?: string | null
  items?: WikiRecallTestHit[] | null
}

export interface WikiRecallTestPayload {
  query: string
  /** 文档范围过滤；空数组表示全部文档 */
  documentIds?: number[]
  /** 返回条数 1-50，默认 5 */
  top?: number
  /** 相似度阈值 0-1；不传表示不过滤 */
  minScore?: number | null
  /** AI 对话模型 id；开启优化或回答时必填 */
  aiModelId?: string | null
  isOptimizeQuery?: boolean
  isAnswer?: boolean
}

/** 知识库召回测试：向量检索切片，支持文档范围过滤、阈值、AI 优化问题与 AI 生成回答 */
export async function recallWikiTest(wikiId: number, payload: WikiRecallTestPayload): Promise<WikiRecallTestResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).recallTest.post({
    wikiId: String(wikiId),
    query: payload.query,
    documentIds: (payload.documentIds ?? []).map((id) => String(id)),
    top: payload.top ?? 5,
    minScore: payload.minScore ?? null,
    aiModelId: payload.aiModelId || null,
    isOptimizeQuery: payload.isOptimizeQuery ?? false,
    isAnswer: payload.isAnswer ?? false,
  })
  return {
    query: res?.query,
    optimizedQuery: res?.optimizedQuery,
    answer: res?.answer,
    items: (res?.items ?? []).map((item) => ({
      documentId: item.documentId != null ? Number(item.documentId) : null,
      documentName: item.documentName,
      chunkId: item.chunkId,
      metadataType: item.metadataType,
      content: item.content,
      score: item.score,
    })),
  }
}

// ==================== 默认工作流配置 ====================

/**
 * 知识库默认工作流配置：切割 / 元数据生成 / 向量化三步预设。
 * 某步骤为 null 表示未配置该步骤；整体覆盖式保存。
 */
export interface WikiWorkflowConfig {
  partition?: {
    mode?: WikiWorkflowPartitionMode | null
    splitMode?: WikiDocumentPartitionSplitMode | null
    chunkSize?: number | null
    chunkOverlap?: number | null
    overlapUnit?: WikiDocumentPartitionOverlapUnit | null
    sizeUnit?: WikiDocumentPartitionSizeUnit | null
    tokenEncodingOrModel?: string | null
    aiModelId?: string | null
    promptTemplate?: string | null
  } | null
  metadata?: {
    metadataModelId?: string | null
    strategyTypes?: WikiMetadataGenerationStrategy[] | null
  } | null
  embedding?: {
    embedSourceText?: boolean | null
    embedMetadata?: boolean | null
  } | null
}

// ==================== 外部源（飞书文档 / 网页爬虫） ====================
/** 外部源类型：与后端 WikiSourceType 枚举的 JsonPropertyName 一致（Kiota 生成为字符串枚举） */
export const WIKI_SOURCE_TYPE_FEISHU = 'feishuDoc'
export const WIKI_SOURCE_TYPE_CRAWLER = 'crawler'

export type WikiSourceType = typeof WIKI_SOURCE_TYPE_FEISHU | typeof WIKI_SOURCE_TYPE_CRAWLER

/** 外部源最近一次同步状态：0=未同步 1=成功 2=失败 3=同步中 */
export const WIKI_SOURCE_SYNC_NONE = 0
export const WIKI_SOURCE_SYNC_SUCCESS = 1
export const WIKI_SOURCE_SYNC_FAILED = 2
export const WIKI_SOURCE_SYNC_SYNCING = 3

export type WikiSourceSyncStatus =
  | typeof WIKI_SOURCE_SYNC_NONE
  | typeof WIKI_SOURCE_SYNC_SUCCESS
  | typeof WIKI_SOURCE_SYNC_FAILED
  | typeof WIKI_SOURCE_SYNC_SYNCING

/** 外部源文档同步状态：0=待同步 1=已同步 2=同步失败 */
export const WIKI_SOURCE_DOC_PENDING = 0
export const WIKI_SOURCE_DOC_SYNCED = 1
export const WIKI_SOURCE_DOC_FAILED = 2

export type WikiSourceDocumentStatus =
  | typeof WIKI_SOURCE_DOC_PENDING
  | typeof WIKI_SOURCE_DOC_SYNCED
  | typeof WIKI_SOURCE_DOC_FAILED

/**
 * 网页爬虫源配置。抓取范围采用「同站点 + 路径前缀限定」策略，
 * 以深度/页数上限与请求间隔（限速）多重兜底，避免把目标站点抓崩。
 */
export interface WikiSourceCrawlerConfig {
  /** 起始 URL（爬取入口），必填 */
  startUrl: string
  /** 抓取路径前缀限定；空串表示限定为起始 URL 所在目录 */
  pathPrefix?: string | null
  /** 链接遍历最大深度，1 表示仅抓起始页，0 表示取默认值 */
  maxDepth?: number | null
  /** 单轮爬取页面数量上限，0 表示取默认值 */
  maxPages?: number | null
  /** 相邻两次请求的最小间隔秒数（限速核心），0 表示取默认值 */
  requestIntervalSeconds?: number | null
  /** 单次请求超时秒数，0 表示取默认值 */
  timeoutSeconds?: number | null
  /** 请求 UserAgent，空串表示取默认值 */
  userAgent?: string | null
  /** 正文选择器（CSS），空串表示抽取整个 body */
  contentSelector?: string | null
  /** 是否覆盖已抓取且内容变化的页面；false 表示已存在的页面直接跳过 */
  isOverwriteExisting?: boolean | null
}

/** 飞书文档源配置（不含任何密钥） */
export interface WikiSourceFeishuConfig {
  feishuAppId?: string | null
  nodeToken?: string | null
  spaceId?: string | null
  includeSubNodes?: boolean | null
  maxDepth?: number | null
  maxDocuments?: number | null
}

/** 外部源工作流配置：与知识库默认工作流同构，为 null 表示回退知识库默认工作流 */
export type WikiSourceWorkflowConfig = WikiWorkflowConfig

export interface WikiSourceItem {
  sourceId?: string | null
  wikiId?: string | number | null
  sourceType?: WikiSourceType | null
  name?: string | null
  description?: string | null
  isEnable?: boolean | null
  /** 定时同步 cron 表达式（UTC），空表示未开启 */
  cron?: string | null
  isEventSubscription?: boolean | null
  workflowConfig?: WikiSourceWorkflowConfig | null
  feishu?: WikiSourceFeishuConfig | null
  crawler?: WikiSourceCrawlerConfig | null
  feishuAppName?: string | null
  feishuAppOpenId?: string | null
  feishuAppOnline?: boolean | null
  lastSyncStatus?: WikiSourceSyncStatus | null
  lastSyncTime?: string | null
  lastSyncMessage?: string | null
  documentCount?: number | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
}

export interface WikiSourcesResult {
  /** 当前用户在知识库所属团队的角色：0=Member 1=Admin 2=Owner */
  myRole?: number | null
  items?: WikiSourceItem[] | null
}

/** 外部源文档映射项 */
export interface WikiSourceDocumentItem {
  sourceId?: string | null
  externalKey?: string | null
  externalTitle?: string | null
  externalPath?: string | null
  /** 知识库文档 id（后端 long 序列化为字符串） */
  documentId?: string | null
  fileName?: string | null
  status?: WikiSourceDocumentStatus | null
  lastSyncTime?: string | null
  lastError?: string | null
  revision?: string | null
}

export interface WikiSourceDocumentsResult {
  total?: number | null
  items?: WikiSourceDocumentItem[] | null
}

/** 外部源同步结果 */
export interface WikiSourceSyncResult {
  total?: number | null
  created?: number | null
  updated?: number | null
  unchanged?: number | null
  skipped?: number | null
  failed?: number | null
  workflowTriggered?: number | null
  message?: string | null
}

/** 创建外部源入参：飞书与爬虫两种形态按 sourceType 二选一填写 */
export interface CreateWikiSourcePayload {
  sourceType: WikiSourceType
  name: string
  description?: string
  /** 爬虫源配置（sourceType=1 时必填） */
  crawler?: WikiSourceCrawlerConfig
  /** 飞书文档源：方式一，选择已有飞书应用连接 */
  feishuAppId?: string | null
  /** 飞书文档源：方式二，新建连接 */
  newAppName?: string | null
  newAppId?: string | null
  newAppSecret?: string | null
  newAppDomain?: string | null
  /** 飞书文档源：知识空间节点 token */
  nodeToken?: string | null
  includeSubNodes?: boolean | null
  maxDepth?: number | null
  maxDocuments?: number | null
  /** 定时同步 cron 表达式（UTC） */
  cron?: string | null
  isEventSubscription?: boolean | null
  isEnable?: boolean | null
}

/** 更新外部源入参：未传字段保持原值；cron 传空串表示关闭定时同步 */
export interface UpdateWikiSourcePayload {
  name?: string
  description?: string
  crawler?: WikiSourceCrawlerConfig
  feishuAppId?: string | null
  newAppName?: string | null
  newAppId?: string | null
  newAppSecret?: string | null
  newAppDomain?: string | null
  nodeToken?: string | null
  includeSubNodes?: boolean | null
  maxDepth?: number | null
  maxDocuments?: number | null
  cron?: string | null
  isEventSubscription?: boolean | null
  isEnable?: boolean | null
}

/** 查询知识库外部源列表（含爬虫配置与最近同步状态） */
export async function getWikiSources(wikiId: number): Promise<WikiSourcesResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).sources.get()
  return { myRole: res?.myRole, items: (res?.items ?? []) as unknown as WikiSourceItem[] }
}

/** 创建外部源（爬虫源创建后立即抓取一次；飞书源创建后自动绑定渠道并拉取一次） */
export async function createWikiSource(wikiId: number, payload: CreateWikiSourcePayload): Promise<string> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).sources.post({
    wikiId: String(wikiId),
    sourceType: payload.sourceType,
    name: payload.name,
    description: payload.description,
    crawler: payload.crawler as never,
    feishuAppId: payload.feishuAppId ?? null,
    newAppName: payload.newAppName ?? null,
    newAppId: payload.newAppId ?? null,
    newAppSecret: payload.newAppSecret ?? null,
    newAppDomain: payload.newAppDomain ?? null,
    nodeToken: payload.nodeToken ?? undefined,
    includeSubNodes: payload.includeSubNodes ?? undefined,
    maxDepth: payload.maxDepth ?? undefined,
    maxDocuments: payload.maxDocuments ?? undefined,
    cron: payload.cron ?? null,
    isEventSubscription: payload.isEventSubscription ?? undefined,
    isEnable: payload.isEnable ?? true,
  })
  return res?.value ?? ''
}

/** 更新外部源（整体覆盖式保存本次提交的配置） */
export async function updateWikiSource(wikiId: number, sourceId: string, payload: UpdateWikiSourcePayload): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).sources.bySourceId(sourceId).put({
    wikiId: String(wikiId),
    sourceId,
    name: payload.name,
    description: payload.description,
    crawler: payload.crawler as never,
    feishuAppId: payload.feishuAppId ?? null,
    newAppName: payload.newAppName ?? null,
    newAppId: payload.newAppId ?? null,
    newAppSecret: payload.newAppSecret ?? null,
    newAppDomain: payload.newAppDomain ?? null,
    nodeToken: payload.nodeToken ?? null,
    includeSubNodes: payload.includeSubNodes ?? null,
    maxDepth: payload.maxDepth ?? null,
    maxDocuments: payload.maxDocuments ?? null,
    cron: payload.cron ?? null,
    isEventSubscription: payload.isEventSubscription ?? null,
    isEnable: payload.isEnable ?? null,
  })
}

/** 删除外部源；已同步进知识库的文档保留 */
export async function deleteWikiSource(wikiId: number, sourceId: string): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).sources.bySourceId(sourceId).delete()
}

/** 手动触发外部源同步；force=true 时忽略内容哈希比对，对全部文档触发工作流 */
export async function syncWikiSource(wikiId: number, sourceId: string, force = false): Promise<WikiSourceSyncResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).sources.bySourceId(sourceId).sync.post({
    wikiId: String(wikiId),
    sourceId,
    force,
  })
  return {
    total: res?.total,
    created: res?.created,
    updated: res?.updated,
    unchanged: res?.unchanged,
    skipped: res?.skipped,
    failed: res?.failed,
    workflowTriggered: res?.workflowTriggered,
    message: res?.message,
  }
}

/** 查询外部源已同步的文档列表 */
export async function getWikiSourceDocuments(
  wikiId: number,
  sourceId: string,
  params: { pageNo: number; pageSize: number; query?: string },
): Promise<WikiSourceDocumentsResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).sources.bySourceId(sourceId).documents.post({
    wikiId: String(wikiId),
    sourceId,
    pageNo: params.pageNo,
    pageSize: params.pageSize,
    query: params.query || undefined,
  })
  return {
    total: res?.total != null ? Number(res.total) : 0,
    items: (res?.items ?? []) as unknown as WikiSourceDocumentItem[],
  }
}

// ==================== 默认工作流 / 批量执行 ====================

export type WikiWorkflowPartitionMode = 'normal' | 'ai'

/** 批量工作流单文档执行结果 */
export interface BatchWorkflowDocumentResult {
  documentId?: number | null
  fileName?: string | null
  success?: boolean | null
  message?: string | null
  taskId?: string | null
}

export interface BatchRunWorkflowPayload {
  documentIds: number[]
  isPartition: boolean
  isAiPartition?: boolean
  aiModelId?: string
  promptTemplate?: string | null
  splitMode?: WikiDocumentPartitionSplitMode
  chunkSize?: number
  chunkOverlap?: number
  overlapUnit?: WikiDocumentPartitionOverlapUnit
  sizeUnit?: WikiDocumentPartitionSizeUnit
  tokenEncodingOrModel?: string | null
  isGenerateMetadata: boolean
  metadataModelId?: string
  strategyTypes?: WikiMetadataGenerationStrategy[] | null
  isEmbedding: boolean
  embedSourceText?: boolean
  embedMetadata?: boolean
}

/** 保存知识库默认工作流配置（三步预设，整体覆盖；某步骤传 null 表示清除该预设） */
export async function updateWikiWorkflowConfig(wikiId: number, payload: WikiSourceWorkflowConfig): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).workflowConfig.put({
    wikiId: String(wikiId),
    partition: payload.partition as never,
    metadata: payload.metadata as never,
    embedding: payload.embedding as never,
  })
}

/** 批量执行文档工作流：按勾选步骤（切割/生成元数据/向量化）一次性处理多个文档，返回逐文档结果 */
export async function batchRunWikiDocumentsWorkflow(wikiId: number, payload: BatchRunWorkflowPayload): Promise<BatchWorkflowDocumentResult[]> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.batchWorkflow.post({
    wikiId: String(wikiId),
    documentIds: payload.documentIds.map((id) => String(id)),
    isPartition: payload.isPartition,
    isAiPartition: payload.isPartition ? (payload.isAiPartition ?? false) : undefined,
    aiModelId: payload.isPartition && payload.isAiPartition ? (payload.aiModelId ?? null) : null,
    promptTemplate: payload.isPartition && payload.isAiPartition ? (payload.promptTemplate || null) : null,
    splitMode: payload.isPartition && !payload.isAiPartition ? (payload.splitMode ?? 'markdown') : undefined,
    chunkSize: payload.isPartition && !payload.isAiPartition ? (payload.chunkSize ?? 0) : undefined,
    chunkOverlap: payload.isPartition && !payload.isAiPartition ? (payload.chunkOverlap ?? 0) : undefined,
    overlapUnit: payload.isPartition && !payload.isAiPartition ? (payload.overlapUnit ?? 'character') : undefined,
    sizeUnit: payload.isPartition && !payload.isAiPartition ? (payload.sizeUnit ?? 'character') : undefined,
    tokenEncodingOrModel: payload.isPartition && !payload.isAiPartition && payload.sizeUnit === 'token' ? (payload.tokenEncodingOrModel || null) : null,
    isGenerateMetadata: payload.isGenerateMetadata,
    metadataModelId: payload.isGenerateMetadata ? (payload.metadataModelId ?? null) : null,
    strategyTypes: payload.isGenerateMetadata ? (payload.strategyTypes ?? null) : null,
    isEmbedding: payload.isEmbedding,
    embedSourceText: payload.isEmbedding ? (payload.embedSourceText ?? true) : undefined,
    embedMetadata: payload.isEmbedding ? (payload.embedMetadata ?? true) : undefined,
  })
  return (res?.items ?? []).map((item) => ({
    documentId: item.documentId != null ? Number(item.documentId) : null,
    fileName: item.fileName,
    success: item.success,
    message: item.message,
    taskId: item.taskId,
  }))
}
