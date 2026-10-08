import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import '@/i18n'
import { UiToolCard } from '../chat/UiToolCard'

describe('UiToolCard（前端展示工具折叠卡）', () => {
  it('参数流未结束（running 无参数）：展示生成中且禁用点击', () => {
    render(<UiToolCard toolCall={{ id: 'tc-1', name: 'ui_show_document', status: 'running' }} />)
    expect(screen.getByText('文档')).toBeInTheDocument()
    expect(screen.getByText('生成中')).toBeInTheDocument()
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('内容就绪：展示类型标签与标题，点击回调 onOpen', () => {
    const onOpen = vi.fn()
    render(
      <UiToolCard
        toolCall={{
          id: 'tc-2',
          name: 'ui_show_chart',
          argsJson: '{"title":"销量分析","option":{"series":[]}}',
          status: 'done',
        }}
        onOpen={onOpen}
      />,
    )
    expect(screen.getByText('图表')).toBeInTheDocument()
    expect(screen.getByText('销量分析')).toBeInTheDocument()
    expect(screen.getByText('查看')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button'))
    expect(onOpen).toHaveBeenCalledTimes(1)
  })

  it('参数非法（done 但无内容）：展示内容缺失且禁用点击', () => {
    render(<UiToolCard toolCall={{ id: 'tc-3', name: 'ui_show_code', argsJson: '{broken', status: 'done' }} />)
    expect(screen.getByText('内容缺失')).toBeInTheDocument()
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('激活态（面板正展示该项）高亮卡片', () => {
    const { container } = render(
      <UiToolCard
        toolCall={{ id: 'tc-4', name: 'ui_show_code', argsJson: '{"title":"脚本","code":"print(1)"}', status: 'done' }}
        active
      />,
    )
    expect(container.querySelector('.moai-chat__ui-tool.is-active')).not.toBeNull()
  })

  it('无标题时回退类型标签为标题', () => {
    render(
      <UiToolCard
        toolCall={{ id: 'tc-5', name: 'ui_show_code', argsJson: '{"code":"print(1)"}', status: 'done' }}
      />,
    )
    expect(screen.getAllByText('代码').length).toBeGreaterThanOrEqual(2)
  })
})
