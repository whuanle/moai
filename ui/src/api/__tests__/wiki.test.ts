import { afterAll, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DocumentsRequestBuilder } from '@/api/client/api/wiki/item/documents'
import type { AiPartitionRequestBuilder } from '@/api/client/api/wiki/item/documents/item/aiPartition'
import type { EmbeddingRequestBuilder } from '@/api/client/api/wiki/item/documents/item/embedding'
import type { PartitionRequestBuilder } from '@/api/client/api/wiki/item/documents/item/partition'
import type { ListRequestBuilder } from '@/api/client/api/wiki/item/documents/list'
import type { PreuploadRequestBuilder } from '@/api/client/api/wiki/item/documents/preupload'
import type { ModelOptionsRequestBuilder } from '@/api/client/api/wiki/modelOptions'
import {
  aiPartitionWikiDocument,
  completeWikiDocument,
  deleteWikiDocuments,
  generateWikiDocumentChunkMetadata,
  generateWikiDocumentChunksMetadata,
  getWikiDocumentContent,
  getWikiDocumentEmbedding,
  getWikiDocuments,
  getWikiModelOptions,
  downloadWikiDocument,
  extractWikiDocumentContent,
  partitionWikiDocument,
  preUploadWikiDocument,
  renameWikiDocument,
  triggerDocumentEmbedding,
  updateWikiEmbeddingConfig,
  updateWikiRerankModel,
} from '@/api/wiki'

const { getApiClientMock } = vi.hoisted(() => ({
  getApiClientMock: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: getApiClientMock,
}))

const listPost = vi.fn<ListRequestBuilder['post']>().mockResolvedValue({
  items: null,
  total: null,
  pageNo: null,
  pageSize: null,
})
const preuploadPost = vi.fn<PreuploadRequestBuilder['post']>().mockResolvedValue({
  fileId: '23',
  isExist: false,
  uploadUrl: 'https://storage.example/upload',
  expiration: '2026-09-10T12:00:00Z',
})
const completePost = vi.fn().mockResolvedValue(undefined)
const documentsDelete = vi.fn<DocumentsRequestBuilder['delete']>().mockResolvedValue(undefined)
const renamePut = vi.fn().mockResolvedValue(undefined)
const downloadGet = vi.fn().mockResolvedValue({ value: 'https://storage.example/download' })
const embeddingGet = vi.fn<EmbeddingRequestBuilder['get']>().mockResolvedValue({
  wikiId: 7,
  documentId: 11,
  fileName: 'guide.md',
  items: [],
})
const embeddingPost = vi.fn<EmbeddingRequestBuilder['post']>().mockResolvedValue(undefined)
const extractPost = vi.fn().mockResolvedValue(undefined)
const contentGet = vi.fn().mockResolvedValue({ value: '# Guide' })
const partitionPost = vi.fn<PartitionRequestBuilder['post']>().mockResolvedValue(undefined)
const aiPartitionPost = vi.fn<AiPartitionRequestBuilder['post']>().mockResolvedValue(undefined)
const metadataGeneratePost = vi.fn().mockResolvedValue({ value: 4 })
const documentMetadataGeneratePost = vi.fn().mockResolvedValue({ value: 6 })
const modelOptionsGet = vi.fn<ModelOptionsRequestBuilder['get']>().mockResolvedValue(undefined)
const embeddingConfigPut = vi.fn().mockResolvedValue(undefined)
const rerankModelPut = vi.fn().mockResolvedValue(undefined)

const byChunkId = vi.fn(() => ({
  metadata: {
    generate: { post: metadataGeneratePost },
  },
}))

function createDocumentBuilder(): ReturnType<DocumentsRequestBuilder['byDocumentId']> {
  return {
    rename: { put: renamePut },
    download: { get: downloadGet },
    embedding: { get: embeddingGet, post: embeddingPost },
    extract: { post: extractPost },
    content: { get: contentGet },
    partition: { post: partitionPost },
    aiPartition: { post: aiPartitionPost },
    chunks: {
      byChunkId,
      metadata: {
        generate: { post: documentMetadataGeneratePost },
      },
    },
  } as unknown as ReturnType<DocumentsRequestBuilder['byDocumentId']>
}

