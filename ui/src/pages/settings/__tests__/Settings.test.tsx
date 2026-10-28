import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Settings } from '../Settings'
import { useAppStore } from '@/store/app'
import { getSettings, saveSetting } from '@/api/settings'
import { refreshServerInfo } from '@/api/auth'

vi.mock('@/api/settings', () => ({
  SettingKeys: {
    kgEnabled: 'KG_ENABLED',
    kgUri: 'KG_URI',
    kgUsername: 'KG_USERNAME',
    kgPassword: 'KG_PASSWORD',
    kgDialect: 'KG_DIALECT',
    wikiMaxFileSize: 'WIKI_MAX_FILE_SIZE_MB',
    sandboxMaxTtl: 'SANDBOX_MAX_TTL_SECONDS',
    sandboxMaxCpu: 'SANDBOX_MAX_CPU',
    sandboxMaxMemory: 'SANDBOX_MAX_MEMORY',
    systemLogo: 'SYSTEM_LOGO',
    systemName: 'SYSTEM_NAME',
  },
  SANDBOX_TTL_LIMITS: { min: 60, max: 604800 },
  SandboxLimitDefaults: { maxTtlSeconds: 86400, maxCpu: '4', maxMemory: '8Gi' },
  getSettings: vi.fn(),
  saveSetting: vi.fn().mockResolvedValue(undefined),
  updateSystemLogo: vi.fn().mockResolvedValue('public/images/logo.png'),
  resetSystemLogo: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@/api/auth', () => ({
  refreshServerInfo: vi.fn().mockResolvedValue({
    serviceUrl: 'http://127.0.0.1:5000',
    publicStoreUrl: '',
    rsaPublic: '',
    logoPath: '',
    name: 'MoAI',
  }),
}))

