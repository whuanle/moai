/**
 * ui_show_chart 支持的图表类型白名单（与 @antv/gpt-vis 渲染组件一一对应）。
 * summary 为纯文本形态（非 type+data 结构）不在此列，文本产出走 ui_show_document；
 * 后端 FrontendToolContextProvider 的 chart 工具 description 须与此保持同步。
 */
export const VIS_CHART_TYPES: ReadonlySet<string> = new Set([
  'line',
  'area',
  'column',
  'bar',
  'pie',
  'scatter',
  'radar',
  'funnel',
  'waterfall',
  'dual-axes',
  'histogram',
  'boxplot',
  'violin',
  'venn',
  'sankey',
  'treemap',
  'word-cloud',
  'liquid',
  'flow-diagram',
  'network-graph',
  'mindmap',
  'indented-tree',
  'organization-chart',
  'fishbone-diagram',
  'table',
])
