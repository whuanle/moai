import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import '@/i18n'
import { Settings } from '../Settings'
import { useAppStore } from '@/store/app'
import { getSettings, saveSetting } from '@/api/settings'
import { getNotificationChannels } from '@/api/notification'
import { getMemoryModelMigrationStatus, startMemoryModelMigration } from '@/api/memory'
import { refreshServerInfo } from '@/api/auth'

vi.mock('@/api/settings', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/settings')>()
  return {
    ...actual,
    getSettings: vi.fn(),
    saveSetting: vi.fn().mockResolvedValue(undefined),
    updateSystemLogo: vi.fn().mockResolvedValue('public/images/logo.png'),
    resetSystemLogo: vi.fn().mockResolvedValue(undefined),
    updateLoginBackground: vi.fn().mockResolvedValue('public/images/bg.png'),
    resetLoginBackground: vi.fn().mockResolvedValue(undefined),
  }
})

vi.mock('@/api/notification', () => ({
  getNotificationChannels: vi.fn().mockResolvedValue([
    { id: 11, name: '桩邮件服务', type: 'smtp', enabled: true, config: '' },
    { id: 12, name: '停用邮件服务', type: 'smtp', enabled: false, config: '' },
    { id: 13, name: '飞书渠道', type: 'feishuWebhook', enabled: true, config: '' },
  ]),
  getNotificationTemplates: vi.fn().mockResolvedValue([
    { id: '1', name: '注册验证码邮件', isBuiltIn: true, contentType: 'html', variables: '[]' },
    { id: '2', name: '登录验证码邮件', isBuiltIn: true, contentType: 'html', variables: '[]' },
    { id: '3', name: '找回密码验证码邮件', isBuiltIn: true, contentType: 'html', variables: '[]' },
  ]),
}))

vi.mock('@/api/auth', () => ({
  refreshServerInfo: vi.fn().mockResolvedValue({
    serviceUrl: 'http://127.0.0.1:5000',
    publicStoreUrl: '',
    rsaPublic: '',
    logoPath: '',
    loginBackgroundPath: '',
    name: 'MoAI',
  }),
}))

vi.mock('@/api/memory', () => ({
  getGlobalMemoryModelOptions: vi.fn().mockResolvedValue([
    { id: 'emb-1', name: '桩全局向量模型' },
    { id: 'emb-2', name: '桩向量模型B' },
  ]),
  clearAllMemory: vi.fn().mockResolvedValue(undefined),
  getMemoryModelMigrationStatus: vi.fn().mockResolvedValue({
    currentModelId: '',
    currentModelName: '',
    currentDimensions: 0,
    totalMemoryCount: 0,
    activeMigration: null,
  }),
  startMemoryModelMigration: vi.fn().mockResolvedValue('task-1'),
}))

