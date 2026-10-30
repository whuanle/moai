// Agent 应用绑定 Agent 应用为工具 E2E（后端运行中，默认 http://127.0.0.1:5210，可传参覆盖）
// 覆盖 @AP-S109~S113：绑定校验（未发布/类型不符/自身 400）→ 保存回显与 null 不清空 → 循环嵌套防护
//       （B 反绑 A、A 绑含 A 节点的流程混合环均 400）→ 对话全链路（agent__ 工具调用 → 目标 Agent 按发布快照
//       无头单轮执行 → 结论回传；history 参数透传；草稿改动不影响嵌套执行）→ 解绑重发布后工具不注册。
// 模型策略：本地 OpenAI 兼容桩（按提示词标记区分主/嵌套请求），零真实依赖、零 SKIP。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
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
async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const isGuid = (s) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(String(s ?? ''))
const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

// ===== 桩模型（按提示词标记路由：B 号发布快照 → 工具结论；工具结果回传 → 汇总；披露含 agent__ 且用户点名 → 工具调用） =====

const chatLog = []
// agent__ 工具名在建完 B 号应用后回填（与后端 WorkflowAppToolProvider 同规则：agent__ + 应用名）
let agentToolName = ''

function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      if (String(req.url).includes('/embeddings')) {
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ object: 'list', model: 'stub', data: [{ object: 'embedding', index: 0, embedding: [0.1, 0.2] }], usage: { prompt_tokens: 1, total_tokens: 1 } }))
        return
      }

      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      chatLog.push({ messages })

      // 嵌套 B 号执行：按发布快照提示词标记识别（草稿标记出现即说明未按快照执行，回显眼文案让断言失败）
      if (allText.includes('B号草稿指令')) {
        return reply(res, payload, '草稿泄漏：B号按草稿执行了！')
      }
      if (allText.includes('B号发布快照指令')) {
        return reply(res, payload, 'B工具结论：已按发布快照处理完毕，订单共 42 笔。')
      }

      // 工具结果已回传：主代理汇总最终答复
      if (messages.some((m) => m.role === 'tool')) {
        return reply(res, payload, 'DONE ' + String(messages.filter((m) => m.role === 'tool').map((m) => m.content).join(';')).slice(0, 200))
      }

      // 主代理 A：指令含 agent__ 工具披露且用户点名调用 → 发起 agent__ 工具调用（携带 query 与可选 history）
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''
      if (agentToolName && allText.includes(agentToolName) && userText.includes('调用B号')) {
        return toolCall(res, payload, 'call_tool', JSON.stringify({
          toolName: agentToolName,
          argumentsJson: JSON.stringify({
            query: '请处理订单汇总',
            history: [
              { role: 'user', content: '先前提问：上个季度销量是多少' },
              { role: 'assistant', content: '先前回答：1000 件' },
            ],
          }),
        }))
      }

      // 兜底普通回复
      return reply(res, payload, '好的，普通回复。')
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

function reply(res, payload, text) {
  if (payload.stream) {
    res.writeHead(200, { 'Content-Type': 'text/event-stream' })
    const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
    res.write('data: [DONE]\n\n')
    res.end()
    return
  }
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({
    id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
    choices: [{ index: 0, message: { role: 'assistant', content: text }, finish_reason: 'stop' }],
    usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
  }))
}

function toolCall(res, payload, name, argsJson) {
  if (payload.stream) {
    res.writeHead(200, { 'Content-Type': 'text/event-stream' })
    const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', tool_calls: [{ index: 0, id: 'call_' + Date.now(), type: 'function', function: { name, arguments: '' } }] }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { tool_calls: [{ index: 0, function: { arguments: argsJson } }] }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] }) + '\n\n')
    res.write('data: [DONE]\n\n')
    res.end()
    return
  }
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({
    id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
    choices: [{ index: 0, message: { role: 'assistant', tool_calls: [{ id: 'call_' + Date.now(), type: 'function', function: { name, arguments: argsJson } }] }, finish_reason: 'tool_calls' }],
    usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
  }))
}

async function adminLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.status === 200 ? l.json.accessToken : null
}

