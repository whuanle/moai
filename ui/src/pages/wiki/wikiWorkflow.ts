import {
  type WikiDocumentPartitionOverlapUnit,
  type WikiDocumentPartitionSizeUnit,
  type WikiDocumentPartitionSplitMode,
  type WikiMetadataGenerationStrategy,
  type WikiWorkflowPartitionMode,
} from '@/api/wiki'

export interface ModelOption {
  id?: string | null
  name?: string | null
}

/** 批量处理工作流表单值：三个步骤开关 + 各步骤参数（每次打开弹窗按通用默认值预填，实时调整后提交） */
export interface WikiWorkflowFormValues {
  partitionEnabled: boolean
  partitionMode: WikiWorkflowPartitionMode
  aiModelId?: string
  promptTemplate?: string
  splitMode: WikiDocumentPartitionSplitMode
  chunkSize: number
  chunkOverlap: number
  overlapUnit: WikiDocumentPartitionOverlapUnit
  sizeUnit: WikiDocumentPartitionSizeUnit
  tokenEncodingOrModel?: string
  metadataEnabled: boolean
  metadataModelId?: string
  strategyTypes: WikiMetadataGenerationStrategy[]
  embeddingEnabled: boolean
  embedSourceText: boolean
  embedMetadata: boolean
}

export const DEFAULT_WORKFLOW_VALUES: WikiWorkflowFormValues = {
  partitionEnabled: true,
  partitionMode: 'normal',
  aiModelId: undefined,
  promptTemplate: undefined,
  splitMode: 'markdown',
  chunkSize: 1000,
  chunkOverlap: 50,
  overlapUnit: 'character',
  sizeUnit: 'character',
  tokenEncodingOrModel: undefined,
  metadataEnabled: false,
  metadataModelId: undefined,
  strategyTypes: [],
  embeddingEnabled: true,
  embedSourceText: true,
  embedMetadata: true,
}

