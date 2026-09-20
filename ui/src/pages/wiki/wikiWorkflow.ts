import {
  type WikiDocumentPartitionOverlapUnit,
  type WikiDocumentPartitionSizeUnit,
  type WikiDocumentPartitionSplitMode,
  type WikiMetadataGenerationStrategy,
  type WikiWorkflowConfig,
  type WikiWorkflowPartitionMode,
} from '@/api/wiki'

export interface ModelOption {
  id?: string | null
  name?: string | null
}

/** 默认工作流 / 批量执行共用的表单值：三个步骤开关 + 各步骤参数 */
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

/** 知识库默认工作流配置 → 表单值（未配置的步骤按通用默认值预填） */
export function workflowValuesFromConfig(config?: WikiWorkflowConfig | null): WikiWorkflowFormValues {
  if (!config) return { ...DEFAULT_WORKFLOW_VALUES }
  return {
    partitionEnabled: config.partition != null,
    partitionMode: config.partition?.mode ?? DEFAULT_WORKFLOW_VALUES.partitionMode,
    aiModelId: config.partition?.aiModelId ?? undefined,
    promptTemplate: config.partition?.promptTemplate ?? undefined,
    splitMode: config.partition?.splitMode ?? DEFAULT_WORKFLOW_VALUES.splitMode,
    chunkSize: config.partition?.chunkSize ?? DEFAULT_WORKFLOW_VALUES.chunkSize,
    chunkOverlap: config.partition?.chunkOverlap ?? DEFAULT_WORKFLOW_VALUES.chunkOverlap,
    overlapUnit: config.partition?.overlapUnit ?? DEFAULT_WORKFLOW_VALUES.overlapUnit,
    sizeUnit: config.partition?.sizeUnit ?? DEFAULT_WORKFLOW_VALUES.sizeUnit,
    tokenEncodingOrModel: config.partition?.tokenEncodingOrModel ?? undefined,
    metadataEnabled: config.metadata != null,
    metadataModelId: config.metadata?.metadataModelId ?? undefined,
    strategyTypes: config.metadata?.strategyTypes ?? [],
    embeddingEnabled: config.embedding != null,
    embedSourceText: config.embedding?.embedSourceText ?? true,
    embedMetadata: config.embedding?.embedMetadata ?? true,
  }
}

/** 表单值 → 知识库默认工作流配置（未勾选的步骤为 null，保存时整体覆盖清除） */
export function workflowConfigFromValues(values: WikiWorkflowFormValues): WikiWorkflowConfig {
  const isAi = values.partitionEnabled && values.partitionMode === 'ai'
  return {
    partition: values.partitionEnabled
      ? {
        mode: values.partitionMode,
        aiModelId: isAi ? (values.aiModelId ?? null) : null,
        promptTemplate: isAi ? (values.promptTemplate || null) : null,
        splitMode: isAi ? null : values.splitMode,
        chunkSize: isAi ? null : values.chunkSize,
        chunkOverlap: isAi ? null : values.chunkOverlap,
        overlapUnit: isAi ? null : values.overlapUnit,
        sizeUnit: isAi ? null : values.sizeUnit,
        tokenEncodingOrModel: !isAi && values.sizeUnit === 'token' ? (values.tokenEncodingOrModel || null) : null,
      }
      : null,
    metadata: values.metadataEnabled
      ? {
        metadataModelId: values.metadataModelId ?? null,
        strategyTypes: values.strategyTypes.length > 0 ? values.strategyTypes : null,
      }
      : null,
    embedding: values.embeddingEnabled
      ? {
        embedSourceText: values.embedSourceText,
        embedMetadata: values.embedMetadata,
      }
      : null,
  }
}
