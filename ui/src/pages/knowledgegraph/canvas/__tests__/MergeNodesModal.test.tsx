import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { MergeNodesModal } from '../MergeNodesModal'
import { mergeKnowledgeGraphNodes } from '@/api/knowledgeGraph'

vi.mock('@/api/knowledgeGraph', () => ({
  mergeKnowledgeGraphNodes: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

const nodes = [
  { id: 'a', label: '实体甲', typeKey: '1', degree: 2, description: '甲描述' },
  { id: 'b', label: '实体乙', typeKey: '1', degree: 5, description: '乙描述' },
  { id: 'c', label: '实体丙', typeKey: '1' },
]

describe('MergeNodesModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('默认保留目标为度数最高者，展示度数与描述差异', async () => {
    render(
      <MemoryRouter>
        <MergeNodesModal open graphId={9} nodes={nodes} typeNameOf={() => '人员'} onCancel={() => {}} onMerged={() => {}} />
      </MemoryRouter>,
    )
    // 名称与度数渲染在同一行（label + （N 条关系）），用子串匹配
    expect(await screen.findByText(/实体甲/)).toBeInTheDocument()
    expect(screen.getByText(/实体乙/)).toBeInTheDocument()
    expect(screen.getByText(/2 条关系/)).toBeInTheDocument()
    expect(screen.getByText(/5 条关系/)).toBeInTheDocument()
    expect(screen.getByText(/乙描述/)).toBeInTheDocument()
    // 度数最高的实体乙为默认目标（radio 选中）
    expect(document.querySelector('input[value="b"]')).toBeChecked()
  })

  it('Popconfirm 二次确认后以 confirm=true 调用合并并回调刷新', async () => {
    vi.mocked(mergeKnowledgeGraphNodes).mockResolvedValue({ rehungEdges: 2, droppedSelfLoopEdges: 0, removedNodes: 2 })
    const onMerged = vi.fn()
    render(
      <MemoryRouter>
        <MergeNodesModal open graphId={9} nodes={nodes} typeNameOf={() => '人员'} onCancel={() => {}} onMerged={onMerged} />
      </MemoryRouter>,
    )
    // 点「合并选中实体」弹 Popconfirm（不可逆确认）
    fireEvent.click(await screen.findByRole('button', { name: /合并选中实体/ }))
    expect(await screen.findByText('确认合并？')).toBeInTheDocument()
    // Popconfirm 确认按钮（okText=合并，位于气泡内）
    const popover = document.querySelector('.ant-popover')
    expect(popover).not.toBeNull()
    const okButton = popover!.querySelector('.ant-btn-primary') as HTMLElement
    fireEvent.click(okButton)

    await waitFor(() => expect(mergeKnowledgeGraphNodes).toHaveBeenCalledWith(9, {
      targetNodeId: 'b',
      sourceNodeIds: ['a', 'c'],
      confirm: true,
    }))
    await waitFor(() => expect(onMerged).toHaveBeenCalledWith({ rehungEdges: 2, removedNodes: 2 }))
  })

  it('候选不足两个时不渲染提交可用态', () => {
    render(
      <MemoryRouter>
        <MergeNodesModal open graphId={9} nodes={[nodes[0]]} typeNameOf={() => '人员'} onCancel={() => {}} onMerged={() => {}} />
      </MemoryRouter>,
    )
    const submit = screen.getByRole('button', { name: /合并选中实体/ })
    expect(submit).toBeDisabled()
  })
})