function settingsResponse(overrides: Record<string, string> = {}) {
  const values: Record<string, string> = {
    KG_ENABLED: 'false',
    KG_URI: '',
    KG_USERNAME: '',
    KG_PASSWORD: '',
    KG_DIALECT: 'memgraph',
    WIKI_MAX_FILE_SIZE_MB: '50',
    SANDBOX_MAX_TTL_SECONDS: '86400',
    SANDBOX_MAX_CPU: '4',
    SANDBOX_MAX_MEMORY: '8Gi',
    SYSTEM_LOGO: '',
    SYSTEM_NAME: '',
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

    // 保存按钮顺序：[0] 网站名称卡片，[1] 知识图谱卡片
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[1])

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

    // 保存按钮顺序：[0] 网站名称卡片，[1] 知识图谱卡片
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[1])

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

  it('沙箱上限卡片默认折叠，回显默认值，修改后保存三项设置', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    const sandboxHeader = screen.getByRole('button', { name: /沙箱资源上限/ })
    expect(sandboxHeader).toHaveAttribute('aria-expanded', 'false')

    fireEvent.click(sandboxHeader)
    const ttlInput = await screen.findByLabelText('沙箱存活时间上限')
    expect(ttlInput).toHaveValue('86400')
    expect(screen.getByLabelText('沙箱 CPU 上限')).toHaveValue('4')
    expect(screen.getByLabelText('沙箱内存上限')).toHaveValue('8Gi')

    fireEvent.change(ttlInput, { target: { value: '3600' } })
    fireEvent.change(screen.getByLabelText('沙箱 CPU 上限'), { target: { value: '2000m' } })
    fireEvent.change(screen.getByLabelText('沙箱内存上限'), { target: { value: '4Gi' } })

    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    expect(saveButton).toBeTruthy()
    fireEvent.click(saveButton!)

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SANDBOX_MAX_TTL_SECONDS', '3600'))
    expect(saveSetting).toHaveBeenCalledWith('SANDBOX_MAX_CPU', '2000m')
    expect(saveSetting).toHaveBeenCalledWith('SANDBOX_MAX_MEMORY', '4Gi')
  })

  it('沙箱上限格式非法时不提交并提示', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /沙箱资源上限/ }))
    await screen.findByLabelText('沙箱存活时间上限')

    fireEvent.change(screen.getByLabelText('沙箱 CPU 上限'), { target: { value: 'fast' } })

    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    expect(saveButton).toBeTruthy()
    fireEvent.click(saveButton!)

    // 格式非法时前置校验拦截，不发起任何保存请求
    expect(saveSetting).not.toHaveBeenCalled()
  })

  it('非 root 管理员被重定向，不渲染知识图谱设置', async () => {
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'admin2', isAdmin: true, isRoot: false },
    })

    renderSettings()

    await waitFor(() => expect(screen.queryByText('知识图谱（图数据库）')).toBeNull())
    expect(screen.queryByLabelText('连接地址')).toBeNull()
  })

  it('网站名称卡片默认展开，回显设置值，保存时去空白提交 SYSTEM_NAME 并刷新 serverInfo', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ SYSTEM_NAME: '旧站名' }))
    renderSettings()

    const input = await screen.findByLabelText('网站名称')
    expect(input).toHaveValue('旧站名')

    fireEvent.change(input, { target: { value: '  新站名  ' } })
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SYSTEM_NAME', '新站名'))
    await waitFor(() => expect(refreshServerInfo).toHaveBeenCalled())
  })

  it('网站名称清空保存提交空串（恢复默认名称）', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ SYSTEM_NAME: '旧站名' }))
    renderSettings()
    const input = await screen.findByLabelText('网站名称')

    fireEvent.change(input, { target: { value: '' } })
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[0])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SYSTEM_NAME', ''))
  })

  it('网站 Logo 卡片默认展开，无自定义 Logo 时回退默认且不显示恢复按钮', async () => {
    const { container } = renderSettings()

    const header = await screen.findByRole('button', { name: /网站 Logo/ })
    expect(header).toHaveAttribute('aria-expanded', 'true')
    // 图标 svg 也带 img role，直接取 img 元素（头像框内）
    const img = container.querySelector('img')
    expect(img).toHaveAttribute('src', '/logo.svg')
    expect(screen.queryByRole('button', { name: /恢复默认/ })).toBeNull()
  })

  it('选择图片后上传 Logo 并刷新全局 serverInfo', async () => {
    renderSettings()
    await screen.findByRole('button', { name: /网站 Logo/ })

    const input = document.querySelector('input[type="file"]') as HTMLInputElement
    expect(input).toBeTruthy()
    const file = new File(['png-bytes'], 'logo.png', { type: 'image/png' })
    fireEvent.change(input, { target: { files: [file] } })

    const { updateSystemLogo } = await import('@/api/settings')
    await waitFor(() => expect(updateSystemLogo).toHaveBeenCalledWith(file))
    await waitFor(() => expect(refreshServerInfo).toHaveBeenCalled())
    // 上传成功后不再显示恢复默认按钮缺失的分支：此时已有自定义 Logo
    expect(await screen.findByRole('button', { name: /恢复默认/ })).toBeInTheDocument()
  })

  it('非图片文件被前端拦截，不发起上传', async () => {
    renderSettings()
    await screen.findByRole('button', { name: /网站 Logo/ })

    const input = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(input, { target: { files: [new File(['x'], 'a.txt', { type: 'text/plain' })] } })

    const { updateSystemLogo } = await import('@/api/settings')
    expect(updateSystemLogo).not.toHaveBeenCalled()
  })

  it('已有自定义 Logo 时可确认恢复默认', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ SYSTEM_LOGO: 'public/images/old.png' }))
    const { container } = renderSettings()
    const resetBtn = await screen.findByRole('button', { name: /恢复默认/ })
    expect(container.querySelector('img')).toHaveAttribute(
      'src',
      expect.stringContaining('public/images/old.png'),
    )

    fireEvent.click(resetBtn)
    expect(await screen.findByText('确定恢复使用默认 Logo 吗？')).toBeInTheDocument()

    fireEvent.click(await screen.findByRole('button', { name: /ok/i }))
    const { resetSystemLogo } = await import('@/api/settings')
    await waitFor(() => expect(resetSystemLogo).toHaveBeenCalled())
    await waitFor(() => expect(refreshServerInfo).toHaveBeenCalled())
  })
})
