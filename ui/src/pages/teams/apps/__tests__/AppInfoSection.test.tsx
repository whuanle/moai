import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppInfoSection } from '../AppInfoSection'
import type { AppDetail } from '../AppConfigSection'
import { updateApp } from '@/api/app'

vi.mock('@/api/classify', () => ({
  classifyApi: { getClassifies: vi.fn().mockResolvedValue([]) },
  classifyLabel: (c: { emoji?: string | null; name?: string | null }) => [c?.emoji, c?.name].filter(Boolean).join(' '),
  ClassifyType: { Plugin: 'plugin', App: 'app', Kb: 'kb', Prompt: 'prompt', Skill: 'skill' },
}))
vi.mock('@/api/app', () => ({
  updateApp: vi.fn().mockResolvedValue(undefined),
  uploadAppAvatar: vi.fn().mockResolvedValue(''),
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
  })

  it('修改名称后保存：提交 updateApp 并回调刷新', async () => {
    const onReload = vi.fn()
    render(
      <AppInfoSection
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
        classifyId: 0,
        version: '1.0.0',
      })
      expect(onReload).toHaveBeenCalled()
    })
  })

  it('版本号回显与编辑：按 aaa.bb.cc 展示，保存随基础信息提交', async () => {
    render(
      <AppInfoSection
        appId="a1"
        detail={{ ...DETAIL, version: 10203 }}
        canManage
        onReload={vi.fn()}
      />,
    )

    const versionInput = (await screen.findByLabelText('应用版本')) as HTMLInputElement
    expect(versionInput.value).toBe('1.2.3')

    fireEvent.change(versionInput, { target: { value: '2.10.1' } })
    fireEvent.click(screen.getByRole('button', { name: /保存信息/ }))

    await waitFor(() => {
      expect(updateApp).toHaveBeenCalledWith('a1', expect.objectContaining({ version: '2.10.1' }))
    })
  })

  it('版本号格式非法时阻止保存', async () => {
    render(
      <AppInfoSection
        appId="a1"
        detail={{ ...DETAIL, version: 10000 }}
        canManage
        onReload={vi.fn()}
      />,
    )

    const versionInput = (await screen.findByLabelText('应用版本')) as HTMLInputElement
    fireEvent.change(versionInput, { target: { value: '1.2' } })
    fireEvent.click(screen.getByRole('button', { name: /保存信息/ }))

    await waitFor(() => {
      expect(screen.getByText(/版本号格式不正确/)).toBeTruthy()
    })
    expect(updateApp).not.toHaveBeenCalled()
  })

  it('普通成员只读：无保存入口并提示需要团队管理员', async () => {
    renderSection({ ...DETAIL, myRole: 0 }, false)

    expect(await screen.findByText('应用信息')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /保存信息/ })).toBeNull()
    expect(screen.getByText(/创建与配置需要团队管理员/)).toBeTruthy()
  })

  it('内部应用不再展示上架状态（已拆分至上架管理分区）', async () => {
    renderSection()

    await screen.findByLabelText('应用名称')
    expect(screen.queryByText('上架状态')).toBeNull()
    expect(screen.queryByRole('button', { name: /申请上架/ })).toBeNull()
  })

  it('外部应用展示访问授权开关', async () => {
    renderSection({ ...DETAIL, isExternal: true })

    expect(await screen.findByText('需要授权访问')).toBeTruthy()
  })
})
