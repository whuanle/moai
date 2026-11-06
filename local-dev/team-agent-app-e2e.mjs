// Agent 应用绑定团队智能体 E2E（team-agent Phase 2：应用绑定与运行时派发，后端 127.0.0.1:5000）
// 覆盖：配置保存校验（默认不在列表 400 / 伪造与停用智能体 400 / 主依赖越界 400 / 外部应用 400；绑定不开协作 200——D53 解耦）
//       → 保存回读与不携带不清空 → 配置回显与 userconfig 切换器数据 → 发布快照内嵌定义（发布后改智能体不影响线上）
//       → 运行时：默认智能体主提示词注入 + 子智能体名册（[可派发子智能体]，D53）+ 具名 subagent_{key} 工具披露 + X-Moai-Team-Agent-Id 切换与非法头回退
//       → 具名派发全链路（子请求注入子智能体提示词、声明即边界工具白名单、深度恒 1、结论回传）
//       → 子智能体过程实时事件 moai.subagent（started/text/completed 序列与实时性断言，D53）
//       → 绑定不开协作的运行时语义（[团队智能体] 标签注入 + 禁止派发文案 + 零派发工具 + 零过程事件，D53）。
// 模型策略：本地 OpenAI 兼容桩（提示词条件行为），零 SKIP。
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
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

// ===== 桩模型 =====

const chatLog = []

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
      chatLog.push({ messages, allText })

      // 具名子智能体请求：指令带 [子代理模式]，直接回结论（绝不再发起工具调用）
      if (allText.includes('[子代理模式]')) {
        return reply(res, payload, '扫描结论：目标共 42 项，全部通过。')
      }

      // 工具结果已回传：主代理汇总最终答复
      if (messages.some((m) => m.role === 'tool')) {
        return reply(res, payload, 'DONE ' + String(messages.filter((m) => m.role === 'tool').map((m) => m.content).join(';')).slice(0, 200))
      }

      // 主代理：披露含具名工具 subagent_scanner 且用户要求派发 → 发起具名委派
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''
      if (allText.includes('subagent_scanner') && userText.includes('派发扫描')) {
        return toolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'subagent_scanner', argumentsJson: JSON.stringify({ task: '扫描目标清单', guidance: '只输出结论' }) }))
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
  const chName = `taa-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'taa-stub-key', enabled: true, description: 'team-agent-app E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null
  await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: `taa-stub-chat-${TS}`, name: `taa-stub-chat-${TS}`, modelKind: 'conversation', description: 'team-agent-app E2E 桩模型' }, enabled: true, isPublic: false },
  })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const modelId = String((models.json?.items ?? []).find((m) => m.name === `taa-stub-chat-${TS}`)?.id ?? '')
  if (!modelId) return null
  const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  return { channelId: CH_ID, modelId }
}

async function chatSse(appId, token, sessionId, text, extraHeaders = {}) {
  const res = await fetch(`${BASE}/api/agent/${appId}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...extraHeaders },
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
  // 子智能体过程自定义事件（moai.subagent，D53）：保留完整事件序列供顺序断言
  const customEvents = events.filter((e) => e.type === 'CUSTOM' && typeof e.name === 'string' && e.name.startsWith('moai.')).map((e) => ({ name: e.name, value: e.value ?? {} }))
  const subAgentEvents = customEvents.filter((e) => e.name === 'moai.subagent')
  return { status: res.status, reply, runError, customEvents, subAgentEvents, eventTypes: events.map((e) => e.type) }
}

