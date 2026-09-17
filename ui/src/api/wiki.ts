import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

export interface WikiItem {
  /** 后端 long 序列化为字符串 */
  wikiId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  /** 是否公开，公开后所有人都可以使用（只读） */
  isPublic?: boolean | null
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

export async function createWiki(payload: { teamId: number; name: string; description?: string; isPublic?: boolean }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.post({ teamId: String(payload.teamId), name: payload.name, description: payload.description, isPublic: payload.isPublic })
  return Number(res?.value ?? 0)
}

export async function updateWiki(wikiId: number, payload: { name: string; description?: string; isPublic?: boolean }): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).put({ name: payload.name, description: payload.description, isPublic: payload.isPublic })
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
