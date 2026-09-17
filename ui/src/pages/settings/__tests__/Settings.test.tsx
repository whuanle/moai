import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Settings } from '../Settings'
import { useAppStore } from '@/store/app'
import { getSettings, saveSetting } from '@/api/settings'

vi.mock('@/api/settings', () => ({
  SettingKeys: {
    kgEnabled: 'KG_ENABLED',
    kgUri: 'KG_URI',
    kgUsername: 'KG_USERNAME',
    kgPassword: 'KG_PASSWORD',
    kgDialect: 'KG_DIALECT',
    wikiMaxFileSize: 'WIKI_MAX_FILE_SIZE_MB',
  },
  getSettings: vi.fn(),
  saveSetting: vi.fn().mockResolvedValue(undefined),
}))

function settingsResponse(overrides: Record<string, string> = {}) {
  const values: Record<string, string> = {
    KG_ENABLED: 'false',
    KG_URI: '',
    KG_USERNAME: '',
    KG_PASSWORD: '',
    KG_DIALECT: 'memgraph',
    WIKI_MAX_FILE_SIZE_MB: '50',
    ...overrides,
  }
  return {
    items: Object.entries(values).map(([key, value]) => ({ key, name: key, description: '', value })),
  } as never
}

function renderSettings() {
  return render(
    <MemoryRouter>
      <Settings />
    </MemoryRouter>,
  )
}

describe('Settings（系统设置）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getSettings).mockResolvedValue(settingsResponse())
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '1', userName: 'admin', isAdmin: true, isRoot: true },
    })
  })

  it('root 可见知识图谱开关，未开启时不显示连接字段', async () => {
    renderSettings()

    expect(await screen.findByText('知识图谱（图数据库）')).toBeInTheDocument()
    expect(screen.queryByLabelText('连接地址')).toBeNull()
    expect(screen.queryByLabelText('用户名')).toBeNull()
    expect(screen.queryByLabelText('密码')).toBeNull()
  })

  it('开启后填写连接信息，保存时提交开关、图数据库类型与三项连接设置', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getAllByRole('switch')[0])

    fireEvent.change(await screen.findByLabelText('连接地址'), {
      target: { value: 'bolt://127.0.0.1:7687' },
    })
    fireEvent.change(screen.getByLabelText('用户名'), { target: { value: 'neo4j' } })
    fireEvent.change(screen.getByLabelText('密码'), { target: { value: 'secret' } })

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('KG_ENABLED', 'true'))
    expect(saveSetting).toHaveBeenCalledWith('KG_URI', 'bolt://127.0.0.1:7687')
    expect(saveSetting).toHaveBeenCalledWith('KG_USERNAME', 'neo4j')
    expect(saveSetting).toHaveBeenCalledWith('KG_PASSWORD', 'secret')
    expect(saveSetting).toHaveBeenCalledWith('KG_DIALECT', 'memgraph')
  })

  it('关闭知识图谱时保存不提交连接信息', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    const graphSwitch = screen.getAllByRole('switch')[0]
    fireEvent.click(graphSwitch)
    fireEvent.click(graphSwitch)

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('KG_ENABLED', 'false'))
    expect(saveSetting).not.toHaveBeenCalledWith('KG_URI', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('KG_USERNAME', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('KG_PASSWORD', expect.anything())
  })

  it('知识图谱卡片默认展开，点击标题可折叠与再展开', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getAllByRole('switch')[0])
    expect(await screen.findByLabelText('连接地址')).toBeInTheDocument()

    const header = screen.getByRole('button', { name: /知识图谱（图数据库）/ })
    expect(header).toHaveAttribute('aria-expanded', 'true')

    fireEvent.click(header)
    expect(header).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByLabelText('连接地址')).toBeNull()

    fireEvent.click(header)
    expect(header).toHaveAttribute('aria-expanded', 'true')
    expect(await screen.findByLabelText('连接地址')).toBeInTheDocument()
  })

  it('知识库卡片默认折叠，默认回显 50，保存时提交 WIKI_MAX_FILE_SIZE_MB', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    const wikiHeader = screen.getByRole('button', { name: /知识库/ })
    expect(wikiHeader).toHaveAttribute('aria-expanded', 'false')

    fireEvent.click(wikiHeader)
    const input = await screen.findByLabelText('最大文件大小')
    expect(input).toHaveValue('50')
    fireEvent.change(input, { target: { value: '80' } })

    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    expect(saveButton).toBeTruthy()
    fireEvent.click(saveButton!)

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('WIKI_MAX_FILE_SIZE_MB', '80'))
  })

  it('非 root 管理员被重定向，不渲染知识图谱设置', async () => {
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'admin2', isAdmin: true, isRoot: false },
    })

    renderSettings()

    await waitFor(() => expect(screen.queryByText('知识图谱（图数据库）')).toBeNull())
    expect(screen.queryByLabelText('连接地址')).toBeNull()
  })
})
