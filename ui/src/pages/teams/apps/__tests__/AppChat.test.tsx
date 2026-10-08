import { describe, expect, it, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AppChat } from '../AppChat'
import {
  createAppSession,
  decideAppSessionToolApproval,
  deleteAppSession,
  extractChatAttachment,
  getAppDetail,
  getAppSessionMessages,
  getAppSessions,
  getAppUserConfig,
  saveAppUserConfig,
  updateAppSessionPrompt,
} from '@/api/app'
import { getMyPrompts, getTeamPrompts, getTopUsedPrompts } from '@/api/prompt'
import { abortAppChat, createAppChatAgent, runAppChat, type AgentChatHandlers } from '@/api/agentChat'
import { uploadChatFile } from '@/utils/storage'

vi.mock('@/api/app', () => ({
  getAppDetail: vi.fn(),
  getAppSessions: vi.fn(),
  createAppSession: vi.fn(),
  getAppSessionMessages: vi.fn(),
  deleteAppSession: vi.fn().mockResolvedValue(undefined),
  updateAppSessionPrompt: vi.fn().mockResolvedValue(undefined),
  getAppUserConfig: vi.fn(),
  saveAppUserConfig: vi.fn().mockResolvedValue(undefined),
  decideAppSessionToolApproval: vi.fn(),
  extractChatAttachment: vi.fn(),
}))

vi.mock('@/api/prompt', () => ({
  getMyPrompts: vi.fn(),
  getTeamPrompts: vi.fn(),
  getTopUsedPrompts: vi.fn(),
}))

vi.mock('@/api/agentChat', () => ({
  createAppChatAgent: vi.fn().mockReturnValue({}),
  runAppChat: vi.fn(),
  abortAppChat: vi.fn(),
}))

vi.mock('@/utils/storage', () => ({
  resolveStorageUrl: vi.fn(() => ''),
  uploadImageWithKey: vi.fn(),
  uploadImage: vi.fn(),
  uploadChatFile: vi.fn(),
}))

