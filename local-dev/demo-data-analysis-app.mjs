// 商业数据分析演示应用种子脚本（DEMO，可重复执行——每次运行新建一套团队/智能体/应用）
// 产物：团队「商业数据分析演示团队」+ 团队智能体「商业数据分析组」（1 主智能体 + 5 子智能体）
//       + Agent 应用「商业数据分析助手」（绑定智能体、开启子代理协作、开场白与快捷输入、已发布）
// 结尾做一轮真实 SSE 对话冒烟（派发子智能体），验证全链路后输出访问入口。
// 用法：node local-dev/demo-data-analysis-app.mjs [serverUrl]   （默认 http://127.0.0.1:5000）
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
async function api(method, path, { token, body, headers } = {}) {
  const h = { ...(headers ?? {}) }
  if (body !== undefined) h['Content-Type'] = 'application/json'
  if (token) h['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)

// ===== 桩渠道保底：环境无真实对话模型时创建本地桩（回复含演示标记，可区分） =====
let stubServer = null
function startStubServer() {
  stubServer = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      const send = (text) => {
        if (payload.stream) {
          res.writeHead(200, { 'Content-Type': 'text/event-stream' })
          const base = { id: 'chatcmpl-demo-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] }) + '\n\n')
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
          res.write('data: [DONE]\n\n')
          res.end()
          return
        }
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          id: 'chatcmpl-demo-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
          choices: [{ index: 0, message: { role: 'assistant', content: text }, finish_reason: 'stop' }],
          usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
        }))
      }
      // 子代理请求：回一段固定结论（演示过程流可见）
      if (allText.includes('[子代理模式]')) { send('【桩演示】子智能体结论：指标口径已核对，Q3 营收环比 +12.4%，增长主要由华东渠道贡献。'); return }
      // 主代理第一轮：演示模式——派发「趋势分析师」子智能体走全链路
      if (messages.some((m) => m.role === 'user') && !messages.some((m) => m.role === 'tool')) {
        const tools = payload.tools ?? []
        const hasSubTool = JSON.stringify(tools).includes('subagent_') || allText.includes('subagent_')
        if (hasSubTool) {
          const target = allText.includes('subagent_trend') ? 'subagent_trend' : (JSON.stringify(tools).match(/subagent_[a-z_]+/) ?? ['subagent_trend'])[0]
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify({
            id: 'chatcmpl-demo-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
            choices: [{ index: 0, message: { role: 'assistant', tool_calls: [{ id: 'call_' + Date.now(), type: 'function', function: { name: 'call_tool', arguments: JSON.stringify({ toolName: target, argumentsJson: JSON.stringify({ task: '分析 Q3 经营数据的趋势与异常点', guidance: '输出结论与关键数字' }) }) } }] }, finish_reason: 'tool_calls' }],
            usage: { prompt_tokens: 20, completion_tokens: 10, total_tokens: 40 },
          }))
          return
        }
      }
      send('【桩演示】分析摘要：Q3 营收环比 +12.4%，华东渠道为主要增长引擎；建议关注华南转化率下滑风险。')
    })
  })
  return new Promise((resolve) => stubServer.listen(0, '127.0.0.1', () => resolve(stubServer)))
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  // admin 登录（root）
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (l.status !== 200) { console.log('FAIL | admin 登录失败，终止', l.status); process.exit(1) }
  const adminToken = l.json.accessToken

  // ===== 1. 团队（创建即自动种入内置规划师/构建师） =====
  const TID = Number((await api('POST', '/api/team', { token: adminToken, body: { name: '商业数据分析演示团队-' + TS } })).json.value)
  check('团队创建', TID > 0, String(TID))

  // ===== 2. 团队智能体：商业数据分析组（1 主 + 5 子） =====
  const analystAgent = await api('POST', '/api/team-agent', {
    token: adminToken,
    body: {
      teamId: TID,
      key: 'analyst',
      name: '商业数据分析组',
      description: '商业数据分析虚拟团队：总监拆解问题并派发子智能体协作，产出结论先行的结构化分析报告',
      mainPrompt: [
        '你是「商业数据分析组」的数据分析总监，直接面向业务方回答经营分析问题。',
        '',
        '工作方式：',
        '1. 先判断问题类型（指标拆解 / 数据核查 / 趋势归因 / 可视化呈现 / 综合报告），按需派发给对应子智能体，不要事事都派发——单点小问题直接回答；',
        '2. 复合问题（如"分析 Q3 销售下滑原因并给出建议"）按 指标拆解 → 趋势分析 →（需要时）可视化 串行派发，每次委派一句明确、可独立完成的任务；',
        '3. 子智能体只回传结论，由你核对口径、交叉验证后整合，不要原样堆砌子智能体输出；',
        '4. 数字必须给出口径与时间范围；数据缺失时明确说"缺少数据"并给获取建议，不要编造精确数字；',
        '5. 最终答复用结论先行的结构化报告：**结论** → **关键发现**（3 条以内，带数字）→ **建议动作** → **风险与未决问题**。',
      ].join('\n'),
      subs: [
        {
          key: 'metrics',
          name: '指标拆解师',
          description: '把模糊的业务问题拆解为可量化的指标体系（北极星指标、过程指标、分析维度），明确口径与时间范围',
          prompt: [
            '你是指标拆解师。输入一个业务问题，输出：',
            '1. 北极星指标（1 个，含口径与时间范围）；',
            '2. 过程指标（3~5 个，说明与北极星的驱动关系）；',
            '3. 建议的分析维度（渠道/区域/品类/客群等）与优先级；',
            '4. 数据需求清单（回答该问题最少需要哪些数据）。',
            '只输出拆解结果，不做趋势判断。',
          ].join('\n'),
        },
        {
          key: 'trend',
          name: '趋势分析师',
          description: '对给定指标与数据做趋势、同比环比、异常点检测与归因假设，输出量化洞察',
          prompt: [
            '你是趋势分析师。对给定的指标/数据做分析，输出：',
            '1. 趋势判断（上行/下行/震荡，幅度与拐点）；',
            '2. 同比/环比拆解与贡献度排序（哪个维度驱动最大）；',
            '3. 异常点清单（时间点、偏离幅度、可能原因假设，区分"确认事实"与"待验证假设"）；',
            '4. 置信度说明（数据不足的结论必须标注）。',
            '没有数据支撑的判断必须明说，禁止编造精确数字。',
          ].join('\n'),
        },
        {
          key: 'viz',
          name: '可视化顾问',
          description: '为分析结论选择合适的图表形态并给出图表建议（趋势用折线、构成用占比、对比用柱状等），必要时给出展示配置要点',
          prompt: [
            '你是可视化顾问。输入分析结论或数据表，输出 1~3 个图表建议：',
            '1. 图表类型与选择理由（趋势→折线、构成→堆叠/占比、对比→分组柱状、相关性→散点）；',
            '2. 每个图的维度、指标、时间范围与关键标注点（如异常拐点）；',
            '3. 一句话的图表标题（业务视角，不看数也能懂结论）。',
            '只给建议与要点，不写实现代码。',
          ].join('\n'),
        },
        {
          key: 'competitor',
          name: '行业情报员',
          description: '围绕业务问题检索行业动态、竞品动作与市场基准，输出对标的参照系与外部风险提示',
          prompt: [
            '你是行业情报员。围绕给定的业务问题输出：',
            '1. 该问题的行业参照系（常见基准值/行业惯例区间，注明是经验值）；',
            '2. 竞品或同类业务的典型打法（2~3 条，注明来源类型：公开报道/行业报告/常识推断）；',
            '3. 外部风险提示（政策/季节性/供应链等）。',
            '信息不确定时标注置信度，禁止把推断说成事实。',
          ].join('\n'),
        },
        {
          key: 'writer',
          name: '报告撰写员',
          description: '把各子智能体的结论整合为面向管理层的结构化分析报告（摘要/关键发现/建议/风险），语言精炼、结论先行',
          prompt: [
            '你是报告撰写员。输入是多个子智能体的分析结论，输出一份面向管理层的报告：',
            '## 摘要（3 句话内，结论先行）',
            '## 关键发现（≤3 条，每条带关键数字与口径）',
            '## 建议动作（≤3 条，可执行、有负责人视角）',
            '## 风险与未决问题（数据缺口与待验证假设）',
            '要求：不重复罗列各子智能体原文，交叉核对口径冲突并取一致表述；总长 ≤600 字。',
          ].join('\n'),
        },
      ],
    },
  })
  const AGENT_ID = String(analystAgent.json?.value ?? '')
  check('团队智能体「商业数据分析组」创建（1 主 + 5 子）', analystAgent.status === 200 && !!AGENT_ID, analystAgent.text.slice(0, 120))

  // ===== 3. 对话模型：优先复用环境已有 conversation 模型并授权本团队；否则建本地桩保底 =====
  const pickExistingModel = async () => {
    const channels = await api('GET', '/api/ai/channel', { token: adminToken })
    for (const ch of channels.json?.items ?? []) {
      if (ch.enabled === false) continue
      const models = await api('GET', `/api/ai/model?channelId=${ch.id}`, { token: adminToken })
      const conv = (models.json?.items ?? []).find((m) => m.modelKind === 'conversation' && m.enabled !== false)
      if (conv) {
        // 渠道级批量授权：把本团队并入授权列表（幂等，公开模型不受影响）
        const cur = await api('GET', `/api/ai/model/${conv.id}/authorization`, { token: adminToken })
        if (cur.status === 200) {
          const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
          await api('PUT', `/api/ai/model/${conv.id}/authorization`, { token: adminToken, body: { modelId: conv.id, teamIds } })
        }
        return { modelId: String(conv.id), name: conv.name, stubbed: false }
      }
    }
    return null
  }

  let model = await pickExistingModel()
  if (!model) {
    console.log('LOG | 环境无可用 conversation 模型，创建本地桩渠道保底（回复为演示文本，不影响链路效果）')
    const stub = await startStubServer()
    const stubPort = stub.address().port
    const chName = `演示桩渠道-${TS}`
    await api('POST', '/api/ai/channel', {
      token: adminToken,
      body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'demo-stub-key', enabled: true, description: '商业数据分析演示保底桩渠道' },
    })
    const channels = await api('GET', '/api/ai/channel', { token: adminToken })
    const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
    await api('POST', '/api/ai/model', {
      token: adminToken,
      body: { channelId: CH_ID, meta: { modelId: `demo-analysis-chat-${TS}`, name: `演示桩模型-${TS}`, modelKind: 'conversation', description: '商业数据分析演示保底桩模型' }, enabled: true, isPublic: false },
    })
    const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
    const modelId = String((models.json?.items ?? []).find((m) => m.name === `演示桩模型-${TS}`)?.id ?? '')
    const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
    model = { modelId, name: `演示桩模型-${TS}`, stubbed: true }
  }
  check('对话模型就绪（' + model.name + (model.stubbed ? '，桩保底' : '，复用环境模型') + '）', !!model.modelId)

  // ===== 4. Agent 应用：商业数据分析助手 =====
  const APP_ID = String((await api('POST', '/api/app', { token: adminToken, body: { teamId: TID, name: '商业数据分析助手', appType: 'agent', description: '面向经营团队的商业数据分析应用：团队智能体协作 + 子智能体派发 + 过程实时可视化' } })).json.value ?? '')
  check('应用创建', !!APP_ID)

  const appPrompt = [
    '你是「商业数据分析助手」，服务经营团队的日常分析与汇报场景。',
    '',
    '通用约束：',
    '- 涉及数字时给出口径与时间范围；缺失数据时明确说明并给获取路径；',
    '- 涉及图表时优先调用前端展示工具 ui_show_chart 呈现（趋势折线/对比柱状/构成占比），再附文字解读；',
    '- 涉及文档产出时优先调用 ui_show_document 呈现；',
    '- 回答始终结论先行，管理层视角，避免堆过程。',
  ].join('\n')

  const save = await api('PUT', `/api/app/${APP_ID}/agent-config`, {
    token: adminToken,
    body: {
      prompt: appPrompt,
      wikiIds: [],
      graphIds: [],
      plugins: [],
      modelId: model.modelId,
      teamAgentIds: [AGENT_ID],
      defaultTeamAgentId: AGENT_ID,
      executionSettings: { subAgent: { enabled: true, timeoutSeconds: 300, resultMaxChars: 8000 } },
      openingStatement: [
        '## 👋 我是商业数据分析助手',
        '',
        '我背后是一个数据分析虚拟团队：**指标拆解师 → 趋势分析师 → 可视化顾问 → 行业情报员 → 报告撰写员**，按需协作回答你的经营问题。',
        '',
        '**试试这些：**',
        '- 分析 Q3 销售环比下滑的原因，并给出下季度建议',
        '- 把「会员复购率下降」拆解成指标体系',
        '- 为「各渠道 GMV 对比」推荐可视化方案',
      ].join('\n'),
      openingStatementEnabled: true,
      quickInputs: [
        '分析 Q3 销售环比下滑的原因，并给出下季度建议',
        '把「会员复购率下降」拆解成指标体系',
        '为「各渠道 GMV 对比」推荐可视化方案',
        '输出一份 Q4 经营分析报告框架',
      ],
    },
  })
  check('应用配置（绑定智能体 + 开启子代理协作 + 开场白/快捷输入）', save.status === 200, save.text.slice(0, 160))

  // ===== 5. 发布上线 =====
  const pub = await api('POST', `/api/app/${APP_ID}/publish`, { token: adminToken })
  check('应用发布（配置冻结进快照）', pub.status === 200, pub.text.slice(0, 120))

  // ===== 6. 真实 SSE 对话冒烟（派发子智能体全链路 + moai.subagent 过程事件） =====
  const sess = String((await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: '演示冒烟-Q3 分析' } })).json.value ?? '')
  const chatRes = await fetch(`${BASE}/api/agent/${APP_ID}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({
      threadId: sess, runId: crypto.randomUUID(), state: {},
      messages: [{ id: crypto.randomUUID(), role: 'user', content: '分析 Q3 经营数据的趋势与异常点' }],
      tools: [], context: [], forwardedProps: {},
    }),
  })
  const raw = await chatRes.text()
  const events = raw.split('\n').filter((x) => x.startsWith('data:')).map((x) => { try { return JSON.parse(x.slice(5).trim()) } catch { return null } }).filter(Boolean)
  const reply = events.filter((e) => e.type === 'TEXT_MESSAGE_CONTENT' && typeof e.delta === 'string').map((e) => e.delta).join('')
  const subEvents = events.filter((e) => e.type === 'CUSTOM' && e.name === 'moai.subagent').map((e) => e.value?.kind)
  const runError = events.find((e) => e.type === 'RUN_ERROR')?.message ?? null
  check('对话冒烟：SSE 回复', !runError && (reply ?? '').length > 0, `err=${runError} reply=${(reply ?? '').slice(0, 80)}`)
  check('对话冒烟：moai.subagent 过程事件（started→…→completed）', subEvents.includes('started') && subEvents.includes('completed'), JSON.stringify(subEvents))

  // ===== 输出入口 =====
  console.log('\n===== 商业数据分析演示应用就绪 =====')
  console.log(`团队 id        : ${TID}（商业数据分析演示团队-${TS}，另含内置 规划师/构建师）`)
  console.log(`团队智能体 id  : ${AGENT_ID}（商业数据分析组：总监 + 指标拆解师/趋势分析师/可视化顾问/行业情报员/报告撰写员）`)
  console.log(`应用 id        : ${APP_ID}（商业数据分析助手，已发布）`)
  console.log(`对话模型       : ${model.name}${model.stubbed ? '（本地桩保底，回复为演示文本）' : '（复用环境已有模型）'}`)
  console.log(`前端入口       : 前端开发服务（npm run dev，默认 http://localhost:4000）→ 团队「商业数据分析演示团队-${TS}」→ 应用「商业数据分析助手」进入工作台「对话」`)
  console.log(`冒烟会话       : 会话 id ${sess} 已生成一轮「Q3 趋势分析」对话（含子智能体过程），打开对话页即可看到嵌套活动卡效果`)

  if (stubServer) stubServer.close()
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('DEMO 异常终止:', e)
  process.exit(1)
})
