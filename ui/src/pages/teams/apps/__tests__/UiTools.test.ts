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

  it('ui_show_chart 解析 ECharts option 对象', () => {
    const payload = parseUiToolPayload('ui_show_chart', '{"title":"销量","option":{"series":[{"type":"bar","data":[1,2]}]}}')
    expect(payload?.kind).toBe('chart')
    expect(payload?.option).toEqual({ series: [{ type: 'bar', data: [1, 2] }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的 option 被模型序列化成 JSON 字符串时归一为对象（线上实测形态）', () => {
    const argsJson = JSON.stringify({
      title: '月度趋势',
      option: JSON.stringify({ xAxis: { type: 'category', data: ['1月', '2月'] }, series: [{ type: 'line', data: [1, 2] }] }),
    })
    const payload = parseUiToolPayload('ui_show_chart', argsJson)
    expect(payload?.option).toEqual({
      xAxis: { type: 'category', data: ['1月', '2月'] },
      series: [{ type: 'line', data: [1, 2] }],
    })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的 option 为 markdown 围栏包裹的 JSON 字符串时归一为对象', () => {
    const argsJson = JSON.stringify({
      title: '占比',
      option: '```json\n{"series":[{"type":"pie","data":[1,2]}]}\n```',
    })
    const payload = parseUiToolPayload('ui_show_chart', argsJson)
    expect(payload?.option).toEqual({ series: [{ type: 'pie', data: [1, 2] }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 漏嵌 option 键而 series 铺在顶层时，去 title 后整体兜底为 option', () => {
    const payload = parseUiToolPayload('ui_show_chart', '{"title":"销量","xAxis":{"type":"category"},"yAxis":{},"series":[{"type":"bar","data":[3]}]}')
    expect(payload?.option).toEqual({ xAxis: { type: 'category' }, yAxis: {}, series: [{ type: 'bar', data: [3] }] })
    expect(hasRenderableContent(payload)).toBe(true)
  })

  it('ui_show_chart 的 option 字符串内容非法（非 JSON 对象）时不可渲染', () => {
    const payload = parseUiToolPayload('ui_show_chart', JSON.stringify({ title: '坏图', option: 'not-json' }))
    expect(payload?.option).toBeUndefined()
    expect(hasRenderableContent(payload)).toBe(false)
  })

  it('ui_show_chart 的 option 为对象但缺 ECharts 基本结构（实测会使 echarts 抛 TypeError）时不可渲染', () => {
    // 线上实测形态：模型把 xAxis 内容打平到顶层，无 series/yAxis
    const payload = parseUiToolPayload(
      'ui_show_chart',
      JSON.stringify({ title: '坏图', option: { axisLabel: { rotate: 45 }, data: ['1月'], type: 'category', xAxis: { type: 'category' } } }),
    )
    expect(payload?.option).toBeUndefined()
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