vi.mock('../chat/EChart', () => ({
  EChart: (props: { option: unknown }) => <div data-testid="echart-mock">{JSON.stringify(props.option)}</div>,
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/team/3/app/a1/chat']}>
      <Routes>
        <Route path="/team/:teamId/app/:appId/chat/:sessionId?" element={<AppChat />} />
        <Route path="/apps" element={<div data-testid="apps-market" />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppChat（Agent 应用对话页）', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      name: '客服助手',
      quickInputs: ['帮我总结一份文档的核心要点', '根据知识库回答一个业务问题'],
    } as never)
    vi.mocked(getAppSessions).mockResolvedValue([
      { sessionId: 's1', title: '第一段对话', lastMessageTime: '2026-09-11T10:00:00Z' },
    ] as never)
    vi.mocked(createAppSession).mockResolvedValue('s2')
    vi.mocked(getAppSessionMessages).mockResolvedValue([])
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 0,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'auto',
      toolApprovalExemptNames: ['search_knowledge_base'],
      toolApprovalExemptPrefixes: ['skill_'],
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
    })
    vi.mocked(getMyPrompts).mockResolvedValue([
      { promptId: 11, name: '写作专家', description: '文案润色', teamId: 0 },
    ] as never)
    vi.mocked(getTeamPrompts).mockResolvedValue([
      { promptId: 22, name: '客服专家', description: '售后话术', teamId: 3 },
    ] as never)
    vi.mocked(getTopUsedPrompts).mockResolvedValue([
      { promptId: 22, name: '客服专家', description: '售后话术', teamId: 3, useCount: 8 },
      { promptId: 11, name: '写作专家', description: '文案润色', teamId: 0, useCount: 3 },
    ] as never)
    vi.mocked(updateAppSessionPrompt).mockResolvedValue(undefined)
    vi.mocked(decideAppSessionToolApproval).mockResolvedValue('approved')
  })

  it('加载后展示会话列表与居中欢迎态（应用名问候 + 热门专家 + 快捷输入，无面包屑/大标题）', async () => {
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('第一段对话')).toBeInTheDocument()
    })
    // 欢迎态：应用名问候（顶栏 + 居中大标题）+ 输入框占位为「发消息给 …」
    expect(screen.getAllByText('客服助手').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByPlaceholderText(/发消息给/)).toBeInTheDocument()
    // 快捷输入来自应用配置（管理员自定义），不再使用写死的默认建议
    expect(screen.getByText('帮我总结一份文档的核心要点')).toBeInTheDocument()
    expect(screen.getByText('根据知识库回答一个业务问题')).toBeInTheDocument()
    // 输入框下方展示热门专家（含使用次数）；专家列表在应用详情确认非流程应用后加载，异步到达
    await waitFor(() => expect(screen.getByText('热门专家')).toBeInTheDocument())
    expect(screen.getByText('8 次使用')).toBeInTheDocument()
    expect(screen.queryByText(/输入问题开始对话/)).not.toBeInTheDocument()
    // 无 Enter 提示文案、无面包屑
    expect(screen.queryByText(/Enter 发送/)).not.toBeInTheDocument()
    expect(screen.queryByText('应用对话')).not.toBeInTheDocument()
    expect(document.querySelector('.ant-breadcrumb')).toBeNull()
    expect(document.querySelector('.moai-chat__main.is-landing')).not.toBeNull()
  })

  it('删除会话：悬浮显示删除入口，确认后删除当前会话并回到欢迎态', async () => {
    vi.mocked(getAppSessionMessages).mockResolvedValue([
      { messageId: 'm1', seq: 1, role: 'user', content: '你好' },
      { messageId: 'm2', seq: 2, role: 'assistant', content: '你好，请问有什么可以帮你？' },
    ] as never)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    // 删除入口存在于每个会话项（悬浮显示、红色由 CSS 控制，jsdom 不加载样式表）
    expect(document.querySelector('.moai-chat__session-del')?.getAttribute('aria-label')).toBe('删除会话')

    // 先进入该会话（有消息，非欢迎态）
    fireEvent.click(screen.getByText('第一段对话'))
    await waitFor(() => expect(getAppSessionMessages).toHaveBeenCalledWith('s1'))
    await waitFor(() => expect(screen.getByText('你好，请问有什么可以帮你？')).toBeInTheDocument())
    expect(document.querySelector('.moai-chat__main.is-landing')).toBeNull()

    // 点击删除（选中会话会引起重渲染，须重新查询节点）→ Popconfirm 确认 → 调删除接口并刷新列表
    fireEvent.click(document.querySelector('.moai-chat__session-del') as HTMLElement)
    expect(await screen.findByText('确定删除该会话？')).toBeTruthy()
    fireEvent.click(document.querySelector('.ant-popconfirm-buttons .ant-btn-primary') as HTMLElement)
    await waitFor(() => expect(deleteAppSession).toHaveBeenCalledWith('s1'))
    await waitFor(() => expect(getAppSessions).toHaveBeenCalledTimes(2))
    // 删除的是当前会话：清空消息回到欢迎态
    await waitFor(() => expect(document.querySelector('.moai-chat__main.is-landing')).not.toBeNull())
  })

  it('欢迎态点击快捷输入：直接发送（创建会话并携带快捷问题，无需手动点发送）', async () => {
    vi.mocked(runAppChat).mockResolvedValue(undefined)

    renderPage()
    await waitFor(() => expect(screen.getByText('帮我总结一份文档的核心要点')).toBeInTheDocument())

    fireEvent.click(screen.getByText('帮我总结一份文档的核心要点'))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 0)
    })
    await waitFor(() => expect(runAppChat).toHaveBeenCalled())
    const outgoing = vi.mocked(runAppChat).mock.calls[0][1] as string
    expect(outgoing).toBe('帮我总结一份文档的核心要点')
  })

  it('发送消息时创建会话并流式追加回复，切换为对话布局', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onDelta?.('你好')
      handlers.onDelta?.('你好，很高兴为您服务')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    const textarea = screen.getByPlaceholderText(/发消息给/)
    fireEvent.change(textarea, { target: { value: '在吗' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 0)
    })
    expect(runAppChat).toHaveBeenCalled()
    await waitFor(() => {
      expect(screen.getByText(/很高兴为您服务/)).toBeInTheDocument()
    })
    expect(document.querySelector('.moai-chat__bubble')?.textContent).toBe('在吗')
    // 发送后离开欢迎态，输入框停靠底部
    expect(document.querySelector('.moai-chat__main.is-landing')).toBeNull()
  })

  it('助手回复以 Markdown 渲染', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onDelta?.('**加粗** 与 `代码`')
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: 'x' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(screen.getByText('加粗').tagName).toBe('STRONG')
    })
    expect(screen.getByText('代码').tagName).toBe('CODE')
  })

  it('打开应用设置面板：专家列表展示个人与团队提示词（右上角无独立专家按钮）', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    // 右上角只剩应用设置入口，专家选择并入设置面板
    expect(screen.queryByRole('button', { name: '专家' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '应用设置' }))

    await waitFor(() => {
      expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(1)
    })
    expect(screen.getAllByText('客服专家').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('文案润色')).toBeInTheDocument()
    // 个人/团队来源标签
    expect(screen.getByText('个人')).toBeInTheDocument()
    expect(screen.getByText('团队')).toBeInTheDocument()
  })

  it('欢迎态点击热门专家：本地记录，首轮创建会话时一并绑定', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('热门专家')).toBeInTheDocument())

    // 热门专家列表中点击「客服专家」（promptId=22）
    fireEvent.click(screen.getAllByText('客服专家')[0])

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '你好' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 22)
    })
    expect(updateAppSessionPrompt).not.toHaveBeenCalled()
    // 输入区上方展示当前专家提示条
    expect(screen.getByText('当前专家')).toBeInTheDocument()
  })

  it('未发送消息时在应用设置中选择专家：保存后本地生效，首轮创建会话时一并绑定', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: '应用设置' }))
    await waitFor(() => expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(1))
    // 设置面板中点选「写作专家」（面板 DOM 先于欢迎态渲染，取第一处）
    fireEvent.click(screen.getAllByText('写作专家')[0])
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }))

    // 保存持久化到用户级配置，并立即应用为当前专家
    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', expect.objectContaining({ promptId: 11 }))
    })
    expect(screen.getByText('当前专家')).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '你好' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 11)
    })
    expect(updateAppSessionPrompt).not.toHaveBeenCalled()
    // 当前专家提示条渲染专家名
    expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(1)
  })

  it('已有会话时在应用设置中切换专家：保存时调用绑定接口，再次选取消', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByText('第一段对话'))
    await waitFor(() => expect(getAppSessionMessages).toHaveBeenCalledWith('s1'))

    fireEvent.click(screen.getByRole('button', { name: '应用设置' }))
    await waitFor(() => expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(1))
    fireEvent.click(screen.getAllByText('写作专家')[0])
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }))

    await waitFor(() => {
      expect(updateAppSessionPrompt).toHaveBeenCalledWith('s1', 11)
    })

    // 等待当前专家生效（提示条出现）后重开面板，再次点击取消绑定
    await waitFor(() => {
      expect(screen.getByText('当前专家')).toBeInTheDocument()
    })
    fireEvent.click(screen.getByRole('button', { name: '应用设置' }))
    await waitFor(() => expect(screen.getAllByText('写作专家').length).toBeGreaterThanOrEqual(1))
    fireEvent.click(screen.getAllByText('写作专家')[0])
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }))

    await waitFor(() => {
      expect(updateAppSessionPrompt).toHaveBeenCalledWith('s1', 0)
    })
  })

  it('审批模式：call_tool 触及沙箱工具时展示审批卡，批准后调用决策接口', async () => {
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 0,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'approval',
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      toolApprovalExemptNames: ['search_knowledge_base'],
      toolApprovalExemptPrefixes: ['skill_'],
    })
    let finishRun: (() => void) | undefined
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onToolCall?.({ id: 'tc-1', name: 'call_tool' })
      handlers.onToolCallEnd?.({
        id: 'tc-1',
        name: 'call_tool',
        args: { toolName: 'sandbox_run_code', argumentsJson: '{"language":"python","code":"print(1)"}' },
      })
      await new Promise<void>((resolve) => {
        finishRun = resolve
      })
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '执行一段代码' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 审批卡：真实工具名 + 等待审批 + 批准/拒绝按钮
    await waitFor(() => {
      expect(screen.getByText('sandbox_run_code')).toBeInTheDocument()
    })
    expect(screen.getByText('等待审批')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '批准执行' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: '批准执行' }))

    await waitFor(() => {
      expect(decideAppSessionToolApproval).toHaveBeenCalledWith('s2', 'sandbox_run_code', true)
    })
    // 批准生效：卡片进入「已批准 · 执行中」
    await waitFor(() => {
      expect(screen.getByText('已批准 · 执行中')).toBeInTheDocument()
    })

    finishRun?.()
  })

  it('审批模式：知识库检索为豁免工具，不展示审批按钮', async () => {
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 0,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'approval',
      toolApprovalAutoApprovedNames: [],
      toolApprovalAutoApprovedPrefixes: [],
      toolApprovalExemptNames: ['search_knowledge_base'],
      toolApprovalExemptPrefixes: ['skill_'],
    })
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onToolCallEnd?.({
        id: 'tc-2',
        name: 'call_tool',
        args: { toolName: 'search_knowledge_base', argumentsJson: '{"query":"售后政策"}' },
      })
      handlers.onDelta?.('已检索知识库。')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '查一下售后政策' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      expect(screen.getByText('search_knowledge_base')).toBeInTheDocument()
    })
    // 豁免工具直接执行（执行中/已完成），无审批按钮
    expect(screen.queryByRole('button', { name: '批准执行' })).not.toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByText('已完成')).toBeInTheDocument()
    })
  })

  it('审批模式：审批策略自动放行的插件与沙箱工具不展示审批卡', async () => {
    // 应用审批策略：白名单插件（weather 工具名精确命中）+ 沙箱自动放行（sandbox_ 前缀命中）
    vi.mocked(getAppUserConfig).mockResolvedValue({
      promptId: 0,
      skills: [],
      defaultSkills: [],
      toolApprovalMode: 'approval',
      toolApprovalAutoApprovedNames: ['weather'],
      toolApprovalAutoApprovedPrefixes: ['sandbox_'],
      toolApprovalExemptNames: ['search_knowledge_base'],
      toolApprovalExemptPrefixes: ['skill_'],
    })
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onToolCallEnd?.({
        id: 'tc-3',
        name: 'call_tool',
        args: { toolName: 'weather', argumentsJson: '{"city":"北京"}' },
      })
      handlers.onToolCallEnd?.({
        id: 'tc-4',
        name: 'call_tool',
        args: { toolName: 'sandbox_run_code', argumentsJson: '{"language":"python","code":"print(1)"}' },
      })
      handlers.onDelta?.('查询完成。')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '北京天气如何并算一道题' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => {
      // 已完成工具默认收缩为折叠组摘要行
      expect(screen.getByRole('button', { name: /已执行 2 次工具/ })).toBeInTheDocument()
    })
    // 展开折叠组后可见明细行
    fireEvent.click(screen.getByRole('button', { name: /已执行 2 次工具/ }))
    await waitFor(() => {
      expect(screen.getByText('weather')).toBeInTheDocument()
      expect(screen.getByText('sandbox_run_code')).toBeInTheDocument()
    })
    // 策略自动放行：无等待审批状态与审批按钮，直接执行
    expect(screen.queryByText('等待审批')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '批准执行' })).not.toBeInTheDocument()
    expect(decideAppSessionToolApproval).not.toHaveBeenCalled()
    await waitFor(() => {
      expect(screen.getAllByText('已完成').length).toBeGreaterThanOrEqual(2)
    })
  })

  it('输入卡左下角切换审批模式：立即持久化到用户级应用配置', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /自动模式/ }))

    await waitFor(() => {
      expect(saveAppUserConfig).toHaveBeenCalledWith('a1', {
        promptId: 0,
        skills: [],
        toolApprovalMode: 'approval',
      })
    })
    // 切换后按钮文案变为审批模式
    expect(screen.getByRole('button', { name: /审批模式/ })).toBeInTheDocument()
  })

  it('上传文档附件：直传后提取文本，发送时拼进消息且气泡以附件 chip 展示', async () => {
    vi.mocked(uploadChatFile).mockResolvedValue({
      objectKey: 'public/chat/abc.docx',
      url: 'http://127.0.0.1:5000/static/public/chat/abc.docx',
    })
    vi.mocked(extractChatAttachment).mockResolvedValue({
      markdown: '# 季度报告\n营收增长 20%',
      contentLength: 18,
      truncated: false,
    })
    vi.mocked(runAppChat).mockResolvedValue(undefined)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    // 通过输入卡附件按钮对应的隐藏 input 选择文件
    const fileInput = document.querySelector('.moai-chat__file-input') as HTMLInputElement
    expect(fileInput).toBeTruthy()
    const file = new File(['fake'], '报告.docx', { type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' })
    Object.defineProperty(fileInput, 'files', { value: [file] })
    fireEvent.change(fileInput)

    // 附件 chip 就绪（提取完成）
    await waitFor(() => {
      expect(extractChatAttachment).toHaveBeenCalledWith('public/chat/abc.docx', '报告.docx')
    })
    await waitFor(() => {
      const chips = document.querySelectorAll('.moai-chat__attachment')
      expect(chips.length).toBe(1)
    })
    // 文档附件展示类型图标（docx→Word 图标），非缩略图
    expect(document.querySelector('.moai-chat__attachment-thumb')).toBeNull()
    expect(document.querySelector('.moai-chat__attachment-icon .anticon-file-word')).not.toBeNull()

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '总结这份文件' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 发送文本 = 用户输入 + 附件标记块（提取内容在内）
    await waitFor(() => expect(runAppChat).toHaveBeenCalled())
    const outgoing = vi.mocked(runAppChat).mock.calls[0][1] as string
    expect(outgoing).toContain('总结这份文件')
    expect(outgoing).toContain('<moai-attachment name="报告.docx">')
    expect(outgoing).toContain('# 季度报告')

    // 用户气泡解析回附件 chip（文档名以按钮形态展示，可展开查看内容）
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /报告\.docx/ })).toBeInTheDocument()
    })
    // 发送后输入卡清空附件
    expect(document.querySelectorAll('.moai-chat__attachment').length).toBe(0)
  })

  it('上传图片附件：不做文本提取，发送文本中携带图片链接', async () => {
    vi.mocked(uploadChatFile).mockResolvedValue({
      objectKey: 'public/chat/xyz.png',
      url: 'http://127.0.0.1:5000/static/public/chat/xyz.png',
    })
    vi.mocked(runAppChat).mockResolvedValue(undefined)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    const fileInput = document.querySelector('.moai-chat__file-input') as HTMLInputElement
    const file = new File(['fake'], '截图.png', { type: 'image/png' })
    Object.defineProperty(fileInput, 'files', { value: [file] })
    fireEvent.change(fileInput)

    await waitFor(() => {
      expect(uploadChatFile).toHaveBeenCalled()
    })
    // 图片附件不触发提取，chip 展示缩略图（直传地址为图片源）
    await waitFor(() => {
      expect(document.querySelectorAll('.moai-chat__attachment').length).toBe(1)
    })
    const thumb = document.querySelector('.moai-chat__attachment-thumb') as HTMLImageElement
    expect(thumb).not.toBeNull()
    expect(thumb.getAttribute('src')).toBe('http://127.0.0.1:5000/static/public/chat/xyz.png')
    expect(extractChatAttachment).not.toHaveBeenCalled()

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '看看这张图' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    await waitFor(() => expect(runAppChat).toHaveBeenCalled())
    const outgoing = vi.mocked(runAppChat).mock.calls[0][1] as string
    // 图片块携带 objectKey 供后端多模态注入，内容为裸下载地址（气泡 chip 可点击打开）
    expect(outgoing).toContain('<moai-attachment name="截图.png" objectKey="public/chat/xyz.png">')
    expect(outgoing).toContain('http://127.0.0.1:5000/static/public/chat/xyz.png')

    // 用户气泡解析回图片附件 chip 并展示缩略图
    await waitFor(() => {
      const bubbleThumb = document.querySelector('.moai-chat__bubble-attachment-thumb') as HTMLImageElement
      expect(bubbleThumb).not.toBeNull()
      expect(bubbleThumb.getAttribute('src')).toBe('http://127.0.0.1:5000/static/public/chat/xyz.png')
    })
  })

  it('附件提取中时发送按钮禁用', async () => {
    vi.mocked(uploadChatFile).mockImplementation(() => new Promise(() => undefined))

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    const fileInput = document.querySelector('.moai-chat__file-input') as HTMLInputElement
    const file = new File(['fake'], 'a.txt', { type: 'text/plain' })
    Object.defineProperty(fileInput, 'files', { value: [file] })
    fireEvent.change(fileInput)

    await waitFor(() => {
      expect(document.querySelector('.moai-chat__attachment')).toBeTruthy()
    })

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: 'hi' } })
    expect(screen.getByRole('button', { name: '发送' })).toBeDisabled()
  })

  it('流程应用：隐藏专家/技能/审批等 Agent 专属 UI，且不发起专家与用户配置请求', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      name: '流程演示',
      appType: 'workflow',
      publishStatus: 1,
      openingStatement: '你好，我是流程应用',
      openingStatementEnabled: true,
      quickInputs: [],
    } as never)

    renderPage()
    await waitFor(() => expect(screen.getAllByText('流程演示').length).toBeGreaterThan(0))

    // 开场白正常展示；会话列表与发送能力保留
    expect(screen.getByText('你好，我是流程应用')).toBeInTheDocument()
    expect(screen.getByText('第一段对话')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /新对话/ })).toBeInTheDocument()

    // Agent 专属能力裁剪：无应用设置入口、无热门专家、无审批模式按钮、无当前专家 chip
    expect(screen.queryByRole('button', { name: '应用设置' })).not.toBeInTheDocument()
    expect(screen.queryByText('热门专家')).not.toBeInTheDocument()
    expect(screen.queryByText('自动模式')).not.toBeInTheDocument()
    expect(screen.queryByText('当前专家')).not.toBeInTheDocument()

    // 后端流程分支不装配专家/技能/审批：对应请求不应发起
    await waitFor(() => expect(getAppDetail).toHaveBeenCalled())
    expect(getAppUserConfig).not.toHaveBeenCalled()
    expect(getMyPrompts).not.toHaveBeenCalled()
    expect(getTeamPrompts).not.toHaveBeenCalled()
    expect(getTopUsedPrompts).not.toHaveBeenCalled()
  })

  it('流程应用：发送消息创建会话（不绑定专家）并流式接收回复', async () => {
    vi.mocked(getAppDetail).mockResolvedValue({
      appId: 'a1',
      name: '流程演示',
      appType: 'workflow',
      publishStatus: 1,
    } as never)
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onDelta?.('流程执行完成')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getAllByText('流程演示').length).toBeGreaterThan(0))

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '查一下天气' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 会话创建不携带专家提示词（流程对话无专家概念）
    await waitFor(() => {
      expect(createAppSession).toHaveBeenCalledWith('a1', undefined, 0)
    })
    await waitFor(() => {
      expect(screen.getByText('流程执行完成')).toBeInTheDocument()
    })
    expect(updateAppSessionPrompt).not.toHaveBeenCalled()
  })

  it('前端展示工具：ui_show_chart 参数流结束后折叠为卡片并自动打开侧边栏，关闭后可再点击打开', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onToolCall?.({ id: 'ui-1', name: 'ui_show_chart' })
      handlers.onToolCallEnd?.({
        id: 'ui-1',
        name: 'ui_show_chart',
        // 线上实测形态：部分模型会把 option 序列化成 JSON 字符串（前端解析层归一化）
        args: {
          title: '销量分析',
          option: JSON.stringify({ series: [{ type: 'bar', data: [3, 5] }] }),
        },
      })
      handlers.onDelta?.('图表已生成，点击卡片查看。')
      handlers.onDone?.()
    })

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '画个销量图表' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 卡片折叠在消息流：类型标签 + 标题，不在正文展开 option
    await waitFor(() => {
      expect(screen.getAllByText('销量分析').length).toBeGreaterThanOrEqual(1)
    })
    expect(screen.getAllByText('图表').length).toBeGreaterThanOrEqual(1)
    // option JSON 不进对话正文（仅存在于侧边栏渲染器）
    const stream = document.querySelector('.moai-chat__stream')
    expect(stream?.textContent).not.toContain('"type":"bar"')

    // 参数流结束自动打开右侧侧边栏渲染图表
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).not.toBeNull()
    })
    expect(screen.getByTestId('echart-mock')).toBeInTheDocument()

    // 关闭侧边栏后，点击卡片可重新打开
    fireEvent.click(screen.getByRole('button', { name: '关闭' }))
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).toBeNull()
    })
    fireEvent.click(screen.getByRole('button', { name: /销量分析/ }))
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).not.toBeNull()
    })
  })

  it('前端展示工具：历史会话回放渲染折叠卡片（点击打开，不自动展开）', async () => {
    vi.mocked(getAppSessionMessages).mockResolvedValue([
      { messageId: 'm1', role: 'user', content: '写份报告' },
      {
        messageId: 'm2',
        role: 'assistant',
        content: '报告已生成，点击卡片查看。',
        toolCalls: JSON.stringify([
          { id: 'ui-h1', name: 'ui_show_document', arguments: { title: '季度报告', content: '# 摘要\n正文' } },
        ]),
      },
    ] as never)
    vi.mocked(runAppChat).mockResolvedValue(undefined)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    // 切入历史会话：折叠卡渲染（类型标签 + 标题），侧边栏不自动展开
    fireEvent.click(screen.getByText('第一段对话'))
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /季度报告/ })).toBeInTheDocument()
    })
    expect(screen.getByText('文档')).toBeInTheDocument()
    expect(document.querySelector('.moai-chat__panel')).toBeNull()

    // 点击卡片打开侧边栏渲染文档
    fireEvent.click(screen.getByRole('button', { name: /季度报告/ }))
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).not.toBeNull()
    })
    expect(screen.getByRole('heading', { name: '摘要' })).toBeInTheDocument()
  })

  it('前端展示工具：一轮多个图表收敛为多标签，可切换、单独关闭，收起后卡片重开保留剩余标签', async () => {
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      handlers.onToolCallEnd?.({
        id: 'ui-a',
        name: 'ui_show_chart',
        args: { title: '销量图表', option: JSON.stringify({ series: [{ type: 'bar', data: [1] }] }) },
      })
      handlers.onToolCallEnd?.({
        id: 'ui-b',
        name: 'ui_show_chart',
        args: { title: '流量图表', option: JSON.stringify({ series: [{ type: 'line', data: [2] }] }) },
      })
      handlers.onDelta?.('两个图表已生成。')
      handlers.onDone?.()
    })

    // 标签与消息卡片同名，按容器区分
    const tabOf = (title: string) =>
      screen.getAllByText(title).map((x) => x.closest('.moai-chat__panel-tab')).find(Boolean) ?? null
    const cardOf = (title: string) =>
      screen.getAllByText(title).map((x) => x.closest('button.moai-chat__ui-tool')).find(Boolean) ?? null

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '画两个图' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))

    // 两个标签都出现，面板自动打开并展示最新一项（流量图表 line）
    await waitFor(() => {
      expect(tabOf('流量图表')).not.toBeNull()
    })
    expect(tabOf('销量图表')).not.toBeNull()
    expect(screen.getByTestId('echart-mock').textContent).toContain('"type":"line"')

    // 点击第一个标签：正文切到销量图表（bar）
    fireEvent.click(tabOf('销量图表') as HTMLElement)
    await waitFor(() => {
      expect(screen.getByTestId('echart-mock').textContent).toContain('"type":"bar"')
    })

    // 关闭当前标签（销量图表）：自动切回相邻标签（流量图表），标签移除
    fireEvent.click((tabOf('销量图表') as HTMLElement).querySelector('.moai-chat__panel-tab-close') as HTMLElement)
    await waitFor(() => {
      expect(screen.getByTestId('echart-mock').textContent).toContain('"type":"line"')
    })
    expect(tabOf('销量图表')).toBeNull()

    // 收起面板后再点消息卡片：剩余标签仍在并可展开
    fireEvent.click(screen.getByRole('button', { name: '关闭' }))
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).toBeNull()
    })
    fireEvent.click(cardOf('流量图表') as HTMLElement)
    await waitFor(() => {
      expect(document.querySelector('.moai-chat__panel')).not.toBeNull()
    })
    expect(tabOf('流量图表')).not.toBeNull()
  })

  it('头部返回按钮：返回应用市场（公开应用的非团队成员不落入团队应用列表）', async () => {
    renderPage()
    await waitFor(() => expect(screen.getAllByText('客服助手').length).toBeGreaterThanOrEqual(1))
    fireEvent.click(screen.getByLabelText('返回'))
    expect(await screen.findByTestId('apps-market')).toBeInTheDocument()
  })

  it('多会话并行：会话 A 流式中点「新对话」可立即发送新会话，A 不受影响继续流式', async () => {
    // 两轮对话均挂起（手动收尾），模拟长时间流式回复
    const finishers: Array<() => void> = []
    vi.mocked(runAppChat).mockImplementation(
      async () => new Promise<void>((resolve) => { finishers.push(resolve) }),
    )
    vi.mocked(createAppChatAgent).mockImplementation(() => ({ agent: crypto.randomUUID() }) as never)
    vi.mocked(createAppSession).mockResolvedValueOnce('s2').mockResolvedValueOnce('s3')
    // 侧栏随创建即时刷新：挂载 → 建会话 s2 → 建会话 s3 → A 收尾各一次
    const s1 = { sessionId: 's1', title: '第一段对话', lastMessageTime: '2026-09-11T10:00:00Z' }
    const s2 = { sessionId: 's2', title: '并行会话 A', lastMessageTime: '2026-10-08T10:00:00Z' }
    const s3 = { sessionId: 's3', title: '并行会话 B', lastMessageTime: '2026-10-08T10:01:00Z' }
    vi.mocked(getAppSessions)
      .mockResolvedValueOnce([s1] as never)
      .mockResolvedValueOnce([s1, s2] as never)
      .mockResolvedValueOnce([s1, s2, s3] as never)
      .mockResolvedValue([s1, s2, s3] as never)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    // 会话 A：发出第一条问题（流式挂起中）
    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '问题 A' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))
    await waitFor(() => expect(runAppChat).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(screen.getByText('并行会话 A')).toBeInTheDocument())

    // 流式中点「新对话」：输入卡立即可用（显示发送而非停止），可立即发送第二个会话
    fireEvent.click(screen.getByRole('button', { name: /新对话/ }))
    expect(screen.queryByRole('button', { name: '停止' })).not.toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '问题 B' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))
    await waitFor(() => expect(createAppSession).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(runAppChat).toHaveBeenCalledTimes(2))
    // 两个会话各自持有独立 agent（threadId 不同）
    expect(vi.mocked(createAppChatAgent).mock.calls[0][1]).toBe('s2')
    expect(vi.mocked(createAppChatAgent).mock.calls[1][1]).toBe('s3')

    // 并行进行中的会话在侧栏以呼吸点标识
    await waitFor(() => expect(screen.getByText('并行会话 B')).toBeInTheDocument())
    expect(document.querySelectorAll('.moai-chat__session.is-streaming').length).toBe(2)

    // A 收尾后：仅 B 保持流式标识
    finishers[0]()
    await waitFor(() => {
      expect(document.querySelectorAll('.moai-chat__session.is-streaming').length).toBe(1)
    })
    finishers[1]()
  })

  it('多会话并行：切回流式中的会话恢复实时现场（不回拉历史），停止只中止当前会话', async () => {
    const finishers: Array<() => void> = []
    const calls: Array<{ handlers: AgentChatHandlers }> = []
    vi.mocked(runAppChat).mockImplementation(async (_agent, _text, handlers) => {
      calls.push({ handlers })
      return new Promise<void>((resolve) => { finishers.push(resolve) })
    })
    vi.mocked(createAppChatAgent).mockImplementation(() => ({ agent: crypto.randomUUID() }) as never)
    vi.mocked(createAppSession).mockResolvedValueOnce('s2').mockResolvedValueOnce('s3')
    const s1 = { sessionId: 's1', title: '第一段对话', lastMessageTime: '2026-09-11T10:00:00Z' }
    const s2 = { sessionId: 's2', title: '并行会话 A', lastMessageTime: '2026-10-08T10:00:00Z' }
    const s3 = { sessionId: 's3', title: '并行会话 B', lastMessageTime: '2026-10-08T10:01:00Z' }
    vi.mocked(getAppSessions)
      .mockResolvedValueOnce([s1] as never)
      .mockResolvedValueOnce([s1, s2] as never)
      .mockResolvedValue([s1, s2, s3] as never)

    renderPage()
    await waitFor(() => expect(screen.getByText('第一段对话')).toBeInTheDocument())

    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '问题 A' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))
    await waitFor(() => expect(runAppChat).toHaveBeenCalledTimes(1))
    calls[0].handlers.onDelta?.('A 的部分回答')

    // 切到新对话发第二个会话 B，再切回流式中的 A
    fireEvent.click(screen.getByRole('button', { name: /新对话/ }))
    fireEvent.change(screen.getByPlaceholderText(/发消息给/), { target: { value: '问题 B' } })
    fireEvent.click(screen.getByRole('button', { name: '发送' }))
    await waitFor(() => expect(runAppChat).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(screen.getByText('并行会话 A')).toBeInTheDocument())

    fireEvent.click(screen.getByText('并行会话 A'))

    // 恢复 A 的实时现场：未回拉服务端历史，A 的提问与已到达内容在，B 的不在
    await waitFor(() => expect(screen.getByText('问题 A')).toBeInTheDocument())
    expect(screen.getByText('A 的部分回答')).toBeInTheDocument()
    expect(screen.queryByText('问题 B')).not.toBeInTheDocument()
    expect(getAppSessionMessages).not.toHaveBeenCalledWith('s2')

    // 切回后流继续推进（onDelta 持续更新 A 的现场）
    calls[0].handlers.onDelta?.('A 的完整回答')
    await waitFor(() => expect(screen.getByText('A 的完整回答')).toBeInTheDocument())

    // 停止只中止当前查看的会话 A（第一个 agent），后台 B 不受影响
    fireEvent.click(screen.getByRole('button', { name: '停止' }))
    await waitFor(() => expect(abortAppChat).toHaveBeenCalledTimes(1))
    expect(vi.mocked(abortAppChat).mock.calls[0][0]).toBe(vi.mocked(createAppChatAgent).mock.results[0].value)

    finishers[0]()
    finishers[1]()
  })
})
