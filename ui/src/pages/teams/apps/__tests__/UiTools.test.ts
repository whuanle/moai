import { describe, expect, it } from 'vitest'
import {
  hasRenderableContent,
  isUiToolName,
  parseUiToolPayload,
  toUiPanelItem,
} from '../chat/uiTools'

describe('uiTools（前端展示工具解析）', () => {
  it('isUiToolName 只识别 ui_ 前缀', () => {
    expect(isUiToolName('ui_show_document')).toBe(true)
    expect(isUiToolName('ui_show_chart')).toBe(true)
    expect(isUiToolName('call_tool')).toBe(false)
    expect(isUiToolName('list_tools')).toBe(false)
    expect(isUiToolName('')).toBe(false)
  })

  it('ui_show_document 解析标题与 markdown 正文', () => {
    const payload = parseUiToolPayload('ui_show_document', '{"title":"季度报告","content":"# 摘要\\n内容"}')
    expect(payload).toMatchObject({ kind: 'document', title: '季度报告', content: '# 摘要\n内容' })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_code 解析标题/语言/代码，language 缺省可空', () => {
    const payload = parseUiToolPayload('ui_show_code', '{"title":"脚本","code":"print(1)"}')
    expect(payload).toMatchObject({ kind: 'code', title: '脚本', code: 'print(1)', language: undefined })
    const withLang = parseUiToolPayload('ui_show_code', '{"title":"脚本","language":"python","code":"print(1)"}')
    expect(withLang?.language).toBe('python')
    expect(hasRenderableContent(withLang)).toBe(true)
  })

  it('ui_show_chart 解析图表配置对象（type + data）', () => {
    const payload = parseUiToolPayload(
      'ui_show_chart',
      '{"title":"销量","chart":{"type":"bar","data":[{"category":"A","value":1}]}}',
    )
    expect(payload?.kind).toBe('chart')
    expect(payload?.chart).toEqual({ type: 'bar', data: [{ category: 'A', value: 1 }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的配置被模型序列化成 JSON 字符串时归一为对象（线上实测形态）', () => {
    const argsJson = JSON.stringify({
      title: '月度趋势',
      chart: JSON.stringify({ type: 'line', data: [{ time: '1月', value: 1 }, { time: '2月', value: 2 }] }),
    })
    const payload = parseUiToolPayload('ui_show_chart', argsJson)
    expect(payload?.chart).toEqual({ type: 'line', data: [{ time: '1月', value: 1 }, { time: '2月', value: 2 }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的配置为 markdown 围栏包裹的 JSON 字符串时归一为对象', () => {
    const argsJson = JSON.stringify({
      title: '占比',
      chart: '```json\n{"type":"pie","data":[{"category":"A","value":1}]}\n```',
    })
    const payload = parseUiToolPayload('ui_show_chart', argsJson)
    expect(payload?.chart).toEqual({ type: 'pie', data: [{ category: 'A', value: 1 }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 漏嵌 chart 键而配置铺在顶层时，去 title 后整体兜底为配置', () => {
    const payload = parseUiToolPayload(
      'ui_show_chart',
      '{"title":"销量","type":"bar","data":[{"category":"A","value":3}]}',
    )
    expect(payload?.chart).toEqual({ type: 'bar', data: [{ category: 'A', value: 3 }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的配置字符串内容非法（非 JSON 对象）时不可渲染', () => {
    const payload = parseUiToolPayload('ui_show_chart', JSON.stringify({ title: '坏图', chart: 'not-json' }))
    expect(payload?.chart).toBeUndefined()
    expect(hasRenderableContent(payload)).toBe(false)
  })

  it('ui_show_chart 缺 data 或 type 在白名单外时不可渲染', () => {
    // 缺 data：畸形配置会让渲染层失败
    const noData = parseUiToolPayload('ui_show_chart', '{"title":"坏图","chart":{"type":"line"}}')
    expect(noData?.chart).toBeUndefined()
    expect(hasRenderableContent(noData)).toBe(false)

    // 白名单外 type（summary 为纯文本形态已排除）
    const excluded = parseUiToolPayload('ui_show_chart', '{"title":"坏图","chart":{"type":"summary","data":[]}}')
    expect(excluded?.chart).toBeUndefined()
    expect(hasRenderableContent(excluded)).toBe(false)
  })

  it('历史消息里的旧 ECharts option（无 type）不再渲染（echarts 已移除，安全降级）', () => {
    const payload = parseUiToolPayload(
      'ui_show_chart',
      '{"title":"旧图","chart":{"xAxis":{"type":"category"},"series":[{"type":"bar","data":[1,2]}]}}',
    )
    expect(payload?.chart).toBeUndefined()
    expect(hasRenderableContent(payload)).toBe(false)
  })

  it('参数流未结束或非法参数返回空负载且不可渲染', () => {
    const noArgs = parseUiToolPayload('ui_show_document')
    expect(noArgs).toMatchObject({ kind: 'document' })
    expect(hasRenderableContent(noArgs)).toBe(false)

    const badJson = parseUiToolPayload('ui_show_chart', '{broken')
    expect(badJson?.kind).toBe('chart')
    expect(hasRenderableContent(badJson)).toBe(false)

    // 非对象参数形态（数组/标量）不中断解析
    const arrayArgs = parseUiToolPayload('ui_show_document', '[1,2]')
    expect(arrayArgs).toMatchObject({ kind: 'document', title: '' })
    expect(hasRenderableContent(arrayArgs)).toBe(false)
  })

  it('未知 ui_ 工具名返回 null（后端新增类型时前端安全降级）', () => {
    expect(parseUiToolPayload('ui_show_table', '{"title":"t"}')).toBeNull()
  })

  it('toUiPanelItem 携带调用 id 生成面板项，缺内容时返回 null', () => {
    const item = toUiPanelItem({
      id: 'tc-9',
      name: 'ui_show_code',
      argsJson: '{"title":"脚本","code":"print(1)"}',
      status: 'done',
    })
    expect(item).toMatchObject({ key: 'tc-9', kind: 'code', title: '脚本' })

    expect(toUiPanelItem({ id: 'tc-10', name: 'ui_show_code', status: 'done' })).toBeNull()
  })
})
