import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Line } from '@antv/gpt-vis'
import '@/i18n'
import { VisChart } from '../chat/VisChart'

// gpt-vis 全量 mock（真实工厂依赖浏览器 canvas）；所有图表工厂统一为同一实例桩
const chartMocks = vi.hoisted(() => {
  const instance = { render: vi.fn(), destroy: vi.fn() }
  return { instance, factory: vi.fn(() => instance) }
})

vi.mock('@antv/gpt-vis', () =>
  Object.fromEntries(
    [
      'Area', 'Bar', 'Boxplot', 'Column', 'DualAxes', 'FishboneDiagram', 'FlowDiagram', 'Funnel',
      'Histogram', 'IndentedTree', 'Line', 'Liquid', 'Mindmap', 'NetworkGraph', 'OrganizationChart',
      'Pie', 'Radar', 'Sankey', 'Scatter', 'Table', 'Treemap', 'Venn', 'Violin', 'Waterfall',
      'WordCloud',
    ].map((name) => [name, chartMocks.factory]),
  ),
)

const factoryMock = vi.mocked(Line)
const renderMock = chartMocks.instance.render
const destroyMock = chartMocks.instance.destroy

const goodChart = { type: 'line', data: [{ time: '1月', value: 1 }] }
const badChart = { type: 'line', data: [{ time: '2月', value: 2 }] }

describe('VisChart（GPT-Vis 图表渲染包装：实例与容器同生命周期）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('挂载即按 type 取工厂：工厂收到容器元素，render 收到原配置，无错误覆盖层', () => {
    const { container } = render(<VisChart chart={goodChart} />)
    expect(factoryMock).toHaveBeenCalledTimes(1)
    const options = factoryMock.mock.calls[0][0] as { container: HTMLElement }
    expect(options.container).toBeInstanceOf(HTMLElement)
    expect(renderMock).toHaveBeenCalledWith(goodChart)
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
    expect(screen.queryByText('图表配置无效，无法渲染')).toBeNull()
  })

  it('chart 更新：先 destroy 旧实例再 render 新配置（不残留双实例）', () => {
    const { rerender } = render(<VisChart chart={goodChart} />)
    rerender(<VisChart chart={badChart} />)
    expect(factoryMock).toHaveBeenCalledTimes(2)
    expect(renderMock).toHaveBeenCalledTimes(2)
    expect(destroyMock).toHaveBeenCalledTimes(1)
    const renderOrder = renderMock.mock.invocationCallOrder
    const destroyOrder = destroyMock.mock.invocationCallOrder[0]
    expect(renderOrder[0]).toBeLessThan(destroyOrder)
    expect(destroyOrder).toBeLessThan(renderOrder[1])
  })

  it('render 抛异常：错误覆盖层出现，容器不卸载', () => {
    renderMock.mockImplementationOnce(() => {
      throw new TypeError('bad config')
    })
    const { container } = render(<VisChart chart={badChart} />)
    expect(screen.getByText('图表配置无效，无法渲染')).toBeInTheDocument()
    // 关键回归点：容器 div 必须仍在 DOM（条件卸载会让换有效配置后永远空白）
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
  })

  it('抛异常后换有效配置：自动重试恢复（覆盖层消失）', () => {
    renderMock.mockImplementationOnce(() => {
      throw new TypeError('bad config')
    })
    const { rerender, container } = render(<VisChart chart={badChart} />)
    expect(screen.getByText('图表配置无效，无法渲染')).toBeInTheDocument()

    rerender(<VisChart chart={goodChart} />)
    expect(renderMock).toHaveBeenCalledTimes(2)
    expect(screen.queryByText('图表配置无效，无法渲染')).toBeNull()
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
  })

  it('白名单外的 type（防御）：不触碰渲染工厂，直接覆盖层提示', () => {
    render(<VisChart chart={{ type: 'unknown-kind', data: [] }} />)
    expect(factoryMock).not.toHaveBeenCalled()
    expect(screen.getByText('图表配置无效，无法渲染')).toBeInTheDocument()
  })

  it('卸载时 destroy 释放实例', () => {
    const { unmount } = render(<VisChart chart={goodChart} />)
    unmount()
    expect(destroyMock).toHaveBeenCalled()
  })
})
