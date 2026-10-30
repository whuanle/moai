import '@/i18n'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ModelAccessDrawer } from '../ModelAccessDrawer'
import { aichannelApi } from '@/api/aichannel'
import { getAllTeams } from '@/api/team'
import type { AIModelItem } from '@/api/aichannel'

vi.mock('@/api/aichannel', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/aichannel')>()
  return {
    ...actual,
    aichannelApi: {
      ...actual.aichannelApi,
      getModelAuthorization: vi.fn().mockResolvedValue(null),
      updateModelVisibility: vi.fn().mockResolvedValue(undefined),
      updateModelAuthorization: vi.fn().mockResolvedValue(undefined),
    },
  }
})

vi.mock('@/api/team', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/team')>()
  return {
    ...actual,
    getAllTeams: vi.fn().mockResolvedValue([
      { teamId: '7', name: 'Alpha 团队', isDisable: false, memberCount: 3 },
      { teamId: '8', name: 'Beta 团队', isDisable: false, memberCount: 2 },
      { teamId: '9', name: 'Gamma 团队', isDisable: false, memberCount: 1 },
    ]),
  }
})

vi.mock('@/api/kiota', () => ({
  getApiClient: vi.fn(() => ({})),
}))

function renderDrawer(model: AIModelItem) {
  const onVisibilityChanged = vi.fn()
  render(
    <ModelAccessDrawer
      model={model}
      open
      onClose={vi.fn()}
      onVisibilityChanged={onVisibilityChanged}
    />,
  )
  return { onVisibilityChanged }
}

const privateModel = { id: 'm-1', name: 'gpt-5', isPublic: false } as AIModelItem

const privateAuthorization = {
  modelId: 'm-1',
  isPublic: false,
  items: [{ teamId: 7, teamName: 'Alpha 团队' }, { teamId: 8, teamName: 'Beta 团队' }],
}

describe('ModelAccessDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('私有模型渲染授权团队列表', async () => {
    vi.mocked(aichannelApi.getModelAuthorization).mockResolvedValue(privateAuthorization)
    renderDrawer(privateModel)

    expect(await screen.findByText('Alpha 团队')).toBeInTheDocument()
    expect(screen.getByText('Beta 团队')).toBeInTheDocument()
    expect(getAllTeams).toHaveBeenCalled()
  })

  it('公开模型提示所有团队可用且不渲染授权列表', async () => {
    vi.mocked(aichannelApi.getModelAuthorization).mockResolvedValue({
      modelId: 'm-1',
      isPublic: true,
      items: [],
    })
    renderDrawer({ id: 'm-1', name: 'gpt-5', isPublic: true } as AIModelItem)

    expect(await screen.findByText('公开模型对所有团队可用，无需配置授权团队。')).toBeInTheDocument()
    expect(screen.queryByText('Alpha 团队')).not.toBeInTheDocument()
  })

  it('添加团队授权提交全量团队 id 集合', async () => {
    vi.mocked(aichannelApi.getModelAuthorization).mockResolvedValue(privateAuthorization)
    renderDrawer(privateModel)

    // 打开 antd 多选下拉并等待候选团队选项渲染（表格行中存在同名文本，用 role 区分）
    const selector = await waitFor(() => {
      const el = document.querySelector('.ant-select-selector')
      expect(el).toBeTruthy()
      return el as Element
    })
    fireEvent.mouseDown(selector)
    // 可点击的是 .ant-select-item-option（带 title 属性），而非 aria 隐藏列表里的同名 option
    fireEvent.click(await screen.findByTitle('Gamma 团队'))

    fireEvent.click(screen.getByRole('button', { name: /添加团队授权/ }))
    await waitFor(() => {
      expect(aichannelApi.updateModelAuthorization).toHaveBeenCalledWith('m-1', [7, 8, 9])
    })
  })

  it('切换可见性调用专用接口并通知父级刷新', async () => {
    vi.mocked(aichannelApi.getModelAuthorization).mockResolvedValue(privateAuthorization)
    const { onVisibilityChanged } = renderDrawer(privateModel)

    const switches = await screen.findAllByRole('switch')
    // 唯一的开关是可见性开关
    fireEvent.click(switches[0])

    await waitFor(() => {
      expect(aichannelApi.updateModelVisibility).toHaveBeenCalledWith('m-1', true)
    })
    await waitFor(() => {
      expect(onVisibilityChanged).toHaveBeenCalled()
    })
  })

  it('移除团队授权提交去除目标团队后的全量集合', async () => {
    vi.mocked(aichannelApi.getModelAuthorization).mockResolvedValue(privateAuthorization)
    renderDrawer(privateModel)

    const removeButtons = await screen.findAllByRole('button', { name: '移除授权' })
    fireEvent.click(removeButtons[0])
    fireEvent.click(await screen.findByRole('button', { name: 'OK' }))

    await waitFor(() => {
      expect(aichannelApi.updateModelAuthorization).toHaveBeenCalledWith('m-1', [8])
    })
  })
})
