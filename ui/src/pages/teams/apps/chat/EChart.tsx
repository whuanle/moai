import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { theme } from 'antd'
import * as echarts from 'echarts/core'
import {
  BarChart,
  BoxplotChart,
  FunnelChart,
  GaugeChart,
  GraphChart,
  HeatmapChart,
  LineChart,
  PieChart,
  RadarChart,
  SankeyChart,
  ScatterChart,
  SunburstChart,
  TreemapChart,
} from 'echarts/charts'
import {
  DataZoomComponent,
  DatasetComponent,
  GridComponent,
  LegendComponent,
  MarkAreaComponent,
  MarkLineComponent,
  MarkPointComponent,
  PolarComponent,
  RadarComponent,
  TitleComponent,
  ToolboxComponent,
  TooltipComponent,
  TransformComponent,
  VisualMapComponent,
} from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

// 按需注册常见图表/组件（模型生成的 ECharts option 以常用图为主），控制打包体积
echarts.use([
  BarChart, LineChart, PieChart, ScatterChart, RadarChart, FunnelChart, GaugeChart,
  HeatmapChart, TreemapChart, SunburstChart, BoxplotChart, SankeyChart, GraphChart,
  GridComponent, TooltipComponent, LegendComponent, TitleComponent, DatasetComponent,
  DataZoomComponent, TransformComponent, ToolboxComponent, VisualMapComponent,
  MarkLineComponent, MarkPointComponent, MarkAreaComponent, PolarComponent, RadarComponent,
  CanvasRenderer,
])

interface EChartProps {
  /** ECharts option（模型产出的纯 JSON 对象；无函数） */
  option: Record<string, unknown>
}

/**
 * ECharts 渲染包装：init 一次 + ResizeObserver 自适应，option 变化整体替换；
 * 文本色随 antd token（暗色主题可读），option 字段优先生效。
 * 容器常驻不因错误卸载（卸载会使图表实例与容器脱钩、换有效 option 后永远空白），
 * 渲染失败以覆盖层提示，option 更新后自动重试恢复.
 */
export function EChart({ option }: EChartProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const containerRef = useRef<HTMLDivElement | null>(null)
  const chartRef = useRef<echarts.ECharts | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const chart = echarts.init(el)
    chartRef.current = chart
    const observer = new ResizeObserver(() => chart.resize())
    observer.observe(el)
    return () => {
      observer.disconnect()
      chart.dispose()
      chartRef.current = null
    }
  }, [])

  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return
    try {
      chart.setOption(
        {
          textStyle: { color: token.colorText },
          ...option,
        } as echarts.EChartsCoreOption,
        { notMerge: true },
      )
      setFailed(false)
    } catch {
      setFailed(true)
    }
  }, [option, token])

  return (
    <div className="moai-chat__panel-chart-wrap">
      <div ref={containerRef} className="moai-chat__panel-chart" />
      {failed && <div className="moai-chat__panel-chart-error">{t('appChat.uiChartInvalid')}</div>}
    </div>
  )
}
