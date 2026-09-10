import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Settings } from '../Settings'
import { useAppStore } from '@/store/app'
import { getSettings, saveSetting } from '@/api/settings'

vi.mock('@/api/settings', () => ({
  SettingKeys: {
    neo4jEnabled: 'OPEN_NEO4J',
    neo4jUri: 'NEO4J_URI',
    neo4jUsername: 'NEO4J_USERNAME',
    neo4jPassword: 'NEO4J_PASSWORD',
  },
  getSettings: vi.fn(),
  saveSetting: vi.fn().mockResolvedValue(undefined),
}))

function settingsResponse(overrides: Record<string, string> = {}) {
  const values: Record<string, string> = {
    OPEN_NEO4J: 'false',
    NEO4J_URI: '',
    NEO4J_USERNAME: '',
    NEO4J_PASSWORD: '',
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

    expect(await screen.findByText('知识图谱（Neo4j）')).toBeInTheDocument()
    expect(screen.queryByLabelText('连接地址')).toBeNull()
    expect(screen.queryByLabelText('用户名')).toBeNull()
    expect(screen.queryByLabelText('密码')).toBeNull()
  })

  it('开启后填写连接信息，保存时提交开关与三项连接设置', async () => {
    renderSettings()
    await screen.findByText('知识图谱（Neo4j）')

    fireEvent.click(screen.getAllByRole('switch')[0])

    fireEvent.change(await screen.findByLabelText('连接地址'), {
      target: { value: 'neo4j://127.0.0.1:7687' },
    })
    fireEvent.change(screen.getByLabelText('用户名'), { target: { value: 'neo4j' } })
    fireEvent.change(screen.getByLabelText('密码'), { target: { value: 'secret' } })

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('OPEN_NEO4J', 'true'))
    expect(saveSetting).toHaveBeenCalledWith('NEO4J_URI', 'neo4j://127.0.0.1:7687')
    expect(saveSetting).toHaveBeenCalledWith('NEO4J_USERNAME', 'neo4j')
    expect(saveSetting).toHaveBeenCalledWith('NEO4J_PASSWORD', 'secret')
  })

  it('关闭知识图谱时保存不提交连接信息', async () => {
    renderSettings()
    await screen.findByText('知识图谱（Neo4j）')

    const graphSwitch = screen.getAllByRole('switch')[0]
    fireEvent.click(graphSwitch)
    fireEvent.click(graphSwitch)

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('OPEN_NEO4J', 'false'))
    expect(saveSetting).not.toHaveBeenCalledWith('NEO4J_URI', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('NEO4J_USERNAME', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('NEO4J_PASSWORD', expect.anything())
  })

  it('非 root 管理员被重定向，不渲染知识图谱设置', async () => {
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'admin2', isAdmin: true, isRoot: false },
    })

    renderSettings()

    await waitFor(() => expect(screen.queryByText('知识图谱（Neo4j）')).toBeNull())
    expect(screen.queryByLabelText('连接地址')).toBeNull()
  })
})
