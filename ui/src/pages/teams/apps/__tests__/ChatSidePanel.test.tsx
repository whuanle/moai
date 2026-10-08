import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { ChatSidePanel } from '../chat/ChatSidePanel'
import type { UiPanelItem } from '../chat/uiTools'

vi.mock('../chat/EChart', () => ({
  EChart: (props: { option: unknown }) => <div data-testid="echart-mock">{JSON.stringify(props.option)}</div>,
}))

const docItem: UiPanelItem = { key: 'tc-1', kind: 'document', title: '季度报告', content: '# 摘要\n正文段落' }
const codeItem: UiPanelItem = { key: 'tc-2', kind: 'code', title: '清理脚本', language: 'python', code: 'print("hi")' }
const chartItem: UiPanelItem = {
  key: 'tc-3',
  kind: 'chart',
  title: '销量分析',
  option: { series: [{ type: 'bar', data: [1, 2] }] },
}

function renderPanel(
  props: Partial<Parameters<typeof ChatSidePanel>[0]> = {},
  items: UiPanelItem[] = [docItem],
) {
  const base = {
    items,
    activeKey: items[items.length - 1]?.key ?? null,
    onSelect: vi.fn(),
    onCloseTab: vi.fn(),
    onClose: vi.fn(),
  }
  return render(<ChatSidePanel {...base} {...props} />)
}

/** 在指定标题的标签里点关闭（每个标签有独立关闭按钮） */
function closeTabByTitle(title: string) {
  const tab = screen.getByText(title).closest('.moai-chat__panel-tab') as HTMLElement
  expect(tab).not.toBeNull()
  fireEvent.click(tab.querySelector('.moai-chat__panel-tab-close') as HTMLElement)
}

describe('ChatSidePanel（前端展示工具侧边栏：多标签 + 可调宽）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.defineProperty(window.navigator, 'clipboard', {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      configurable: true,
    })
    localStorage.clear()
  })

  it('无内容项时不渲染任何内容', () => {
    const { container } = renderPanel({ items: [], activeKey: null })
    expect(container.querySelector('.moai-chat__panel')).toBeNull()
  })

  it('单文档项：标签展示标题，正文渲染 markdown，复制写入剪贴板', async () => {
    renderPanel()
    expect(screen.getByText('季度报告')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: '摘要' })).toBeInTheDocument()
    expect(screen.getByText('正文段落')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: '复制内容' }))
    await expect(window.navigator.clipboard.writeText).toHaveBeenCalledWith('# 摘要\n正文段落')
  })

  it('多标签：点击标签回调 onSelect(key)，关闭按钮回调 onCloseTab(key)', () => {
    const onSelect = vi.fn()
    const onCloseTab = vi.fn()
    renderPanel({ onSelect, onCloseTab }, [docItem, codeItem])
    // activeKey 指向最后一项（代码），正文展示代码而非文档
    expect(screen.getByText(/print\("hi"\)/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: '摘要' })).not.toBeInTheDocument()

    // 点击标签只回调切换（激活态由父级驱动）
    fireEvent.click(screen.getByText('季度报告'))
    expect(onSelect).toHaveBeenCalledWith('tc-1')

    // 标签有独立关闭按钮
    closeTabByTitle('清理脚本')
    expect(onCloseTab).toHaveBeenCalledWith('tc-2')
  })

  it('代码项：展示语言标签与代码文本', () => {
    renderPanel({}, [codeItem])
    expect(screen.getByText('python')).toBeInTheDocument()
    expect(screen.getByText(/print\("hi"\)/)).toBeInTheDocument()
  })

  it('图表项：透传 ECharts option 给渲染组件', () => {
    renderPanel({}, [chartItem])
    const chart = screen.getByTestId('echart-mock')
    expect(chart.textContent).toContain('"type":"bar"')
  })

  it('无标题时回退类型标签作为标签标题', () => {
    renderPanel({}, [{ key: 'tc-5', kind: 'chart', title: '', option: {} }])
    expect(screen.getAllByText('图表').length).toBeGreaterThanOrEqual(1)
  })

  it('点击收起按钮回调 onClose（标签保留由父级管理）', () => {
    const onClose = vi.fn()
    renderPanel({ onClose })
    fireEvent.click(screen.getByRole('button', { name: '关闭' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('左缘存在拖宽手柄（separator）', () => {
    const { container } = renderPanel()
    expect(screen.getByRole('separator')).toBeInTheDocument()
    expect(container.querySelector('.moai-chat__panel-resize')).not.toBeNull()
  })
})
