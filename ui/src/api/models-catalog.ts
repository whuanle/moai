import type { AIChannelModelMeta } from '@/api/client/models'

/**
 * 前端内置的模型目录（models.json）与协议推断逻辑。
 * 目录文件位于 `ui/public/models.json`，运行时按需加载；
 * 渠道对应的协议族/协议风格由前端代码（inferProviderProtocol）推导，后端不感知。
 */

interface RawModel {
  id?: unknown
  name?: unknown
  description?: unknown
  family?: unknown
  attachment?: unknown
  reasoning?: unknown
  tool_call?: unknown
  structured_output?: unknown
  temperature?: unknown
  knowledge?: unknown
  release_date?: unknown
  last_updated?: unknown
  open_weights?: unknown
  modalities?: Record<string, unknown>
  limit?: Record<string, unknown>
  cost?: Record<string, unknown>
}

interface RawProvider {
  id?: unknown
  name?: unknown
  npm?: unknown
  api?: unknown
  models?: Record<string, RawModel> | RawModel[]
}

export interface CatalogProvider {
  /** 渠道标识，对应 models.json 的 provider id. */
  id: string
  /** 展示名称. */
  name: string
  /** 默认接入端点. */
  baseUrl: string
  /** 协议（已推断，方案族+风格组合，值需与后端枚举一致）. */
  protocol: string
  /** 目录内模型数量. */
  modelCount: number
}

type Catalog = Record<string, RawProvider>

let cache: Catalog | undefined

async function loadCatalog(): Promise<Catalog> {
  if (cache) return cache
  const res = await fetch('/models.json')
  if (!res.ok) {
    throw new Error('models.json 加载失败')
  }
  cache = (await res.json()) as Catalog
  return cache
}

function optionalString(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined
  const trimmed = value.trim()
  return trimmed || undefined
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined
}

/**
 * 依据 provider 的 id / npm 包推断协议族（baked-in，值需与后端枚举一致）.
 */
export function inferProviderProtocolFamily(providerId: string, npm?: string): string {
  const id = providerId.trim().toLowerCase()
  switch (id) {
    case 'openai':
      return 'openAI'
    case 'anthropic':
    case 'google-vertex-anthropic':
      return 'anthropic'
    case 'azure':
    case 'azure-cognitive-services':
    case 'lmstudio':
    case 'lm-studio':
    case 'openrouter':
    case 'github-copilot':
      return 'openAI'
    case 'google':
    case 'gemini':
    case 'google-vertex':
      return 'google'
    case 'ollama':
    case 'ollama-cloud':
      return 'ollama'
    default:
      break
  }

  if (npm === '@ai-sdk/openai' || npm === '@ai-sdk/openai-compatible' || npm === '@openrouter/ai-sdk-provider') {
    return 'openAI'
  }
  if (npm === '@ai-sdk/anthropic') return 'anthropic'
  if (npm === '@ai-sdk/azure') return 'openAI'
  if (npm === '@ai-sdk/google' || npm === '@ai-sdk/google-vertex') return 'google'
  if (npm === '@ai-sdk/amazon-bedrock' || npm === '@ai-sdk/cohere' || npm === '@ai-sdk/mistral') return 'openAI'

  return 'custom'
}

/**
 * 依据协议族推断协议风格（baked-in，值需与后端枚举一致）.
 */
export function inferProviderApiStyle(providerId: string, npm: string | undefined, protocolFamily: string): string {
  const id = providerId.trim().toLowerCase()
  if (protocolFamily === 'anthropic') return 'messages'
  if (protocolFamily === 'google') return 'generateContent'
  if (protocolFamily === 'ollama') return id === 'ollama' ? 'ollamaChat' : 'chatCompletions'
  if (protocolFamily === 'openAI') {
    if (id === 'openai' || npm === '@ai-sdk/openai') return 'responses'
    return 'chatCompletions'
  }
  return 'custom'
}

