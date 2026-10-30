import '@/i18n'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ChannelAccessModal } from '../ChannelAccessModal'
import { aichannelApi } from '@/api/aichannel'
import { getAllTeams } from '@/api/team'
import type { AIChannelItem } from '@/api/aichannel'

vi.mock('@/api/aichannel', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/aichannel')>()
  return {
    ...actual,
    aichannelApi: {
      ...actual.aichannelApi,
      getChannelAuthorization: vi.fn().mockResolvedValue(null),
      updateChannelAuthorization: vi.fn().mockResolvedValue(undefined),
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

const channel = { id: 'c-1', name: 'LMStudio' } as AIChannelItem

function renderModal() {
  const onSaved = vi.fn()
  render(
    <ChannelAccessModal
      channel={channel}
      open
      onClose={vi.fn()}
      onSaved={onSaved}
    />,
  )
  return { onSaved }
}

async function openSelectAndPick(optionTitle: string) {
  fireEvent.mouseDown(await waitFor(() => document.querySelector('.ant-select-selector') as Element))
  fireEvent.click(await screen.findByTitle(optionTitle))
  // 多选下拉选中后不自动关闭；下拉打开时弹窗 footer 被 aria-hidden 隔离，
  // 再次 mouseDown 触发开合切换将其关闭，后续才能操作 footer 按钮
  fireEvent.mouseDown(document.querySelector('.ant-select-selector') as Element)
  await waitFor(() => {
    // 关闭信号：hidden 常驻类或 leave 动画类（jsdom 下动画以类切换模拟）
    const cls = document.querySelector('.ant-select-dropdown')?.className ?? ''
    expect(cls.includes('ant-select-dropdown-hidden') || cls.includes('ant-slide-up-leave')).toBe(true)
  })
}

describe('ChannelAccessModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('打开时选择框为空，授权/撤销按钮均禁用，不再调用授权并集查询', async () => {
    renderModal()

    await waitFor(() => {
      expect(getAllTeams).toHaveBeenCalled()
    })
    expect(screen.queryByText('Alpha 团队')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: '授 权' })).toBeDisabled()
    expect(screen.getByRole('button', { name: '撤 销' })).toBeDisabled()
    expect(aichannelApi.getChannelAuthorization).not.toHaveBeenCalled()
  })

  it('勾选团队点「授权」= 仅提交 grantTeamIds，成功后清空勾选并通知刷新', async () => {
    const { onSaved } = renderModal()

    await openSelectAndPick('Beta 团队')
    fireEvent.click(screen.getByRole('button', { name: '授 权' }))

    await waitFor(() => {
      expect(aichannelApi.updateChannelAuthorization).toHaveBeenCalledWith('c-1', {
        grantTeamIds: [8],
        revokeTeamIds: [],
      })
    })
    await waitFor(() => {
      expect(onSaved).toHaveBeenCalled()
    })
    // 操作后勾选清空，可继续下一批。
    // 注意：不能用 accessible name 查询——antd loading 图标离场动画在 jsdom 中滞后，
    // 会让按钮 accessible name 变成 "loading 授 权"；此处按文本内容定位按钮断言禁用态
    await waitFor(() => {
      const grantBtn = screen.getAllByRole('button').find((b) => b.textContent === '授 权')
      expect(grantBtn).toBeDefined()
      expect(grantBtn as HTMLButtonElement).toBeDisabled()
    })
  })

  it('勾选团队点「撤销」= 仅提交 revokeTeamIds', async () => {
    renderModal()

    await openSelectAndPick('Alpha 团队')
    await openSelectAndPick('Gamma 团队')
    fireEvent.click(screen.getByRole('button', { name: '撤 销' }))

    await waitFor(() => {
      expect(aichannelApi.updateChannelAuthorization).toHaveBeenCalledWith('c-1', {
        grantTeamIds: [],
        revokeTeamIds: [7, 9],
      })
    })
  })
})
