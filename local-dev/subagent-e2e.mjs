// Agent 应用子代理协作 E2E（后端 127.0.0.1:5210）
// 覆盖：subAgent 配置保存与回显 → spawn_subagent 工具委派全链路（主代理工具调用 → 子代理独立上下文执行 → 结论回传）
//       → 深度固定 1（子代理请求不携带 spawn_subagent 工具，桩捕获请求断言）→ 未开启协作时工具不注册（普通回复无 RUN_ERROR）
//       → 外部应用开启子代理保存 400 → 参数越界 400。
// 模型策略：本地 OpenAI 兼容桩（提示词条件行为：主代理委派 → 工具调用；子代理模式 → 直接结论），零 SKIP。
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
      chatLog.push({ messages })

      // 子代理请求：指令带 [子代理模式]，直接回结论（绝不再发起工具调用）
      if (allText.includes('[子代理模式]')) {
        return reply(res, payload, '子代理结论：Q3 销售数据整理完成，共 42 单，总额 18.6 万，无异常单据。')
      }

      // 工具结果已回传：主代理汇总最终答复
      if (messages.some((m) => m.role === 'tool')) {
        return reply(res, payload, 'DONE ' + String(messages.filter((m) => m.role === 'tool').map((m) => m.content).join(';')).slice(0, 200))
      }

      // 主代理：指令工具列表含 spawn_subagent 且用户要求委派 → 发起委派
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''
      if (allText.includes('spawn_subagent') && userText.includes('委派子代理')) {
        return toolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'spawn_subagent', argumentsJson: JSON.stringify({ task: '整理Q3销售数据', guidance: '只输出结论与关键数字' }) }))
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
  const chName = `sub-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'sub-stub-key', enabled: true, description: 'subagent E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null
  await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: `sub-stub-chat-${TS}`, name: `sub-stub-chat-${TS}`, modelKind: 'conversation', description: 'subagent E2E 桩模型' }, enabled: true, isPublic: false },
  })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const modelId = String((models.json?.items ?? []).find((m) => m.name === `sub-stub-chat-${TS}`)?.id ?? '')
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
  const owner = await mkuser('so')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'sub-team-' + TS } })).json.value)

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = adminToken ? await createStubModel(adminToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  try {
    // ===== 配置保存与校验 =====
    const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '子代理应用-' + TS, appType: 'agent' } })
    const APP = String(ca.json?.value ?? '')

    const badRange = await api('PUT', `/api/app/${APP}/agent-config`, {
      token: owner.token,
      body: { prompt: 'x', wikiIds: [], plugins: [], modelId: models.modelId, executionSettings: { subAgent: { enabled: true, timeoutSeconds: 1 } } },
    })
    check('SA-01 子任务超时越界 400', badRange.status === 400, `${badRange.status}`)

    const save = await api('PUT', `/api/app/${APP}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId,
        executionSettings: { subAgent: { enabled: true, timeoutSeconds: 120, resultMaxChars: 4000 } },
      },
    })
    check('SA-02 子代理配置保存 200', save.status === 200, `${save.status} ${save.text.slice(0, 120)}`)

    const back = await api('GET', `/api/app/${APP}/agent-config`, { token: owner.token })
    check('SA-03 子代理配置回显', back.json?.executionSettings?.subAgent?.enabled === true && back.json?.executionSettings?.subAgent?.timeoutSeconds === 120,
      back.text.slice(0, 200))

    check('SA-04 发布应用 200', (await api('POST', `/api/app/${APP}/publish`, { token: owner.token })).status === 200)

    // ===== 委派全链路 =====
    chatLog.length = 0
    const sess = await api('POST', `/api/app/${APP}/session`, { token: owner.token, body: { title: 'sa-委派' } })
    const SID = String(sess.json?.value ?? '')
    const chat = await chatSse(APP, owner.token, SID, '请委派子代理整理数据')
    check('SA-05 主代理委派并回传子代理结论', chat.status === 200 && !chat.runError && (chat.reply ?? '').includes('子代理结论'), `reply=${chat.reply.slice(0, 100)} err=${chat.runError}`)
    check('SA-06 主代理汇总包含 DONE', (chat.reply ?? '').includes('DONE'), `reply=${chat.reply.slice(0, 100)}`)

    // 深度固定 1：子代理请求不携带 spawn_subagent 工具
    const subRequests = chatLog.filter((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('[子代理模式]')))
    check('SA-07 子代理被实际执行（桩捕获到子代理请求）', subRequests.length >= 1, `captures=${chatLog.length}`)
    check('SA-08 子代理不携带 spawn 工具（深度=1）', subRequests.every((r) => !r.messages.some((m) => typeof m.content === 'string' && m.content.includes('spawn_subagent'))))
    const mainRequests = chatLog.filter((r) => !r.messages.some((m) => typeof m.content === 'string' && m.content.includes('[子代理模式]')))
    check('SA-09 主代理请求携带 spawn_subagent 披露', mainRequests.some((r) => r.messages.some((m) => typeof m.content === 'string' && m.content.includes('spawn_subagent'))))

    // ===== 未开启协作：工具不注册 =====
    const cb = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '无子代理应用-' + TS, appType: 'agent' } })
    const APP2 = String(cb.json?.value ?? '')
    await api('PUT', `/api/app/${APP2}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.modelId, executionSettings: { subAgent: { enabled: false } } },
    })
    await api('POST', `/api/app/${APP2}/publish`, { token: owner.token })
    const sess2 = await api('POST', `/api/app/${APP2}/session`, { token: owner.token, body: { title: 'sa-关闭' } })
    const chat2 = await chatSse(APP2, owner.token, String(sess2.json?.value ?? ''), '请委派子代理整理数据')
    check('SA-10 未开启时无子代理调用且无 RUN_ERROR', !chat2.runError && !(chat2.reply ?? '').includes('子代理结论'), `reply=${chat2.reply.slice(0, 60)} err=${chat2.runError}`)
  } finally {
    if (adminToken && models.channelId) await api('DELETE', `/api/ai/channel/${models.channelId}`, { token: adminToken })
    stub.close()
  }

  console.log(`\n===== subagent E2E 完成：PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
