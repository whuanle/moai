import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import '@/i18n'
import { EChart } from '../chat/EChart'

const setOptionMock = vi.fn()
const disposeMock = vi.fn()
const initTargets: unknown[] = []
const initMock = vi.fn((el: HTMLElement | null) => {
  initTargets.push(el)
  return {
    setOption: setOptionMock,
    resize: vi.fn(),
    dispose: disposeMock,
  }
})

// 仅替换 init（真实 init 需要浏览器 canvas），use/类型保持原样
vi.mock('echarts/core', async (importOriginal) => {
  const actual = await importOriginal<typeof import('echarts/core')>()
  return { ...actual, init: (el: HTMLElement | null) => initMock(el) }
})

const goodOption = { series: [{ type: 'bar', data: [1, 2] }] }
const badOption = { xAxis: { type: 'category' } }

describe('EChart（图表渲染包装：实例与容器同生命周期）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('挂载时 init 一次并 setOption（textStyle 与 option 合并），容器常驻', () => {
    const { container } = render(<EChart option={goodOption} />)
    expect(initMock).toHaveBeenCalledTimes(1)
    expect(setOptionMock).toHaveBeenCalledTimes(1)
    const arg = setOptionMock.mock.calls[0][0] as Record<string, unknown>
    expect(arg.series).toEqual(goodOption.series)
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
    expect(screen.queryByText('图表配置无效，无法渲染')).toBeNull()
  })

  it('option 更新复用同一实例（不重复 init），setOption 整体替换', () => {
    const { rerender } = render(<EChart option={goodOption} />)
    rerender(<EChart option={{ series: [{ type: 'line', data: [3] }] }} />)
    expect(initMock).toHaveBeenCalledTimes(1)
    expect(setOptionMock).toHaveBeenCalledTimes(2)
  })

  it('setOption 抛异常：错误覆盖层出现，容器不卸载（实例不与容器脱钩）', () => {
    setOptionMock.mockImplementationOnce(() => {
      throw new TypeError('Cannot read properties of undefined')
    })
    const { container } = render(<EChart option={badOption} />)
    expect(screen.getByText('图表配置无效，无法渲染')).toBeInTheDocument()
    // 关键回归点：容器 div 必须仍在 DOM（曾因条件卸载容器导致换有效 option 后永远空白）
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
  })

  it('抛异常后换有效 option：同实例重试 setOption 并自动恢复（覆盖层消失）', () => {
    setOptionMock.mockImplementationOnce(() => {
      throw new TypeError('Cannot read properties of undefined')
    })
    const { rerender, container } = render(<EChart option={badOption} />)
    expect(screen.getByText('图表配置无效，无法渲染')).toBeInTheDocument()

    rerender(<EChart option={goodOption} />)
    expect(setOptionMock).toHaveBeenCalledTimes(2)
    expect(initMock).toHaveBeenCalledTimes(1)
    expect(screen.queryByText('图表配置无效，无法渲染')).toBeNull()
    expect(container.querySelector('.moai-chat__panel-chart')).not.toBeNull()
  })

  it('卸载时释放实例', () => {
    const { unmount } = render(<EChart option={goodOption} />)
    unmount()
    expect(disposeMock).toHaveBeenCalledTimes(1)
  })
})
