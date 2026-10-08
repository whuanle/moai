import type { ToolCallDisplay } from './ToolCallCard'

/** 前端展示工具名前缀（与后端 FrontendToolContract 保持同步）：识别到即转交侧边栏渲染，后端仅桩执行 */
export const UI_TOOL_PREFIX = 'ui_'

export const UI_TOOL_DOCUMENT = 'ui_show_document'
export const UI_TOOL_CODE = 'ui_show_code'
export const UI_TOOL_CHART = 'ui_show_chart'

export type UiToolKind = 'document' | 'code' | 'chart'

/** 前端展示工具的解析后内容：按 kind 取对应字段 */
export interface UiToolPayload {
  kind: UiToolKind
  title: string
  /** document：markdown 正文 */
  content?: string
  /** code：编程语言（可空） */
  language?: string
  /** code：完整代码文本 */
  code?: string
  /** chart：ECharts option（纯 JSON 对象） */
  option?: Record<string, unknown>
}

/** 判断工具调用是否为前端展示工具（固定前缀 ui_） */
export function isUiToolName(name: string): boolean {
  return name.startsWith(UI_TOOL_PREFIX)
}

function kindOf(name: string): UiToolKind | null {
  if (name === UI_TOOL_DOCUMENT) return 'document'
  if (name === UI_TOOL_CODE) return 'code'
  if (name === UI_TOOL_CHART) return 'chart'
  return null
}

/**
 * 图表 option 归一化：部分模型会把整个 option 序列化成 JSON 字符串（或包一层 markdown 围栏）传给工具，
 * 统一归一为对象；无法归一出对象时返回 undefined（卡片按内容缺失降级）.
 */
function normalizeChartOption(raw: unknown): Record<string, unknown> | undefined {
  if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
    return raw as Record<string, unknown>
  }
  if (typeof raw !== 'string') return undefined
  let text = raw.trim()
  if (text.startsWith('```')) {
    text = text.replace(/^```[a-zA-Z]*\s*/, '').replace(/```\s*$/, '').trim()
  }
  if (!text.startsWith('{')) return undefined
  try {
    const parsed: unknown = JSON.parse(text)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>
    }
  } catch {
    // 非法 JSON 保持 undefined
  }
  return undefined
}

/** 判断对象是否长得像 ECharts option（series/dataset/坐标轴等顶层键） */
function looksLikeEchartsOption(obj: Record<string, unknown>): boolean {
  return 'series' in obj || 'dataset' in obj || ('xAxis' in obj && 'yAxis' in obj) || 'radar' in obj
}

/**
 * 把前端展示工具调用解析为侧边栏渲染负载；参数流未结束（无 args）或非三类已知工具时返回 null，
 * 由卡片按「生成中/内容缺失」状态展示。参数异常字段一律忽略，不让模型输出中断渲染.
 */
export function parseUiToolPayload(name: string, argsJson?: string): UiToolPayload | null {
  const kind = kindOf(name)
  if (!kind) return null

  let args: Record<string, unknown> = {}
  if (argsJson && argsJson.trim()) {
    try {
      const parsed: unknown = JSON.parse(argsJson)
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        args = parsed as Record<string, unknown>
      }
    } catch {
      args = {}
    }
  }

  const title = typeof args.title === 'string' ? args.title.trim() : ''
  const payload: UiToolPayload = { kind, title }
  if (kind === 'document') {
    payload.content = typeof args.content === 'string' ? args.content : undefined
  } else if (kind === 'code') {
    payload.language = typeof args.language === 'string' && args.language.trim() ? args.language.trim() : undefined
    payload.code = typeof args.code === 'string' ? args.code : undefined
  } else {
    // 首选 option 字段（归一化字符串/围栏形态）；归一化结果必须具备 ECharts option 的基本结构
    // （series/dataset/坐标轴等，实测缺 series 的畸形 option 会让 echarts.setOption 抛 TypeError）；
    // 模型漏嵌 option 键而把 series 等直接铺在顶层时，去掉 title 后整体作为 option 兜底
    let option = normalizeChartOption(args.option)
    if (!option || !looksLikeEchartsOption(option)) {
      const rest = { ...args }
      delete rest.title
      option = looksLikeEchartsOption(rest) ? rest : undefined
    }
    payload.option = option
  }
  return payload
}

/** 判断解析负载是否具备可渲染内容（决定卡片可否点击打开） */
export function hasRenderableContent(payload: UiToolPayload | null): boolean {
  if (!payload) return false
  if (payload.kind === 'document') return !!payload.content
  if (payload.kind === 'code') return !!payload.code
  return !!payload.option
}

/** 侧边栏面板项：由工具调用负载生成，key 为调用 id 以支持历史卡片重开 */
export interface UiPanelItem extends UiToolPayload {
  key: string
}

/** 把消息内工具调用转为面板项（点击卡片打开历史/已完成内容时使用） */
export function toUiPanelItem(toolCall: ToolCallDisplay): UiPanelItem | null {
  const payload = parseUiToolPayload(toolCall.name, toolCall.argsJson)
  if (!payload || !hasRenderableContent(payload)) return null
  return { key: toolCall.id, ...payload }
}
