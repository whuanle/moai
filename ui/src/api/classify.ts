import { getApiClient } from '@/api/kiota'
import type {
  ClassifyItem,
  CreateClassifyCommand,
  DeleteClassifyCommand,
  UpdateClassifyCommand,
} from '@/api/client/models'

/** 分类类型（固定字符串，与后端 MoAI.Classify.ClassifyTypes 对齐）. */
export const ClassifyType = {
  Plugin: 'plugin',
  App: 'app',
  Kb: 'kb',
  Prompt: 'prompt',
  Skill: 'skill',
} as const

export type ClassifyTypeKey = (typeof ClassifyType)[keyof typeof ClassifyType]

/** 分类项（Kiota 生成类型别名）. */
export type Classify = ClassifyItem

/** 插件分类（兼容旧引用，指向通用分类项）. */
export type PluginClassify = ClassifyItem

export interface CreateClassifyPayload {
  type: ClassifyTypeKey
  name: string
  description?: string
  emoji?: string
}

export interface UpdateClassifyPayload {
  classifyId: number
  name: string
  description?: string
  emoji?: string
}

/** 分类显示文本（emoji + 名称，缺 emoji 时只显示名称）. */
export function classifyLabel(classify: Pick<Classify, 'emoji' | 'name'>): string {
  return [classify.emoji, classify.name].filter(Boolean).join(' ')
}

async function getClassifies(type?: ClassifyTypeKey): Promise<Classify[]> {
  const client = getApiClient()
  const res = await client.api.classify.list.get({
    queryParameters: type ? { type } : undefined,
  })
  return res?.items ?? []
}

/** 读取插件类型分类列表（插件模块只读，用于分类筛选）. */
async function getPluginClassifies(): Promise<PluginClassify[]> {
  return getClassifies(ClassifyType.Plugin)
}

async function createClassify(payload: CreateClassifyPayload): Promise<number> {
  const client = getApiClient()
  const res = await client.api.classify.post(payload as CreateClassifyCommand)
  return res?.value ?? 0
}

async function updateClassify(payload: UpdateClassifyPayload): Promise<void> {
  const client = getApiClient()
  await client.api.classify.put(payload as UpdateClassifyCommand)
}

async function deleteClassify(classifyId: number): Promise<void> {
  const client = getApiClient()
  await client.api.classify.delete({ classifyId } as DeleteClassifyCommand)
}

export const classifyApi = {
  getClassifies,
  getPluginClassifies,
  createClassify,
  updateClassify,
  deleteClassify,
}