// 桩捕获请求的 system 指令（MAF ChatOptions.Instructions 以 system 消息下发）
const sysText = (r) => r.messages.filter((m) => m.role === 'system').map((m) => (typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? ''))).join('\n')
const isSubRequest = (r) => r.allText.includes('[子代理模式]')

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const adminToken = await adminLogin()
  const owner = await mkuser('ta')
  // 团队创建即自动种入系统内置智能体（规划师 plan / 构建师 build）
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'taa-team-' + TS } })).json.value)

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = adminToken ? await createStubModel(adminToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  // 知识库 W1：App1 绑定后主代理披露知识库检索工具（scanner 未声明 → 白名单收缩对照）
  const W1 = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'taa-wiki-' + TS } })).json.value)

  // 自定义智能体 C1（主提示词 V1 标记 + 子智能体 scanner）与 C2（主依赖声明 W1）、C3（停用用）
  const C1 = String((await api('POST', '/api/team-agent', {
    token: owner.token,
    body: {
      teamId: TID, key: 'builder', name: '构建器-' + TS, description: 'E2E 构建智能体',
      mainPrompt: '你是构建器。BUILDER_MAIN_V1 标记。收到目标后派发子智能体完成。',
      subs: [{ key: 'scanner', name: '扫描器', description: '对目标做逐项扫描核对', prompt: '你是扫描器。SCANNER_SUB_V1 标记。逐项扫描并输出结论。' }],
    },
  })).json.value ?? '')
  const C2 = String((await api('POST', '/api/team-agent', {
    token: owner.token,
    body: { teamId: TID, key: 'depcheck', name: '依赖检查-' + TS, mainPrompt: '依赖检查主提示词', mainWikiIds: [W1] },
  })).json.value ?? '')
  const C3 = String((await api('POST', '/api/team-agent', {
    token: owner.token,
    body: { teamId: TID, key: 'offagent', name: '停用体-' + TS, mainPrompt: '停用主提示词', enabled: true },
  })).json.value ?? '')
  await api('PUT', `/api/team-agent/${C3}`, { token: owner.token, body: { key: 'offagent', name: '停用体-' + TS, mainPrompt: '停用主提示词', enabled: false, subs: [] } })

  const listRes = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: owner.token })
  const PLAN = String((listRes.json?.items ?? []).find((a) => a.key === 'plan')?.id ?? '')
  if (!C1 || !PLAN) { console.log('FAIL | 团队智能体准备失败，终止'); process.exit(1) }

  try {
    // ===== 应用与配置保存校验 =====
    const mkApp = async (name, isExternal = false) => String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name, appType: 'agent', isExternal } })).json.value ?? '')

    const APP1 = await mkApp('多智能体应用-' + TS)
    const APP2 = await mkApp('单智能体应用-' + TS)
    const APPX = await mkApp('外部应用-' + TS, true)
    const baseSettings = { subAgent: { enabled: true, timeoutSeconds: 120, resultMaxChars: 4000 } }

    // TAA-01 绑定团队智能体但未开子代理协作 → 200（D53 解耦：绑定只代表主智能体提示词生效，不再 400）
    const r01 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C1], executionSettings: { subAgent: { enabled: false } } },
    })
    check('TAA-01 未开子代理协作绑定 200（D53 解耦）', r01.status === 200, `${r01.status} ${r01.text.slice(0, 80)}`)
    const back01 = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('TAA-01b 解耦绑定回读不清空', Array.isArray(back01.json?.teamAgentIds) && back01.json.teamAgentIds.length === 1, back01.text.slice(0, 160))

    // TAA-02 默认智能体不在列表 → 400
    const r02 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C1], defaultTeamAgentId: PLAN, executionSettings: baseSettings },
    })
    check('TAA-02 默认智能体不在列表 400', r02.status === 400, `${r02.status} ${r02.text.slice(0, 80)}`)

    // TAA-03 伪造智能体 id → 400
    const r03 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [crypto.randomUUID()], executionSettings: baseSettings },
    })
    check('TAA-03 伪造智能体 400', r03.status === 400, `${r03.status} ${r03.text.slice(0, 80)}`)

    // TAA-04 停用智能体 → 400
    const r04 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C3], executionSettings: baseSettings },
    })
    check('TAA-04 停用智能体 400', r04.status === 400, `${r04.status} ${r04.text.slice(0, 80)}`)

    // TAA-05 主依赖缺失不阻断（弱依赖，team-agent sdd D3）：保存 200 且声明项随选项回显供前端「一键添加」
    const r05 = await api('PUT', `/api/app/${APP2}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C2], executionSettings: baseSettings },
    })
    check('TAA-05 主依赖缺失不阻断（弱依赖 200）', r05.status === 200, `${r05.status} ${r05.text.slice(0, 80)}`)
    const back05 = await api('GET', `/api/app/${APP2}/agent-config`, { token: owner.token })
    check('TAA-05b 弱依赖声明随选项回显', String((back05.json?.teamAgents ?? [])[0]?.mainWikiIds?.[0]) === String(W1), JSON.stringify({ w1: W1, agents: back05.json?.teamAgents }))

    // TAA-06 外部应用绑定 → 400
    const r06 = await api('PUT', `/api/app/${APPX}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C1], executionSettings: baseSettings },
    })
    check('TAA-06 外部应用绑定 400', r06.status === 400, `${r06.status} ${r06.text.slice(0, 80)}`)

    // TAA-07 多选保存 200 + 回读（默认=C1）
    const r07 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [W1], plugins: [], modelId: models.modelId, teamAgentIds: [C1, PLAN], defaultTeamAgentId: C1, executionSettings: baseSettings },
    })
    check('TAA-07 多选保存 200', r07.status === 200, `${r07.status} ${r07.text.slice(0, 120)}`)
    const back = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('TAA-08 配置回读 teamAgentIds/默认', Array.isArray(back.json?.teamAgentIds) && back.json.teamAgentIds.length === 2
      && back.json.defaultTeamAgentId === C1, back.text.slice(0, 200))
    check('TAA-09 配置回读 teamAgents 选项', (back.json?.teamAgents ?? []).length === 2
      && back.json.teamAgents.some((a) => a.key === 'builder') && back.json.teamAgents.some((a) => a.key === 'plan'), back.text.slice(0, 200))

    // TAA-10 不携带 teamAgentIds 再保存不清空
    await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [W1], plugins: [], modelId: models.modelId, executionSettings: baseSettings },
    })
    const back2 = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('TAA-10 不携带不清空', (back2.json?.teamAgentIds ?? []).length === 2, back2.text.slice(0, 160))

    // TAA-11 userconfig 切换器数据（已发布口径，先发布）
    check('TAA-11 发布 200', (await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })).status === 200)
    const uc = await api('GET', `/api/app/${APP1}/userconfig`, { token: owner.token })
    check('TAA-12 userconfig 回显双智能体与默认', (uc.json?.teamAgents ?? []).length === 2 && uc.json?.defaultTeamAgentId === C1
      && uc.json.teamAgents.every((a) => a.id && a.name), uc.text.slice(0, 240))

    // ===== 运行时：默认生效 / 头切换 / 非法回退 =====
    const sess = async (app, title) => String((await api('POST', `/api/app/${app}/session`, { token: owner.token, body: { title } })).json.value ?? '')

    chatLog.length = 0
    const s1 = await sess(APP1, 'taa-默认')
    const chat1 = await chatSse(APP1, owner.token, s1, '普通问候')
    check('TAA-13 默认智能体主提示词注入', !chat1.runError && chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('BUILDER_MAIN_V1')),
      `err=${chat1.runError}`)
    check('TAA-14 主请求披露具名工具 subagent_scanner 与知识库检索', chatLog.some((r) => !isSubRequest(r) && r.allText.includes('subagent_scanner') && r.allText.includes('search_knowledge_base')))
    check('TAA-13b 协作开启注入子智能体名册（D53：[可派发子智能体]+工具名+派发说明）',
      chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('[可派发子智能体]') && sysText(r).includes('subagent_scanner') && sysText(r).includes('派发说明')),
      sysText(chatLog.find((r) => !isSubRequest(r)) ?? { messages: [] }).slice(0, 200))
    check('TAA-13c 无派发轮不产生 moai.subagent 事件', (chat1.subAgentEvents ?? []).length === 0 && (chat1.customEvents ?? []).length === 0,
      JSON.stringify(chat1.customEvents ?? []).slice(0, 120))

    chatLog.length = 0
    const s2 = await sess(APP1, 'taa-切换')
    const chat2 = await chatSse(APP1, owner.token, s2, '普通问候', { 'X-Moai-Team-Agent-Id': PLAN })
    check('TAA-15 请求头切换到 plan 生效', !chat2.runError && chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('规划师')),
      `err=${chat2.runError}`)
    check('TAA-16 切换后工具集跟随（披露 researcher 不披露 scanner）', chatLog.some((r) => !isSubRequest(r) && r.allText.includes('subagent_researcher'))
      && chatLog.every((r) => !r.allText.includes('subagent_scanner')))

    chatLog.length = 0
    const s3 = await sess(APP1, 'taa-非法头')
    const chat3 = await chatSse(APP1, owner.token, s3, '普通问候', { 'X-Moai-Team-Agent-Id': crypto.randomUUID() })
    check('TAA-17 非法头静默回退默认', !chat3.runError && chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('BUILDER_MAIN_V1')), `err=${chat3.runError}`)

    // ===== 具名派发全链路 =====
    chatLog.length = 0
    const s4 = await sess(APP1, 'taa-派发')
    const chat4 = await chatSse(APP1, owner.token, s4, '请派发扫描')
    const subReqs = chatLog.filter(isSubRequest)
    check('TAA-18 具名派发全链路（结论回传 DONE）', chat4.status === 200 && !chat4.runError && (chat4.reply ?? '').includes('扫描结论') && (chat4.reply ?? '').includes('DONE'),
      `reply=${(chat4.reply ?? '').slice(0, 80)} err=${chat4.runError}`)
    check('TAA-19 子请求注入子智能体提示词与子代理模式后缀', subReqs.length >= 1 && subReqs.every((r) => sysText(r).includes('SCANNER_SUB_V1') && sysText(r).includes('[子代理模式]')),
      `captures=${chatLog.length}`)
    check('TAA-20 子请求工具白名单（无知识库检索/无主提示词注入的越界工具）', subReqs.every((r) => !r.allText.includes('search_knowledge_base')))
    check('TAA-21 深度恒 1（子请求无任何派发工具）', subReqs.every((r) => !r.allText.includes('spawn_subagent') && !r.allText.includes('subagent_')))
    check('TAA-22 子请求不含元工具（声明即边界：scanner 无声明则零工具）', subReqs.every((r) => !r.allText.includes('list_tools')))

    // ===== 子智能体过程实时事件（moai.subagent，D53） =====
    const sa = chat4.subAgentEvents ?? []
    const saStarted = sa.find((e) => e.value.kind === 'started')
    const saTexts = sa.filter((e) => e.value.kind === 'text')
    const saCompleted = sa.find((e) => e.value.kind === 'completed')
    check('TAA-18b 派发轮产生 moai.subagent 事件且序列完整（started→…→completed）',
      sa.length >= 2 && saStarted != null && saCompleted != null && sa.findIndex((e) => e.value.kind === 'started') < sa.findIndex((e) => e.value.kind === 'completed'),
      JSON.stringify(sa.map((e) => e.value.kind)).slice(0, 160))
    check('TAA-18c started 携带子智能体标识（key=scanner/名称=扫描器/traceId）',
      saStarted != null && saStarted.value.agentKey === 'scanner' && String(saStarted.value.agentName ?? '').includes('扫描器') && !!saStarted.value.traceId,
      JSON.stringify(saStarted ?? {}).slice(0, 200))
    check('TAA-18d text 事件携带子会话正文增量', saTexts.some((e) => String(e.value.text ?? '').includes('扫描结论')),
      JSON.stringify(saTexts).slice(0, 160))
    check('TAA-18e completed 携带最终结论与耗时', saCompleted != null && String(saCompleted.value.finalText ?? '').includes('扫描结论')
      && Number(saCompleted.value.elapsedMilliseconds ?? 0) >= 0, JSON.stringify(saCompleted ?? {}).slice(0, 200))
    check('TAA-18f started 实时于派发结果事件前（合并循环窗口注入）',
      saStarted != null && chat4.eventTypes.indexOf('CUSTOM') < chat4.eventTypes.indexOf('TOOL_CALL_RESULT'),
      `types=${chat4.eventTypes.join(',').slice(0, 200)}`)

    // ===== 发布快照冻结（发布后改智能体不影响线上） =====
    await api('PUT', `/api/app/${APP2}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C1], executionSettings: baseSettings },
    })
    check('TAA-23 单选保存 200', true)
    check('TAA-24 单智能体应用发布 200', (await api('POST', `/api/app/${APP2}/publish`, { token: owner.token })).status === 200)

    chatLog.length = 0
    const s5 = await sess(APP2, 'taa-冻结前')
    await chatSse(APP2, owner.token, s5, '普通问候')
    check('TAA-25 发布后按快照主提示词执行（V1）', chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('BUILDER_MAIN_V1')))

    await api('PUT', `/api/team-agent/${C1}`, {
      token: owner.token,
      body: {
        key: 'builder', name: '构建器-' + TS, description: 'E2E 构建智能体',
        mainPrompt: '你是构建器。BUILDER_MAIN_V2 标记。收到目标后派发子智能体完成。',
        subs: [{ key: 'scanner', name: '扫描器', description: '对目标做逐项扫描核对', prompt: '你是扫描器。SCANNER_SUB_V2 标记。逐项扫描并输出结论。' }],
      },
    })
    chatLog.length = 0
    const s6 = await sess(APP2, 'taa-冻结后')
    const chat6 = await chatSse(APP2, owner.token, s6, '普通问候')
    check('TAA-26 发布后编辑智能体不影响线上（仍 V1 非 V2）', !chat6.runError
      && chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('BUILDER_MAIN_V1'))
      && chatLog.every((r) => !r.allText.includes('BUILDER_MAIN_V2')), `err=${chat6.runError}`)

    // 调试会话（草稿口径）跟随实时定义
    const dbg = await api('POST', `/api/app/${APP2}/debug/session`, { token: owner.token, body: {} })
    if (dbg.status === 200) {
      chatLog.length = 0
      const DSID = String(dbg.json?.value ?? dbg.json ?? '')
      await chatSse(APP2, owner.token, DSID, '普通问候')
      check('TAA-27 调试会话按草稿实时定义（V2）', chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('BUILDER_MAIN_V2')))
    } else {
      check('TAA-27 调试会话按草稿实时定义（V2）', false, `debug-session ${dbg.status}`)
    }

    // ===== 绑定不开子代理协作的运行时语义（D53：仅主智能体生效） =====
    const APP3 = await mkApp('解耦应用-' + TS)
    const r28 = await api('PUT', `/api/app/${APP3}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, teamAgentIds: [C1], executionSettings: { subAgent: { enabled: false } } },
    })
    check('TAA-28 绑定不开协作保存 200 并发布', r28.status === 200 && (await api('POST', `/api/app/${APP3}/publish`, { token: owner.token })).status === 200,
      `${r28.status} ${r28.text.slice(0, 120)}`)

    chatLog.length = 0
    const s7 = await sess(APP3, 'taa-仅主智能体')
    const chat7 = await chatSse(APP3, owner.token, s7, '请派发扫描')
    check('TAA-29 主智能体提示词注入（含 [团队智能体] 标签与当前定义标记 V2）', !chat7.runError
      && chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('[团队智能体：') && sysText(r).includes('BUILDER_MAIN_V2')), `err=${chat7.runError}`)
    check('TAA-30 未开协作注入禁止派发文案且不披露具名派发工具',
      chatLog.some((r) => !isSubRequest(r) && sysText(r).includes('未开启子代理协作'))
        && chatLog.every((r) => !r.allText.includes('subagent_scanner') && !r.allText.includes('[可派发子智能体]')))
    check('TAA-31 不开协作对话无 moai.subagent 事件', (chat7.subAgentEvents ?? []).length === 0, JSON.stringify(chat7.customEvents ?? []).slice(0, 120))
  } finally {
    if (adminToken && models.channelId) await api('DELETE', `/api/ai/channel/${models.channelId}`, { token: adminToken })
    stub.close()
  }

  console.log(`\n===== team-agent-app E2E 完成：PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