const byDocumentId = vi.fn<DocumentsRequestBuilder['byDocumentId']>(createDocumentBuilder)

const byId = vi.fn(() => ({
  documents: {
    list: { post: listPost },
    preupload: { post: preuploadPost },
    complete: { post: completePost },
    delete: documentsDelete,
    byDocumentId,
  },
  embeddingConfig: { put: embeddingConfigPut },
  rerankModel: { put: rerankModelPut },
}))

const apiClient = {
  api: {
    wiki: {
      byId,
      modelOptions: { get: modelOptionsGet },
    },
  },
}

const fetchMock = vi.fn()
const digestMock = vi.fn().mockResolvedValue(new Uint8Array([0xab, 0xcd]).buffer)

function mockFetchJson(body: unknown = {}): void {
  fetchMock.mockResolvedValue({
    ok: true,
    status: 200,
    json: vi.fn().mockResolvedValue(body),
    text: vi.fn().mockResolvedValue(''),
  } as unknown as Response)
}

function expectNoFetch(): void {
  expect(fetchMock).toHaveBeenCalledTimes(0)
}

describe('Wiki API Kiota contracts', () => {
  beforeEach(() => {
    getApiClientMock.mockReset().mockReturnValue(apiClient)
    listPost.mockReset().mockResolvedValue({
      items: null,
      total: null,
      pageNo: null,
      pageSize: null,
    })
    preuploadPost.mockReset().mockResolvedValue({
      fileId: '23',
      isExist: false,
      uploadUrl: 'https://storage.example/upload',
      expiration: '2026-09-10T12:00:00Z',
    })
    completePost.mockReset().mockResolvedValue(undefined)
    documentsDelete.mockReset().mockResolvedValue(undefined)
    renamePut.mockReset().mockResolvedValue(undefined)
    downloadGet.mockReset().mockResolvedValue({ value: 'https://storage.example/download' })
    embeddingGet.mockReset().mockResolvedValue({
      wikiId: 7,
      documentId: 11,
      fileName: 'guide.md',
      items: [],
    })
    embeddingPost.mockReset().mockResolvedValue(undefined)
    extractPost.mockReset().mockResolvedValue(undefined)
    contentGet.mockReset().mockResolvedValue({ value: '# Guide' })
    partitionPost.mockReset().mockResolvedValue(undefined)
    aiPartitionPost.mockReset().mockResolvedValue(undefined)
    metadataGeneratePost.mockReset().mockResolvedValue({ value: 4 })
    documentMetadataGeneratePost.mockReset().mockResolvedValue({ value: 6 })
    modelOptionsGet.mockReset().mockResolvedValue(undefined)
    embeddingConfigPut.mockReset().mockResolvedValue(undefined)
    rerankModelPut.mockReset().mockResolvedValue(undefined)
    byChunkId.mockReset().mockImplementation(() => ({
      metadata: {
        generate: { post: metadataGeneratePost },
      },
    }))
    byDocumentId.mockReset().mockImplementation(createDocumentBuilder)
    byId.mockReset().mockImplementation(() => ({
      documents: {
        list: { post: listPost },
        preupload: { post: preuploadPost },
        complete: { post: completePost },
        delete: documentsDelete,
        byDocumentId,
      },
      embeddingConfig: { put: embeddingConfigPut },
      rerankModel: { put: rerankModelPut },
    }))
    fetchMock.mockReset()
    mockFetchJson()
    digestMock.mockReset().mockResolvedValue(new Uint8Array([0xab, 0xcd]).buffer)
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('crypto', { subtle: { digest: digestMock } })
  })

  afterAll(() => {
    vi.unstubAllGlobals()
  })

  it('lists documents through the nested builder and preserves facade defaults', async () => {
    mockFetchJson({ items: null, total: null, pageNo: null, pageSize: null })

    const result = await getWikiDocuments(7, {
      pageNo: 2,
      pageSize: 20,
      query: 'guide',
      isEmbedding: false,
    })

    expect(result).toEqual({ items: [], total: 0, pageNo: 2, pageSize: 20 })
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(listPost).toHaveBeenCalledTimes(1)
    expect(listPost).toHaveBeenCalledWith({
      wikiId: '7',
      pageNo: 2,
      pageSize: 20,
      query: 'guide',
      isEmbedding: false,
    })
    expectNoFetch()
  })

  it('defaults omitted document list filters in the request body', async () => {
    await getWikiDocuments(7, { pageNo: 1, pageSize: 10 })

    expect(listPost).toHaveBeenCalledTimes(1)
    expect(listPost).toHaveBeenCalledWith({
      wikiId: '7',
      pageNo: 1,
      pageSize: 10,
      query: undefined,
      isEmbedding: null,
    })
    expectNoFetch()
  })

  it('normalizes an undefined document list response', async () => {
    listPost.mockResolvedValueOnce(undefined)

    const result = await getWikiDocuments(7, { pageNo: 2, pageSize: 20 })

    expect(result).toEqual({ items: [], total: 0, pageNo: 2, pageSize: 20 })
    expect(listPost).toHaveBeenCalledTimes(1)
    expectNoFetch()
  })

  it('pre-uploads a document through the preupload builder', async () => {
    mockFetchJson({
      fileId: 23,
      isExist: false,
      uploadUrl: 'https://storage.example/upload',
      expiration: '2026-09-10T12:00:00Z',
    })
    const buffer = new Uint8Array([1, 2, 3]).buffer
    const arrayBuffer = vi.fn().mockResolvedValue(buffer)
    const file = {
      name: 'guide.md',
      type: 'text/markdown',
      size: 128,
      arrayBuffer,
    } as unknown as File

    const result = await preUploadWikiDocument(7, file)

    expect(result).toEqual({
      fileId: 23,
      isExist: false,
      uploadUrl: 'https://storage.example/upload',
      expiration: '2026-09-10T12:00:00Z',
    })
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(preuploadPost).toHaveBeenCalledTimes(1)
    expect(preuploadPost).toHaveBeenCalledWith({
      wikiId: '7',
      fileName: 'guide.md',
      contentType: 'text/markdown',
      fileSize: 128,
      shA256: 'abcd',
    })
    expect(arrayBuffer).toHaveBeenCalledTimes(1)
    expect(digestMock).toHaveBeenCalledTimes(1)
    expect(digestMock).toHaveBeenCalledWith('SHA-256', buffer)
    expectNoFetch()
  })

  it('uses the binary content type when the file type is empty', async () => {
    const file = {
      name: 'unknown.bin',
      type: '',
      size: 3,
      arrayBuffer: vi.fn().mockResolvedValue(new Uint8Array([1, 2, 3]).buffer),
    } as unknown as File

    await preUploadWikiDocument(7, file)

    expect(preuploadPost).toHaveBeenCalledTimes(1)
    expect(preuploadPost).toHaveBeenCalledWith({
      wikiId: '7',
      fileName: 'unknown.bin',
      contentType: 'application/octet-stream',
      fileSize: 3,
      shA256: 'abcd',
    })
    expectNoFetch()
  })

  it('passes through an undefined pre-upload response', async () => {
    preuploadPost.mockResolvedValueOnce(undefined)
    const file = {
      name: 'guide.md',
      type: 'text/markdown',
      size: 0,
      arrayBuffer: vi.fn().mockResolvedValue(new ArrayBuffer(0)),
    } as unknown as File

    const result = await preUploadWikiDocument(7, file)

    expect(result).toBeUndefined()
    expect(preuploadPost).toHaveBeenCalledTimes(1)
    expectNoFetch()
  })

  it('completes a document upload through the complete builder', async () => {
    await completeWikiDocument(7, {
      fileId: 23,
      fileName: 'guide.md',
      isSuccess: true,
    })

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(completePost).toHaveBeenCalledTimes(1)
    expect(completePost).toHaveBeenCalledWith({
      wikiId: '7',
      fileId: '23',
      fileName: 'guide.md',
      isSuccess: true,
    })
    expectNoFetch()
  })

  it('deletes documents with string wiki and document IDs', async () => {
    await deleteWikiDocuments(7, [11, 12])

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(documentsDelete).toHaveBeenCalledTimes(1)
    expect(documentsDelete).toHaveBeenCalledWith({
      wikiId: '7',
      documentIds: ['11', '12'],
    })
    expectNoFetch()
  })

  it('gets a document download URL through the download builder', async () => {
    mockFetchJson({ value: 'https://storage.example/download' })

    const result = await downloadWikiDocument(7, 11)

    expect(result).toBe('https://storage.example/download')
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(downloadGet).toHaveBeenCalledTimes(1)
    expect(downloadGet).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
  ])('falls back to an empty download URL for a %s response', async (_label, response) => {
    downloadGet.mockResolvedValueOnce(response)

    const result = await downloadWikiDocument(7, 11)

    expect(result).toBe('')
    expect(downloadGet).toHaveBeenCalledTimes(1)
    expect(downloadGet).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it('renames a document through its item builder with string IDs', async () => {
    await renameWikiDocument(7, 11, 'renamed.md')

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(renamePut).toHaveBeenCalledTimes(1)
    expect(renamePut).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      fileName: 'renamed.md',
    })
    expectNoFetch()
  })

  it('gets document embedding details through the embedding builder', async () => {
    const embedding = {
      wikiId: 7,
      documentId: 11,
      fileName: 'guide.md',
      items: [],
    }
    mockFetchJson(embedding)

    const result = await getWikiDocumentEmbedding(7, 11)

    expect(result).toEqual(embedding)
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(embeddingGet).toHaveBeenCalledTimes(1)
    expect(embeddingGet).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it('passes through an undefined document embedding response', async () => {
    embeddingGet.mockResolvedValueOnce(undefined)

    const result = await getWikiDocumentEmbedding(7, 11)

    expect(result).toBeUndefined()
    expect(embeddingGet).toHaveBeenCalledTimes(1)
    expectNoFetch()
  })

  it('triggers embedding through the document embedding builder', async () => {
    await triggerDocumentEmbedding(7, 11, {
      isEmbedSourceText: false,
      isEmbedMetadata: true,
    })

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(embeddingPost).toHaveBeenCalledTimes(1)
    expect(embeddingPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      isEmbedSourceText: false,
      isEmbedMetadata: true,
    })
    expectNoFetch()
  })

  it('defaults omitted embedding booleans to true', async () => {
    await triggerDocumentEmbedding(7, 11, {})

    expect(embeddingPost).toHaveBeenCalledTimes(1)
    expect(embeddingPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      isEmbedSourceText: true,
      isEmbedMetadata: true,
    })
    expectNoFetch()
  })

  it('extracts document content through the extract builder', async () => {
    await extractWikiDocumentContent(7, 11)

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(extractPost).toHaveBeenCalledTimes(1)
    expect(extractPost).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it('gets extracted document content through the content builder', async () => {
    mockFetchJson({ value: '# Guide' })

    const result = await getWikiDocumentContent(7, 11)

    expect(result).toBe('# Guide')
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(contentGet).toHaveBeenCalledTimes(1)
    expect(contentGet).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
  ])('falls back to empty document content for a %s response', async (_label, response) => {
    contentGet.mockResolvedValueOnce(response)

    const result = await getWikiDocumentContent(7, 11)

    expect(result).toBe('')
    expect(contentGet).toHaveBeenCalledTimes(1)
    expect(contentGet).toHaveBeenCalledWith()
    expectNoFetch()
  })

  it('partitions a document through the partition builder', async () => {
    await partitionWikiDocument(7, 11, {
      splitMode: 'recursive',
      chunkSize: 512,
      chunkOverlap: 64,
      overlapUnit: 'character',
      sizeUnit: 'token',
      tokenEncodingOrModel: 'cl100k_base',
    })

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(partitionPost).toHaveBeenCalledTimes(1)
    expect(partitionPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      splitMode: 'recursive',
      chunkSize: 512,
      chunkOverlap: 64,
      overlapUnit: 'character',
      sizeUnit: 'token',
      tokenEncodingOrModel: 'cl100k_base',
    })
    expectNoFetch()
  })

  it('sends a null token encoding when partitioning by character', async () => {
    await partitionWikiDocument(7, 11, {
      splitMode: 'fixedSize',
      chunkSize: 512,
      chunkOverlap: 64,
      overlapUnit: 'character',
      sizeUnit: 'character',
      tokenEncodingOrModel: 'cl100k_base',
    })

    expect(partitionPost).toHaveBeenCalledTimes(1)
    expect(partitionPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      splitMode: 'fixedSize',
      chunkSize: 512,
      chunkOverlap: 64,
      overlapUnit: 'character',
      sizeUnit: 'character',
      tokenEncodingOrModel: null,
    })
    expectNoFetch()
  })

  it('AI-partitions a document through the ai-partition builder', async () => {
    await aiPartitionWikiDocument(7, 11, {
      aiModelId: 'conversation-model-id',
      promptTemplate: 'Split by topic',
    })

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(aiPartitionPost).toHaveBeenCalledTimes(1)
    expect(aiPartitionPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      aiModelId: 'conversation-model-id',
      promptTemplate: 'Split by topic',
    })
    expectNoFetch()
  })

  it('sends a null prompt template when AI partitioning omits it', async () => {
    await aiPartitionWikiDocument(7, 11, { aiModelId: 'conversation-model-id' })

    expect(aiPartitionPost).toHaveBeenCalledTimes(1)
    expect(aiPartitionPost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      aiModelId: 'conversation-model-id',
      promptTemplate: null,
    })
    expectNoFetch()
  })

  it('generates chunk metadata through the chunk builder and unwraps value', async () => {
    mockFetchJson({ value: 4 })

    const result = await generateWikiDocumentChunkMetadata(
      7,
      11,
      'chunk-3',
      'metadata-model-id',
      true,
      'questionGeneration',
    )

    expect(result).toBe(4)
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(byChunkId).toHaveBeenCalledTimes(1)
    expect(byChunkId).toHaveBeenCalledWith('chunk-3')
    expect(metadataGeneratePost).toHaveBeenCalledTimes(1)
    expect(metadataGeneratePost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      metadataModelId: 'metadata-model-id',
      chunkIds: ['chunk-3'],
      appendExisting: true,
      strategyType: 'questionGeneration',
    })
    expectNoFetch()
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
  ])('falls back to zero for a %s single chunk metadata response', async (_label, response) => {
    metadataGeneratePost.mockResolvedValueOnce(response)
    mockFetchJson({})

    const result = await generateWikiDocumentChunkMetadata(7, 11, 'chunk-3', 'metadata-model-id')

    expect(result).toBe(0)
    expect(metadataGeneratePost).toHaveBeenCalledTimes(1)
    expect(metadataGeneratePost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      metadataModelId: 'metadata-model-id',
      chunkIds: ['chunk-3'],
      appendExisting: false,
      strategyType: undefined,
    })
    expectNoFetch()
  })

  it('generates metadata for document chunks through the document metadata builder', async () => {
    mockFetchJson({ value: 6 })

    const result = await generateWikiDocumentChunksMetadata(
      7,
      11,
      'metadata-model-id',
      ['chunk-1', 'chunk-2'],
      'keywordSummaryFusion',
    )

    expect(result).toBe(6)
    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(byDocumentId).toHaveBeenCalledTimes(1)
    expect(byDocumentId).toHaveBeenCalledWith('11')
    expect(documentMetadataGeneratePost).toHaveBeenCalledTimes(1)
    expect(documentMetadataGeneratePost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      metadataModelId: 'metadata-model-id',
      chunkIds: ['chunk-1', 'chunk-2'],
      strategyType: 'keywordSummaryFusion',
    })
    expectNoFetch()
  })

  it('defaults omitted batch metadata chunk IDs to an empty array', async () => {
    await generateWikiDocumentChunksMetadata(7, 11, 'metadata-model-id')

    expect(documentMetadataGeneratePost).toHaveBeenCalledTimes(1)
    expect(documentMetadataGeneratePost).toHaveBeenCalledWith({
      wikiId: '7',
      documentId: '11',
      metadataModelId: 'metadata-model-id',
      chunkIds: [],
      strategyType: undefined,
    })
    expectNoFetch()
  })

  it.each([
    ['undefined', undefined],
    ['null', null],
  ])('falls back to zero for a %s batch metadata response', async (_label, response) => {
    documentMetadataGeneratePost.mockResolvedValueOnce(response)
    mockFetchJson({})

    const result = await generateWikiDocumentChunksMetadata(7, 11, 'metadata-model-id')

    expect(result).toBe(0)
    expect(documentMetadataGeneratePost).toHaveBeenCalledTimes(1)
    expectNoFetch()
  })

  it('gets model options through the model-options builder', async () => {
    const options = {
      embeddingModels: [{ id: 'embedding-id', name: 'Embedding', modelKind: 'embedding' }],
      conversationModels: [],
      rerankModels: [],
    }
    mockFetchJson(options)
    modelOptionsGet.mockResolvedValueOnce(options)

    const result = await getWikiModelOptions(3)

    expect(result).toEqual(options)
    expect(modelOptionsGet).toHaveBeenCalledTimes(1)
    expect(modelOptionsGet).toHaveBeenCalledWith({
      queryParameters: { teamId: 3 },
    })
    expectNoFetch()
  })

  it('passes through undefined model options', async () => {
    modelOptionsGet.mockResolvedValueOnce(undefined)

    const result = await getWikiModelOptions(3)

    expect(result).toBeUndefined()
    expect(modelOptionsGet).toHaveBeenCalledTimes(1)
    expect(modelOptionsGet).toHaveBeenCalledWith({
      queryParameters: { teamId: 3 },
    })
    expectNoFetch()
  })

  it('updates embedding configuration through the wiki item builder', async () => {
    await updateWikiEmbeddingConfig(7, {
      embeddingModelId: 'embedding-id',
      embeddingDimensions: 1536,
    })

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(embeddingConfigPut).toHaveBeenCalledTimes(1)
    expect(embeddingConfigPut).toHaveBeenCalledWith({
      wikiId: '7',
      embeddingModelId: 'embedding-id',
      embeddingDimensions: 1536,
    })
    expectNoFetch()
  })

  it('updates the rerank model through the wiki item builder', async () => {
    await updateWikiRerankModel(7, 'rerank-model-id')

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(rerankModelPut).toHaveBeenCalledTimes(1)
    expect(rerankModelPut).toHaveBeenCalledWith({
      wikiId: '7',
      rerankModelId: 'rerank-model-id',
    })
    expectNoFetch()
  })

  it('sends null unchanged when unbinding the rerank model', async () => {
    await updateWikiRerankModel(7, null)

    expect(byId).toHaveBeenCalledTimes(1)
    expect(byId).toHaveBeenCalledWith('7')
    expect(rerankModelPut).toHaveBeenCalledTimes(1)
    expect(rerankModelPut).toHaveBeenCalledWith({
      wikiId: '7',
      rerankModelId: null,
    })
    expectNoFetch()
  })
})