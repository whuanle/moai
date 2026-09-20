import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppInfoSection } from '../AppInfoSection'
import type { AppDetail } from '../AppConfigSection'
import { updateApp } from '@/api/app'
import { applyPublication, getTeamPublicationList } from '@/api/publication'

vi.mock('@/api/app', () => ({
  updateApp: vi.fn().mockResolvedValue(undefined),
  uploadAppAvatar: vi.fn().mockResolvedValue(''),
}))

vi.mock('@/api/publication', () => ({
  applyPublication: vi.fn().mockResolvedValue('1'),
  getTeamPublicationList: vi.fn().mockResolvedValue([]),
  withdrawPublication: vi.fn().mockResolvedValue(undefined),
}))

const DETAIL: AppDetail = {
  appId: 'a1',
  teamId: '3',
  name: '客服助手',
  description: '售前售后问答',
  appType: 'agent',
  avatarPath: '',
  isPublic: false,
  myRole: 2,
}

function renderSection(detail: AppDetail = DETAIL, canManage = true) {
  return render(
    <AppInfoSection
      teamId={3}
      appId={detail.appId ?? 'a1'}
      detail={detail}
      canManage={canManage}
      onReload={vi.fn()}
    />,
  )
}

describe('AppInfoSection（应用信息分区）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('回显应用基础信息与类型标签', async () => {
    renderSection()

    expect(await waitFor(() => expect((screen.getByLabelText('应用名称') as HTMLInputElement).value).toBe('客服助手'))).toBeTruthy()
    expect((screen.getByLabelText('应用描述') as HTMLTextAreaElement).value).toBe('售前售后问答')
    expect(screen.getByText('Agent 应用')).toBeTruthy()
    expect(getTeamPublicationList).toHaveBeenCalledWith(3, expect.objectContaining({ resourceType: 'app' }))
  })

  it('未公开时管理员可申请上架：填写理由后提交', async () => {
    renderSection()

    const applyBtn = await screen.findByRole('button', { name: /申请上架/ })
    fireEvent.click(applyBtn)

    const reason = await screen.findByPlaceholderText(/上架理由/)
    fireEvent.change(reason, { target: { value: '对外提供客服问答' } })
    fireEvent.click(screen.getByRole('button', { name: /提交申请/ }))

    await waitFor(() => {
      expect(applyPublication).toHaveBeenCalledWith({
        resourceType: 'app',
        resourceId: 'a1',
        applyReason: '对外提供客服问答',
      })
    })
  })

  it('修改名称后保存：提交 updateApp 并回调刷新', async () => {
    const onReload = vi.fn()
    render(
      <AppInfoSection
        teamId={3}
        appId="a1"
        detail={DETAIL}
        canManage
        onReload={onReload}
      />,
    )

    const nameInput = (await screen.findByLabelText('应用名称')) as HTMLInputElement
    fireEvent.change(nameInput, { target: { value: '售前助手' } })
    fireEvent.click(screen.getByRole('button', { name: /保存信息/ }))

    await waitFor(() => {
      expect(updateApp).toHaveBeenCalledWith('a1', {
        name: '售前助手',
        description: '售前售后问答',
        isExternal: false,
        isAuth: false,
      })
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('普通成员只读：无保存入口并提示需要团队管理员', async () => {
    renderSection({ ...DETAIL, myRole: 0 }, false)

    expect(await screen.findByText('应用信息')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /保存信息/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })
})