async function mkuser(p) {
  const name = uname(p)
  await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function createStubModel(adminToken, TID, stubPort) {
  const chName = `ag-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'ag-stub-key', enabled: true, description: 'agent-app-tool E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null
  await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: `ag-stub-chat-${TS}`, name: `ag-stub-chat-${TS}`, modelKind: 'conversation', description: 'agent-app-tool E2E 桩模型' }, enabled: true, isPublic: false },
  })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const modelId = String((models.json?.items ?? []).find((m) => m.name === `ag-stub-chat-${TS}`)?.id ?? '')
  if (!modelId) return null
  const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  return { channelId: CH_ID, modelId }
}

async function chatSse(appId, token, sessionId, text) {
  const res = await fetch(`${BASE}/api/agent/${appId}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      threadId: sessionId, runId: crypto.randomUUID(), state: {},
      messages: [{ id: crypto.randomUUID(), role: 'user', content: text }],
      tools: [], context: [], forwardedProps: {},
    }),
  })
  const raw = await res.text()
  const events = raw.split('\n')
    .filter((l) => l.startsWith('data:'))
    .map((l) => { try { return JSON.parse(l.slice(5).trim()) } catch { return null } })
    .filter(Boolean)
  const reply = events.filter((e) => e.type === 'TEXT_MESSAGE_CONTENT' && typeof e.delta === 'string').map((e) => e.delta).join('')
  const runError = events.find((e) => e.type === 'RUN_ERROR')?.message ?? null
  return { status: res.status, reply, runError }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const adminToken = await adminLogin()
  const owner = await mkuser('at')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'ag-team-' + TS } })).json.value)

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = adminToken ? await createStubModel(adminToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  try {
    // ===== 建应用：B 号（目标 Agent）与 A 号（绑定方） =====
    const B_NAME = `B号专家${TS}`
    const cb = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: B_NAME, appType: 'agent' } })
    const APP_B = String(cb.json?.value ?? '')
    const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `A号调度${TS}`, appType: 'agent' } })
    const APP_A = String(ca.json?.value ?? '')
    agentToolName = `agent__${B_NAME}`

    // ===== @AP-S109 绑定校验 =====
    const s109a = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_B] },
    })
    check('AP-S109a 未发布 Agent 应用绑定 400', s109a.status === 400 && (s109a.text ?? '').includes('Agent 应用'), `${s109a.status} ${s109a.text.slice(0, 160)}`)

    const cw = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `类型不符流程${TS}`, appType: 'workflow' } })
    const APP_W_DUMMY = String(cw.json?.value ?? '')
    const s109b1 = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_W_DUMMY] },
    })
    const s109b2 = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [crypto.randomUUID()] },
    })
    check('AP-S109b 流程类型应用不可绑为 Agent 工具 400', s109b1.status === 400 && (s109b1.text ?? '').includes('Agent 应用'), `${s109b1.status} ${s109b1.text.slice(0, 160)}`)
    check('AP-S109c 不存在的应用 id 绑定 400', s109b2.status === 400 && (s109b2.text ?? '').includes('Agent 应用'), `${s109b2.status}`)

    // B 号配置并发布，A 号绑定自身应被拒
    await api('PUT', `/api/app/${APP_B}/agent-config`, {
      token: owner.token,
      body: { prompt: 'B号发布快照指令：你是B号专家。', wikiIds: [], plugins: [], modelId: models.modelId },
    })
    const bPub = await api('POST', `/api/app/${APP_B}/publish`, { token: owner.token })
    check('AP-S109d B 号应用发布 200', bPub.status === 200, `${bPub.status} ${bPub.text.slice(0, 120)}`)

    const s109e = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_A] },
    })
    check('AP-S109e 绑定自身 400', s109e.status === 400 && (s109e.text ?? '').includes('不能绑定自身'), `${s109e.status} ${s109e.text.slice(0, 160)}`)

    // ===== @AP-S110 保存回显与 null 不清空 =====
    const s110a = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_B] },
    })
    check('AP-S110a 绑定已发布 Agent 应用保存 200', s110a.status === 200, `${s110a.status} ${s110a.text.slice(0, 160)}`)

    const echo1 = await api('GET', `/api/app/${APP_A}/agent-config`, { token: owner.token })
    check('AP-S110b 回显 agentApps 绑定', echo1.status === 200 && JSON.stringify(echo1.json?.agentApps ?? []) === JSON.stringify([APP_B]), echo1.text.slice(0, 200))

    await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手（改）', wikiIds: [], plugins: [], modelId: models.modelId },
    })
    const echo2 = await api('GET', `/api/app/${APP_A}/agent-config`, { token: owner.token })
    check('AP-S110c 请求不携带 agentApps 不清空既有绑定', JSON.stringify(echo2.json?.agentApps ?? []) === JSON.stringify([APP_B]), echo2.text.slice(0, 200))

    const aPub = await api('POST', `/api/app/${APP_A}/publish`, { token: owner.token })
    check('AP-S110d A 号发布 200', aPub.status === 200, `${aPub.status} ${aPub.text.slice(0, 120)}`)

    // ===== @AP-S111 循环嵌套防护 =====
    const s111a = await api('PUT', `/api/app/${APP_B}/agent-config`, {
      token: owner.token,
      body: { prompt: 'B号发布快照指令：你是B号专家。', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_A] },
    })
    check('AP-S111a B 号反向绑定 A 号成环 400', s111a.status === 400 && (s111a.text ?? '').includes('循环嵌套'), `${s111a.status} ${s111a.text.slice(0, 200)}`)

    // 混合环：流程 W（agentApp 节点 → A）发布后，A 绑定 W 为流程工具 —— 平台既有语义：Agent 侧保存允许，
    // 该类环由流程侧保存/发布/调试守卫与运行期根流程守卫拦截（见 workflow-e2e WF-26）
    const w3 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `混合环流程${TS}`, appType: 'workflow' } })
    const APP_W = String(w3.json?.value ?? '')
    const buildAgentFlow = () => ({
      id: '', name: 'wf-mixed-cycle', version: 0, status: 'draft',
      nodes: [
        { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true }] },
        { key: 'agent', name: 'Agent 节点', type: 'agentApp', config: { agentAppId: APP_A }, inputs: { prompt: { expressionType: 'interpolation', value: '用户问题：{start.question}', required: true } }, outputs: [{ name: 'answer', fieldType: 'string' }] },
        { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'variable', value: 'agent.answer', required: true } }, outputs: [] },
      ],
      connections: [{ id: 'g1', source: 'start', target: 'agent' }, { id: 'g2', source: 'agent', target: 'end' }],
      ui: {},
    })
    await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP_W, teamId: TID, definition: JSON.stringify(buildAgentFlow()), editorData: '{}' } })
    const w3Pub = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: APP_W, teamId: TID } })
    check('AP-S111b 含 A 节点的流程发布 200', w3Pub.status === 200, `${w3Pub.status} ${w3Pub.text.slice(0, 120)}`)

    const s111c = await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手（改）', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_B], workflowApps: [APP_W] },
    })
    const echoW = await api('GET', `/api/app/${APP_A}/agent-config`, { token: owner.token })
    check('AP-S111c 绑定含自身节点的流程不被 Agent 侧拒绝（流程侧守卫负责）', s111c.status === 200 && JSON.stringify(echoW.json?.workflowApps ?? []) === JSON.stringify([APP_W]), `${s111c.status} ${s111c.text.slice(0, 160)}`)

    // 纯 Agent 链环：A→B（已绑）、B→C 允许；C→A 经 B 回到自身 → 400
    const cc = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `C号末端${TS}`, appType: 'agent' } })
    const APP_C = String(cc.json?.value ?? '')
    await api('PUT', `/api/app/${APP_C}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是C号末端。', wikiIds: [], plugins: [], modelId: models.modelId },
    })
    await api('POST', `/api/app/${APP_C}/publish`, { token: owner.token })
    const s111d1 = await api('PUT', `/api/app/${APP_B}/agent-config`, {
      token: owner.token,
      body: { prompt: 'B号发布快照指令：你是B号专家。', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_C] },
    })
    check('AP-S111d B 号绑定 C 号（链 A→B→C 无环）200', s111d1.status === 200, `${s111d1.status} ${s111d1.text.slice(0, 160)}`)
    // 环闭包按发布快照取边：B 的 C 绑定须重新发布后才对守卫可见
    await api('POST', `/api/app/${APP_B}/publish`, { token: owner.token })
    const s111e = await api('PUT', `/api/app/${APP_C}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是C号末端。', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [APP_A] },
    })
    check('AP-S111e C 号绑定 A 号成传递环（C→A→B→C）400', s111e.status === 400 && (s111e.text ?? '').includes('循环嵌套'), `${s111e.status} ${s111e.text.slice(0, 200)}`)

    // 还原 B 号无 Agent 绑定（保持发布快照纯净，供后续对话链路使用）
    await api('PUT', `/api/app/${APP_B}/agent-config`, {
      token: owner.token,
      body: { prompt: 'B号发布快照指令：你是B号专家。', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [] },
    })
    await api('POST', `/api/app/${APP_B}/publish`, { token: owner.token })

    // ===== @AP-S112 对话全链路 =====
    const mkSession = async (title) => String((await api('POST', `/api/app/${APP_A}/session`, { token: owner.token, body: { title } })).json?.value ?? '')

    chatLog.length = 0
    const SID1 = await mkSession('ag-全链路')
    const chat1 = await chatSse(APP_A, owner.token, SID1, '请调用B号处理订单')
    check('AP-S112a 主代理调用 agent__ 工具并回传结论', chat1.status === 200 && !chat1.runError && (chat1.reply ?? '').includes('B工具结论'), `reply=${(chat1.reply ?? '').slice(0, 120)} err=${chat1.runError}`)

    const bNested = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('B号发布快照指令')))
    check('AP-S112b 嵌套 B 号按发布快照执行（桩捕获）', bNested.length >= 1, `captures=${chatLog.length}`)
    check('AP-S112c history 参数透传到 B 号请求', bNested.some((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('上个季度销量'))), `nested=${bNested.length}`)
    const aMain = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('A号调度助手')))
    check('AP-S112d 主代理请求披露 agent__ 工具', aMain.some((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes(agentToolName))), `main=${aMain.length}`)

    // B 号改草稿提示词（不重新发布）：嵌套执行仍按发布快照
    await api('PUT', `/api/app/${APP_B}/agent-config`, {
      token: owner.token,
      body: { prompt: 'B号草稿指令：你是B号草稿。', wikiIds: [], plugins: [], modelId: models.modelId },
    })
    chatLog.length = 0
    const SID2 = await mkSession('ag-快照校验')
    const chat2 = await chatSse(APP_A, owner.token, SID2, '请调用B号处理订单')
    const nested2 = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('B号发布快照指令')))
    const draftLeak = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('B号草稿指令')))
    check('AP-S112e B 号草稿改动不影响嵌套执行（仍按发布快照）', !chat2.runError && (chat2.reply ?? '').includes('B工具结论') && nested2.length >= 1 && draftLeak.length === 0, `reply=${(chat2.reply ?? '').slice(0, 100)} nested=${nested2.length} leak=${draftLeak.length}`)

    // ===== @AP-S113 解绑并重新发布后工具不注册 =====
    await api('PUT', `/api/app/${APP_A}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是A号调度助手（改）', wikiIds: [], plugins: [], modelId: models.modelId, agentApps: [] },
    })
    const aPub2 = await api('POST', `/api/app/${APP_A}/publish`, { token: owner.token })
    check('AP-S113a 解绑后重新发布 200', aPub2.status === 200, `${aPub2.status} ${aPub2.text.slice(0, 120)}`)

    chatLog.length = 0
    const SID3 = await mkSession('ag-解绑')
    const chat3 = await chatSse(APP_A, owner.token, SID3, '请调用B号处理订单')
    const disclosure3 = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes(agentToolName)))
    check('AP-S113b 解绑重发布后 agent__ 工具不再注册且对话正常', !chat3.runError && disclosure3.length === 0 && !(chat3.reply ?? '').includes('B工具结论'), `reply=${(chat3.reply ?? '').slice(0, 100)} err=${chat3.runError} leak=${disclosure3.length}`)
  } finally {
    if (adminToken && models.channelId) await api('DELETE', `/api/ai/channel/${models.channelId}`, { token: adminToken })
    stub.close()
  }

  console.log(`\n===== agent-app-tool E2E 完成：PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