function settingsResponse(overrides: Record<string, string> = {}) {
  const values: Record<string, string> = {
    KG_ENABLED: 'false',
    KG_URI: '',
    KG_USERNAME: '',
    KG_PASSWORD: '',
    KG_DIALECT: 'memgraph',
    WIKI_MAX_FILE_SIZE_MB: '50',
    WIKI_ALLOWED_FILE_TYPES: '.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json',
    SANDBOX_MAX_TTL_SECONDS: '86400',
    SANDBOX_MAX_CPU: '4',
    SANDBOX_MAX_MEMORY: '8Gi',
    SYSTEM_LOGO: '',
    SYSTEM_NAME: '',
    LOGIN_BACKGROUND: '',
    SMTP_ENABLED: 'false',
    SMTP_CHANNEL_ID: '',
    SMTP_REGISTER_CODE_TEMPLATE_ID: '',
    SMTP_RESET_CODE_TEMPLATE_ID: '',
    SMTP_LOGIN_CODE_TEMPLATE_ID: '',
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

    // 保存按钮顺序：[0] 网站信息卡片，[1] 知识图谱卡片
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

    // 保存按钮顺序：[0] 网站信息卡片，[1] 知识图谱卡片
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

  it('邮箱验证卡片默认折叠，展开后未启用时不显示绑定下拉与模板字段', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    const emailHeader = screen.getByRole('button', { name: /邮箱验证/ })
    expect(emailHeader).toHaveAttribute('aria-expanded', 'false')

    fireEvent.click(emailHeader)
    expect(await screen.findByText('启用邮箱验证')).toBeInTheDocument()
    // 未启用不提供绑定下拉与验证码模板
    expect(screen.queryByLabelText('绑定 SMTP 服务')).toBeNull()
    expect(screen.queryByLabelText('注册验证码邮件主题')).toBeNull()
    // 连接配置已收敛到通知中心，不再有服务器/账号字段
    expect(screen.queryByLabelText('SMTP 服务器')).toBeNull()
    expect(screen.queryByLabelText('登录账号')).toBeNull()
  })

  it('绑定下拉来自通知中心 SMTP 渠道，停用渠道带标记且不出现其它类型', async () => {
    vi.mocked(getNotificationChannels).mockResolvedValue([
      { id: 11, name: '桩邮件服务', type: 'smtp', enabled: true, config: '' },
      { id: 12, name: '停用邮件服务', type: 'smtp', enabled: false, config: '' },
      { id: 13, name: '飞书渠道', type: 'feishuWebhook', enabled: true, config: '' },
    ] as never)
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /邮箱验证/ }))
    fireEvent.click(screen.getAllByRole('switch')[1])

    // antd Select 的 aria-label 同时落在容器与内层 input，直接按容器类名定位触发展开
    const bindingSelector = document.querySelector('div[aria-label="绑定 SMTP 服务"] .ant-select-selector') as HTMLElement
    expect(bindingSelector).toBeTruthy()
    fireEvent.mouseDown(bindingSelector)
    expect(await screen.findByText('桩邮件服务')).toBeInTheDocument()
    expect(screen.getByText('停用邮件服务（已停用）')).toBeInTheDocument()
    expect(screen.queryByText('飞书渠道')).toBeNull()
  })

  it('启用并绑定 SMTP 服务后保存，先提交开关再提交绑定渠道与验证码开关，不提交连接字段', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ SMTP_ENABLED: 'true', SMTP_CHANNEL_ID: '11' }))
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /邮箱验证/ }))
    // 启用开关已开启，直接打开注册验证码开关后保存
    fireEvent.click(await screen.findByLabelText('注册需要邮箱验证码'))

    // 保存按钮顺序：[0] 网站信息卡片，[1] 知识图谱卡片，[2] 邮箱验证卡片
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[2])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SMTP_ENABLED', 'true'))
    expect(saveSetting).toHaveBeenCalledWith('SMTP_CHANNEL_ID', '11')
    expect(saveSetting).toHaveBeenCalledWith('REGISTER_EMAIL_CODE_ENABLED', 'true')
    expect(saveSetting).not.toHaveBeenCalledWith('SMTP_HOST', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('SMTP_USERNAME', expect.anything())
  })

  it('解绑（清空）SMTP 服务后保存提交空绑定', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ SMTP_ENABLED: 'true', SMTP_CHANNEL_ID: '11' }))
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /邮箱验证/ }))
    const clearIcon = document.querySelector('div[aria-label="绑定 SMTP 服务"] .ant-select-clear') as HTMLElement
    expect(clearIcon).toBeTruthy()
    // antd 的 clear 图标在 onMouseDown 中清空选中值
    fireEvent.mouseDown(clearIcon)

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[2])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SMTP_CHANNEL_ID', ''))
  })

  it('验证码通知模板绑定：下拉回显内置模板标识，清空后保存提交空模板绑定', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({
      SMTP_ENABLED: 'true',
      SMTP_REGISTER_CODE_TEMPLATE_ID: '1',
      SMTP_RESET_CODE_TEMPLATE_ID: '3',
      SMTP_LOGIN_CODE_TEMPLATE_ID: '2',
    }))
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /邮箱验证/ }))
    // 三个绑定下拉回显已绑定模板，内置模板带「内置」标识
    await screen.findByText('注册验证码邮件（内置）')
    const registerSelect = document.querySelector('div[aria-label="注册验证码通知模板"]') as HTMLElement
    expect(registerSelect).toBeTruthy()
    expect(registerSelect).toHaveTextContent('注册验证码邮件（内置）')
    expect(document.querySelector('div[aria-label="找回密码验证码通知模板"]')).toHaveTextContent('找回密码验证码邮件（内置）')
    expect(document.querySelector('div[aria-label="登录验证码通知模板"]')).toHaveTextContent('登录验证码邮件（内置）')

    // 清空注册模板绑定后保存：清空的键提交空串，其余两个保持原绑定
    const clearIcon = registerSelect.querySelector('.ant-select-clear') as HTMLElement
    expect(clearIcon).toBeTruthy()
    fireEvent.mouseDown(clearIcon)

    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ })[2])

    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SMTP_REGISTER_CODE_TEMPLATE_ID', ''))
    expect(saveSetting).toHaveBeenCalledWith('SMTP_RESET_CODE_TEMPLATE_ID', '3')
    expect(saveSetting).toHaveBeenCalledWith('SMTP_LOGIN_CODE_TEMPLATE_ID', '2')
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

  it('知识库卡片回显允许上传的文件类型，新增扩展名保存时提交归一后的逗号串', async () => {
    const { container } = renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /知识库/ }))
    // 默认类型回显为标签
    expect(await screen.findByText('.pdf')).toBeInTheDocument()
    expect(screen.getByText('.markdown')).toBeInTheDocument()

    // 在类型选择器中输入 ".sh," 经逗号分隔符归一为标签
    const selectInput = container.querySelector('.ant-select-selection-search-input') as HTMLInputElement
    expect(selectInput).toBeTruthy()
    fireEvent.change(selectInput, { target: { value: '.sh,' } })
    await screen.findByText('.sh')

    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    fireEvent.click(saveButton!)

    await waitFor(() =>
      expect(saveSetting).toHaveBeenCalledWith(
        'WIKI_ALLOWED_FILE_TYPES',
        '.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json,.sh',
      ),
    )
    expect(saveSetting).toHaveBeenCalledWith('WIKI_MAX_FILE_SIZE_MB', '50')
  })

  it('文件类型含非法扩展名时保存被前端拦截，不发起请求', async () => {
    const { container } = renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /知识库/ }))
    await screen.findByText('.pdf')

    const selectInput = container.querySelector('.ant-select-selection-search-input') as HTMLInputElement
    fireEvent.change(selectInput, { target: { value: 'bad,' } })

    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    fireEvent.click(saveButton!)

    // 非法扩展名时前置校验拦截，不发起任何保存请求
    expect(saveSetting).not.toHaveBeenCalled()
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

  it('沙箱禁用命令清单随卡片保存提交，非法模式拦截不发请求', async () => {
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /沙箱资源上限/ }))
    const denyInput = await screen.findByLabelText('平台级禁用命令清单')

    // 非法模式（regex: 不可编译）拦截
    fireEvent.change(denyInput, { target: { value: 'shutdown\nregex:([bad' } })
    const saveButton = screen
      .getAllByRole('button', { name: /保\s*存/ })
      .find((b) => !(b as HTMLButtonElement).disabled)
    expect(saveButton).toBeTruthy()
    fireEvent.click(saveButton!)
    expect(saveSetting).not.toHaveBeenCalled()

    // 合法清单随卡片保存一并提交（换行分隔原文）
    fireEvent.change(denyInput, { target: { value: 'shutdown\nmkfs' } })
    fireEvent.click(screen.getAllByRole('button', { name: /保\s*存/ }).find((b) => !(b as HTMLButtonElement).disabled)!)
    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('SANDBOX_DENIED_COMMANDS', 'shutdown\nmkfs'))
  })

  it('非 root 管理员被重定向，不渲染知识图谱设置', async () => {
    useAppStore.setState({
      userInfo: { accessToken: 'token', userId: '2', userName: 'admin2', isAdmin: true, isRoot: false },
    })

    renderSettings()

    await waitFor(() => expect(screen.queryByText('知识图谱（图数据库）')).toBeNull())
    expect(screen.queryByLabelText('连接地址')).toBeNull()
  })

  it('网站信息卡片默认展开，网站名称回显设置值，保存时去空白提交 SYSTEM_NAME 并刷新 serverInfo', async () => {
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

  it('网站信息卡片默认展开，无自定义 Logo 时回退默认且不显示恢复按钮', async () => {
    const { container } = renderSettings()

    const header = await screen.findByRole('button', { name: /网站信息/ })
    expect(header).toHaveAttribute('aria-expanded', 'true')
    // 图标 svg 也带 img role，直接取 img 元素（头像框内）
    const img = container.querySelector('img')
    expect(img).toHaveAttribute('src', '/logo.svg')
    expect(screen.queryByRole('button', { name: /恢复默认/ })).toBeNull()
  })

  it('选择图片后上传 Logo 并刷新全局 serverInfo', async () => {
    renderSettings()
    await screen.findByRole('button', { name: /网站信息/ })

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
    await screen.findByRole('button', { name: /网站信息/ })

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

  it('网站信息卡片默认展开，无自定义背景时显示占位且不显示恢复按钮', async () => {
    renderSettings()

    const header = await screen.findByRole('button', { name: /网站信息/ })
    expect(header).toHaveAttribute('aria-expanded', 'true')
    expect(await screen.findByText('默认渐变背景')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /恢复默认/ })).toBeNull()
  })

  it('选择图片后上传登录页背景并刷新全局 serverInfo', async () => {
    renderSettings()
    await screen.findByRole('button', { name: /网站信息/ })

    // 文件输入顺序（同一卡片内）：[0] 网站 Logo，[1] 登录页背景图
    const inputs = document.querySelectorAll('input[type="file"]')
    expect(inputs.length).toBeGreaterThanOrEqual(2)
    const file = new File(['png-bytes'], 'bg.png', { type: 'image/png' })
    fireEvent.change(inputs[1], { target: { files: [file] } })

    const { updateLoginBackground } = await import('@/api/settings')
    await waitFor(() => expect(updateLoginBackground).toHaveBeenCalledWith(file))
    await waitFor(() => expect(refreshServerInfo).toHaveBeenCalled())
    expect(await screen.findByRole('button', { name: /恢复默认/ })).toBeInTheDocument()
  })

  it('登录页背景非图片文件被前端拦截，不发起上传', async () => {
    renderSettings()
    await screen.findByRole('button', { name: /网站信息/ })

    const inputs = document.querySelectorAll('input[type="file"]')
    fireEvent.change(inputs[1], { target: { files: [new File(['x'], 'b.txt', { type: 'text/plain' })] } })

    const { updateLoginBackground } = await import('@/api/settings')
    expect(updateLoginBackground).not.toHaveBeenCalled()
  })

  it('已有自定义登录页背景时可确认恢复默认', async () => {
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({ LOGIN_BACKGROUND: 'public/images/bg.png' }))
    const { container } = renderSettings()

    const resetBtn = await screen.findByRole('button', { name: /恢复默认/ })
    // 背景 img 是第二张（第一张为 Logo 头像框）
    const imgs = container.querySelectorAll('img')
    expect(imgs[1]).toHaveAttribute('src', expect.stringContaining('public/images/bg.png'))

    fireEvent.click(resetBtn)
    expect(await screen.findByText('确定恢复使用默认背景吗？')).toBeInTheDocument()

    fireEvent.click(await screen.findByRole('button', { name: /ok/i }))
    const { resetLoginBackground } = await import('@/api/settings')
    await waitFor(() => expect(resetLoginBackground).toHaveBeenCalled())
    await waitFor(() => expect(refreshServerInfo).toHaveBeenCalled())
  })

  it('长期记忆锁定态：显示锁定提示与迁移入口，保存仅提交开关不提交模型/维度', async () => {
    vi.mocked(getMemoryModelMigrationStatus).mockResolvedValue({
      currentModelId: 'emb-1',
      currentModelName: '桩全局向量模型',
      currentDimensions: 1024,
      totalMemoryCount: 3,
      activeMigration: null,
    })
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({
      MEMORY_ENABLED: 'true',
      MEMORY_EMBEDDING_MODEL: 'emb-1',
      MEMORY_EMBEDDING_DIMENSIONS: '1024',
    }))
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /长期记忆/ }))
    expect(await screen.findByText(/平台已有 3 条记忆/)).toBeInTheDocument()
    // 未改动模型/维度时迁移入口禁用
    expect(screen.getByRole('button', { name: /开始迁移/ })).toBeDisabled()

    // 锁定态保存只提交开关，模型/维度改动须走迁移接口（antd 按钮两汉字间自动插空格，用正则匹配）
    fireEvent.click(screen.getAllByRole('switch')[1])
    const saveButtons = screen.getAllByRole('button', { name: /保\s*存/ })
    fireEvent.click(saveButtons[saveButtons.length - 1])
    await waitFor(() => expect(saveSetting).toHaveBeenCalledWith('MEMORY_ENABLED', 'false'))
    expect(saveSetting).not.toHaveBeenCalledWith('MEMORY_EMBEDDING_MODEL', expect.anything())
    expect(saveSetting).not.toHaveBeenCalledWith('MEMORY_EMBEDDING_DIMENSIONS', expect.anything())
  })

  it('长期记忆锁定态：改动模型后迁移入口启用，确认后调用迁移接口', async () => {
    vi.mocked(getMemoryModelMigrationStatus).mockResolvedValue({
      currentModelId: 'emb-1',
      currentModelName: '桩全局向量模型',
      currentDimensions: 1024,
      totalMemoryCount: 3,
      activeMigration: null,
    })
    vi.mocked(getSettings).mockResolvedValue(settingsResponse({
      MEMORY_ENABLED: 'true',
      MEMORY_EMBEDDING_MODEL: 'emb-1',
      MEMORY_EMBEDDING_DIMENSIONS: '1024',
    }))
    renderSettings()
    await screen.findByText('知识图谱（图数据库）')

    fireEvent.click(screen.getByRole('button', { name: /长期记忆/ }))
    await screen.findByText(/平台已有 3 条记忆/)

    // 改动向量化模型（antd Select：mouseDown 展开 → 点击选项；aria-label 在嵌套输入上重复，取角色定位）
    fireEvent.mouseDown(screen.getByRole('combobox'))
    fireEvent.click(await screen.findByText('桩向量模型B'))

    const migrateButton = screen.getByRole('button', { name: /开始迁移/ })
    await waitFor(() => expect(migrateButton).not.toBeDisabled())
    fireEvent.click(migrateButton)
    fireEvent.click(await screen.findByRole('button', { name: /确\s*定/ }))
    await waitFor(() => expect(startMemoryModelMigration).toHaveBeenCalledWith('emb-2', 1024))
  })
})
