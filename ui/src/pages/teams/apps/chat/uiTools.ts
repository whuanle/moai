import type { ToolCallDisplay } from './ToolCallCard'
import { VIS_CHART_TYPES } from './visChartTypes'

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
  /** chart：GPT-Vis 图表配置（type + data 等，纯 JSON 对象） */
  chart?: Record<string, unknown>
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
 * 图表配置归一化：部分模型会把整个配置序列化成 JSON 字符串（或包一层 markdown 围栏）传给工具，
 * 统一归一为对象；无法归一出对象时返回 undefined（卡片按内容缺失降级）.
 */
function normalizeChartConfig(raw: unknown): Record<string, unknown> | undefined {
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

/** data 为单对象（而非数组）的图表类型：树/图家族 data={name,children} 或 {nodes,edges}，与 gpt-vis 渲染器读取形态对齐 */
const OBJECT_DATA_CHART_TYPES: ReadonlySet<string> = new Set([
  'mindmap',
  'indented-tree',
  'organization-chart',
  'fishbone-diagram',
  'network-graph',
  'flow-diagram',
])

/**
 * 判断对象是否为可渲染的图表配置：type 在支持列表内，且 data 形态与该类型的渲染契约一致
 * （缺数据或白名单外 type 会让渲染层失败）。dual-axes 顶层是 categories+series（无 data 数组）、
 * liquid 顶层是 percent，树/图家族 data 为对象，其余类型 data 为数组.
 */
function looksLikeVisChart(obj: Record<string, unknown>): boolean {
  const type = obj.type
  if (typeof type !== 'string' || !VIS_CHART_TYPES.has(type)) return false
  if (type === 'dual-axes') return Array.isArray(obj.categories) && Array.isArray(obj.series)
  if (type === 'liquid') return typeof obj.percent === 'number'
  if (OBJECT_DATA_CHART_TYPES.has(type)) {
    return !!obj.data && typeof obj.data === 'object' && !Array.isArray(obj.data)
  }
  return Array.isArray(obj.data)
}

/**
 * 行级图表的 x 字段契约（gpt-vis 渲染器硬编码 encode：line/area 用 time、column/bar/pie/waterfall/
 * funnel/boxplot/violin 用 category，y 一律 value）；数据行字段名不符时渲染器不报错，而是把全部行
 * 塌进一个 x 槽位叠画（如 20 根柱叠成 1 根），因此解析层做字段漂移修复.
 */
const CHART_ROW_X_FIELD: ReadonlyMap<string, string> = new Map([
  ['line', 'time'],
  ['area', 'time'],
  ['column', 'category'],
  ['bar', 'category'],
  ['pie', 'category'],
  ['waterfall', 'category'],
  ['funnel', 'category'],
  ['boxplot', 'category'],
  ['violin', 'category'],
])

/** x 字段的常见别名（模型漂移命名，按命中优先级）；y 别名单列 */
const X_FIELD_ALIASES: readonly string[] = ['time', 'category', 'date', 'month', 'day', 'year', 'name', 'label']
const Y_FIELD_ALIASES: readonly string[] = ['value', 'count', 'amount', 'total', 'sum']

/**
 * 字段漂移修复：数据行缺失该类型硬编码的 x/value 字段、但存在常见别名字段时，整列改名为契约字段
 * （如 column 数据写成 {time,value} 时把 time 改为 category——线上真实事故形态）。
 * 仅在期望字段完全缺失且别名无歧义时生效，正确的数据原样返回；非行级形态（dual-axes 等）不处理.
 */
function repairChartDataFields(chart: Record<string, unknown>): Record<string, unknown> {
  const type = typeof chart.type === 'string' ? chart.type : ''
  const xField = CHART_ROW_X_FIELD.get(type)
  if (!xField || !Array.isArray(chart.data) || chart.data.length === 0) return chart
  const first = chart.data[0]
  if (!first || typeof first !== 'object' || Array.isArray(first)) return chart

  const keys = Object.keys(first as Record<string, unknown>)
  const xAlias = keys.includes(xField)
    ? undefined
    : X_FIELD_ALIASES.find((k) => keys.includes(k) && k !== 'value')
  const yAlias = keys.includes('value')
    ? undefined
    : Y_FIELD_ALIASES.find((k) => keys.includes(k) && k !== xField && k !== xAlias)
  if (!xAlias && !yAlias) return chart

  const data = chart.data.map((row) => {
    if (!row || typeof row !== 'object' || Array.isArray(row)) return row
    const next: Record<string, unknown> = {}
    for (const [key, val] of Object.entries(row as Record<string, unknown>)) {
      if (xAlias && key === xAlias) next[xField] = val
      else if (yAlias && key === yAlias) next.value = val
      else next[key] = val
    }
    return next
  })
  return { ...chart, data }
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
    // 首选 chart 字段（归一化字符串/围栏形态）；归一化结果必须是可渲染的图表配置
    // （type 在白名单内且数据形态与类型契约一致）；模型漏嵌 chart 键而把配置直接铺在顶层时，
    // 去掉 title 后整体作为配置兜底；行级图表再做字段漂移修复
    let chart = normalizeChartConfig(args.chart)
    if (!chart || !looksLikeVisChart(chart)) {
      const rest = { ...args }
      delete rest.title
      chart = looksLikeVisChart(rest) ? rest : undefined
    }
    payload.chart = chart ? repairChartDataFields(chart) : undefined
  }
  return payload
}

/** 判断解析负载是否具备可渲染内容（决定卡片可否点击打开） */
export function hasRenderableContent(payload: UiToolPayload | null): boolean {
  if (!payload) return false
  if (payload.kind === 'document') return !!payload.content
  if (payload.kind === 'code') return !!payload.code
  return !!payload.chart
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
