import {
  Area,
  Bar,
  Boxplot,
  Column,
  DualAxes,
  FishboneDiagram,
  FlowDiagram,
  Funnel,
  Histogram,
  IndentedTree,
  Line,
  Liquid,
  Mindmap,
  NetworkGraph,
  OrganizationChart,
  Pie,
  Radar,
  Sankey,
  Scatter,
  Table,
  Treemap,
  Venn,
  Violin,
  Waterfall,
  WordCloud,
} from '@antv/gpt-vis'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { VIS_CHART_TYPES } from './visChartTypes'

/** 图表类型 → GPT-Vis 渲染工厂（与 visChartTypes.ts 白名单保持一致） */
const RENDERERS: Record<string, (options: { container: HTMLElement }) => VisInstance> = {
  line: Line,
  area: Area,
  column: Column,
  bar: Bar,
  pie: Pie,
  scatter: Scatter,
  radar: Radar,
  funnel: Funnel,
  waterfall: Waterfall,
  'dual-axes': DualAxes,
  histogram: Histogram,
  boxplot: Boxplot,
  violin: Violin,
  venn: Venn,
  sankey: Sankey,
  treemap: Treemap,
  'word-cloud': WordCloud,
  liquid: Liquid,
  'flow-diagram': FlowDiagram,
  'network-graph': NetworkGraph,
  mindmap: Mindmap,
  'indented-tree': IndentedTree,
  'organization-chart': OrganizationChart,
  'fishbone-diagram': FishboneDiagram,
  table: Table,
}

/** 各图表实例的公共形态（render 入参按图表类型各异，由上游白名单校验保证结构） */
interface VisInstance {
  render: (config: never) => void
  destroy: () => void
}

interface VisChartProps {
  /** GPT-Vis 图表配置（type + data 等纯 JSON 对象，已过 uiTools 白名单校验） */
  chart: Record<string, unknown>
}

/**
 * GPT-Vis 图表渲染包装：按 type 取渲染工厂，实例与容器同生命周期，配置变化整体重渲染
 * （gpt-vis 的 render 内部自毁重建）；G2 autoFit 自适应容器尺寸，无需手动 resize。
 * 容器常驻不因错误卸载，渲染失败以覆盖层提示，配置更新后自动重试恢复.
 */
export function VisChart({ chart }: VisChartProps) {
  const { t } = useTranslation()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const el = containerRef.current
    const type = typeof chart.type === 'string' ? chart.type : ''
    const factory = VIS_CHART_TYPES.has(type) ? RENDERERS[type] : undefined
    if (!el || !factory) {
      setFailed(true)
      return undefined
    }
    try {
      const instance = factory({ container: el })
      instance.render(chart as never)
      setFailed(false)
      return () => instance.destroy()
    } catch {
      setFailed(true)
      return undefined
    }
  }, [chart])

  return (
    <div className="moai-chat__panel-chart-wrap">
      <div ref={containerRef} className="moai-chat__panel-chart" />
      {failed ? <div className="moai-chat__panel-chart-error">{t('appChat.uiChartInvalid')}</div> : null}
    </div>
  )
}