/**
 * 依据 provider 的 id/npm 推断组合协议（协议族+风格，值需与后端枚举一致）.
 */
function inferProviderProtocol(providerId: string, npm: string | undefined): string {
  const family = inferProviderProtocolFamily(providerId, npm)
  const style = inferProviderApiStyle(providerId, npm, family)
  if (family === 'openAI') return style === 'responses' ? 'openAIResponses' : 'openAIChatCompletions'
  if (family === 'anthropic') return 'anthropicMessages'
  if (family === 'google') return 'googleGemini'
  return 'openAIChatCompletions'
}

function buildProvider(raw: RawProvider): CatalogProvider | null {
  const id = optionalString(raw.id)
  if (!id) return null
  const npm = optionalString(raw.npm)
  const models = raw.models && typeof raw.models === 'object' && !Array.isArray(raw.models)
    ? Object.keys(raw.models).length
    : Array.isArray(raw.models)
      ? raw.models.length
      : 0

  return {
    id,
    name: optionalString(raw.name) ?? id,
    baseUrl: optionalString(raw.api) ?? '',
    protocol: inferProviderProtocol(id, npm),
    modelCount: models,
  }
}

/** 获取全部内置渠道（模型目录），含推断出的协议信息. */
export async function getCatalogProviders(): Promise<CatalogProvider[]> {
  const catalog = await loadCatalog()
  return Object.values(catalog)
    .map(buildProvider)
    .filter((p): p is CatalogProvider => p !== null)
    .sort((a, b) => a.name.localeCompare(b.name, 'en'))
}

/** 依据渠道标识（providerKey）查找内置 provider 的协议信息. */
export async function getCatalogProvider(providerKey: string): Promise<CatalogProvider | null> {
  const catalog = await loadCatalog()
  const raw = catalog[providerKey]
  return raw ? buildProvider(raw) : null
}

/** 将 models.json 的某个 provider 的模型列表解析为模型元数据. */
export async function getProviderModels(providerKey: string): Promise<AIChannelModelMeta[]> {
  const catalog = await loadCatalog()
  const provider = catalog[providerKey]
  if (!provider) return []

  const models = provider.models
  const list: unknown[] = Array.isArray(models) ? models : (models ? Object.values(models) : [])

  const readStringArray = (value: unknown): string[] | undefined =>
    Array.isArray(value)
      ? value.filter((v): v is string => typeof v === 'string').map((v) => v.trim()).filter(Boolean)
      : undefined

  const asNumber = (value: unknown): number | undefined => (typeof value === 'number' ? value : undefined)

  const results: AIChannelModelMeta[] = []
  for (const entry of list) {
    const model = asRecord(entry)
    if (!model) continue
    const modelId = optionalString(model.id)
    const name = optionalString(model.name)
    if (!modelId || !name) continue

    const limit = asRecord(model.limit)
    const cost = asRecord(model.cost)
    const modalities = asRecord(model.modalities)

    results.push({
      modelId,
      name,
      description: optionalString(model.description),
      family: optionalString(model.family),
      supportsAttachments: model.attachment === true,
      supportsReasoning: model.reasoning === true,
      supportsToolCall: model.tool_call === true,
      supportsStructuredOutput: model.structured_output === true,
      supportsTemperature: model.temperature === true,
      knowledgeCutoff: optionalString(model.knowledge),
      releaseDate: optionalString(model.release_date),
      lastUpdated: optionalString(model.last_updated),
      inputModalities: modalities ? readStringArray(modalities.input) : undefined,
      outputModalities: modalities ? readStringArray(modalities.output) : undefined,
      openWeights: model.open_weights === true,
      contextWindow: asNumber(limit?.context),
      maxOutput: asNumber(limit?.output),
      costInput: asNumber(cost?.input),
      costOutput: asNumber(cost?.output),
      costCacheRead: asNumber(cost?.cache_read),
    })
  }

  return results
}
