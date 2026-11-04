import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import '@/i18n'
import { OrgRolesPanel } from '../OrgRolesPanel'
import {
  createOrgRole,
  deleteOrgRole,
  getOrgRoleUsers,
  getOrgRoles,
  getUserOrgRoles,
  updateOrgRole,
  updateUserOrgRoles,
} from '@/api/org'

vi.mock('@/api/org', () => ({
  getOrgRoles: vi.fn(),
  createOrgRole: vi.fn().mockResolvedValue({ orgRoleId: 3, path: '/3/' }),
  updateOrgRole: vi.fn().mockResolvedValue(undefined),
  deleteOrgRole: vi.fn().mockResolvedValue(undefined),
  getOrgRoleUsers: vi.fn().mockResolvedValue([]),
  getUserOrgRoles: vi.fn().mockResolvedValue({ userId: 100, userName: 'zhangsan', nickName: '张三', items: [] }),
  updateUserOrgRoles: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/usermanage', () => ({
  getUsers: vi.fn().mockResolvedValue({
    totalCount: 1,
    items: [{ id: 100, userName: 'zhangsan', nickName: '张三' }],
  }),
}))

function renderPanel() {
  return render(<OrgRolesPanel />)
}

/** 点击左侧树节点选中组织，并等待右栏头部出现其物化路径 */
async function selectRole(name: string, path: string) {
  fireEvent.click(await screen.findByText(name))
  await waitFor(() => expect(screen.getByText(path)).toBeTruthy())
}

/** 在指定树节点内按 aria-label 找按钮 */
function nodeButton(nodeName: string, label: string): HTMLElement {
  const btn = Array.from(document.querySelectorAll(`button[aria-label="${label}"]`)).find((b) =>
    b.closest('.ant-tree-treenode')?.textContent?.includes(nodeName),
  )
  expect(btn, `node ${nodeName} button ${label} should exist`).toBeTruthy()
  return btn as HTMLElement
}

describe('OrgRolesPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getOrgRoles).mockResolvedValue([
      { orgRoleId: 1, name: '集团总部', parentId: null, path: '/1/', userCount: 0, hasChildren: true },
      { orgRoleId: 2, name: '销售部', parentId: 1, path: '/1/2/', userCount: 2, hasChildren: false },
    ])
    vi.mocked(getOrgRoleUsers).mockResolvedValue([
      {
        userId: '100',
        userName: 'zhangsan',
        nickName: '张三',
        email: 'zhangsan@test.local',
        phone: '13800000000',
        isAdmin: false,
        isRoot: false,
        isDisable: false,
        createTime: '2026-10-21T10:00:00Z',
      },
    ])
    vi.mocked(getUserOrgRoles).mockResolvedValue({
      userId: 100,
      userName: 'zhangsan',
      nickName: '张三',
      items: [{ orgRoleId: 2, name: '销售部', path: '/1/2/' }],
    })
  })

  it('左侧树顶部固定默认根，组织挂其下且带成员数', async () => {
    renderPanel()
    expect(await screen.findByText('默认根')).toBeTruthy()
    expect(await screen.findByText('集团总部')).toBeTruthy()
    expect(screen.getByText('销售部')).toBeTruthy()
  })

  it('点击组织后右栏按用户管理样式加载成员列表', async () => {
    renderPanel()
    await selectRole('销售部', '/1/2/')
    expect(await screen.findByText('张三')).toBeTruthy()
    expect(screen.getByText('zhangsan@test.local')).toBeTruthy()
    expect(screen.getByText('13800000000')).toBeTruthy()
    expect(screen.getByText('普通用户')).toBeTruthy()
    expect(screen.getByText('正常')).toBeTruthy()
    await waitFor(() => expect(getOrgRoleUsers).toHaveBeenCalledWith(2))
  })

  it('未选中组织时右栏为空态提示', async () => {
    renderPanel()
    expect(await screen.findByText('集团总部')).toBeTruthy()
    expect(screen.getByText('在左侧选择一个组织查看成员')).toBeTruthy()
  })

  it('默认根上的新建按钮创建顶级组织（仅填名称，上级为空）', async () => {
    renderPanel()
    fireEvent.click(nodeButton('默认根', '新建顶级组织'))
    fireEvent.change(await screen.findByPlaceholderText('请输入组织名称'), { target: { value: '新事业部' } })
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))
    await waitFor(() => expect(createOrgRole).toHaveBeenCalledWith({ name: '新事业部', parentId: null }))
  })

  it('组织节点上的新建按钮在其下创建下级组织', async () => {
    renderPanel()
    await screen.findByText('集团总部')
    fireEvent.click(nodeButton('集团总部', '新建下级组织'))
    fireEvent.change(await screen.findByPlaceholderText('请输入组织名称'), { target: { value: '子公司' } })
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))
    await waitFor(() => expect(createOrgRole).toHaveBeenCalledWith({ name: '子公司', parentId: 1 }))
  })

  it('每个组织节点行内带新建下级/编辑/删除三个图标按钮', async () => {
    renderPanel()
    await screen.findByText('销售部')
    expect(nodeButton('销售部', '新建下级组织')).toBeTruthy()
    expect(nodeButton('销售部', '编辑')).toBeTruthy()
    expect(nodeButton('销售部', '删除')).toBeTruthy()
  })

  it('编辑组织仅改名（层级保持不变）', async () => {
    renderPanel()
    await screen.findByText('销售部')
    fireEvent.click(nodeButton('销售部', '编辑'))
    const input = await screen.findByPlaceholderText('请输入组织名称')
    expect((input as HTMLInputElement).value).toBe('销售部')
    fireEvent.change(input, { target: { value: '市场部' } })
    fireEvent.click(screen.getByRole('button', { name: /保\s*存/ }))
    await waitFor(() => expect(updateOrgRole).toHaveBeenCalledWith(2, { name: '市场部', parentId: 1 }))
  })

  it('删除组织需 Popconfirm 二次确认（无需先选中组织）', async () => {
    renderPanel()
    await screen.findByText('销售部')
    fireEvent.click(nodeButton('销售部', '删除'))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(deleteOrgRole).toHaveBeenCalledWith(2))
  })

  it('成员查询按钮按用户名/昵称前端筛选当前组织成员', async () => {
    renderPanel()
    await selectRole('销售部', '/1/2/')
    expect(await screen.findByText('张三')).toBeTruthy()
    // 输入不匹配关键字并点击查询：列表被前端过滤，不触发后端请求（页面有两个「查询」——左栏组织树搜索在先、成员筛选在后）
    fireEvent.change(screen.getByPlaceholderText('搜索用户名/昵称'), { target: { value: 'lisi' } })
    const queryButtons = screen.getAllByRole('button', { name: /查\s*询/ })
    fireEvent.click(queryButtons[queryButtons.length - 1])
    await waitFor(() => expect(screen.queryByText('张三')).toBeNull())
    expect(getOrgRoleUsers).toHaveBeenCalledTimes(1)
    // 清空输入后恢复全量
    fireEvent.change(screen.getByPlaceholderText('搜索用户名/昵称'), { target: { value: '' } })
    await waitFor(() => expect(screen.getByText('张三')).toBeTruthy())
  })

  it('成员移除按整体覆盖该用户组织集合提交', async () => {
    renderPanel()
    await selectRole('销售部', '/1/2/')
    expect(await screen.findByText('张三')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: '移除' }))
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定|OK|确\s*认/ }))
    await waitFor(() => expect(updateUserOrgRoles).toHaveBeenCalledWith(100, []))
  })
})
