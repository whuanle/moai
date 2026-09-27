// 应用 ACP（agent-to-agent）E2E（场景 @ACP-S01 ~ @ACP-S19；后端 127.0.0.1:5210）
// 覆盖：认证与 scope 门禁（无凭证 401 / 无 app_acp 403 / app token 403 / GET 405）→ 协议行为
//       （initialize 握手 / 未知方法 -32601 / 非法 JSON -32700）→ 应用门禁（跨团队 404 / 未发布 403）
//       → Agent 应用对话（session/new → session/prompt SSE：agent_message_chunk + end_turn → 续聊落库）
//       → 用户 token 路径 → 会话隔离与 session/load → Workflow 应用对话（节点 tool_call + 最终回复）
//       → session/cancel 中断（SLOW 桩流式）→ 范围缓存立即性（授权/吊销即生效）→ 默认范围含 app_acp。
// 模型策略：内置本地 OpenAI 兼容桩（/v1/chat/completions 固定回显文案，SLOW 标记触发慢速流），无需真实模型渠道；
//       Workflow 应用使用引擎确定性编排（start→compute→check→hit/miss→end），不依赖模型。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0, SKIP = 0
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
const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

// ===== ACP 调用封装 =====

function acpHeaders(cred) {
  const headers = { 'Content-Type': 'application/json' }
  if (cred?.key) headers['x-api-key'] = cred.key
  if (cred?.token) headers['Authorization'] = `Bearer ${cred.token}`
  return headers
}

/** 单条 JSON-RPC 请求（非流式方法或需要 HTTP 状态断言时使用） */
async function acp(cred, appId, body, method = 'POST') {
  const res = await fetch(`${BASE}/api/external/app/${appId}/acp`, { method, headers: acpHeaders(cred), body: method === 'GET' ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

/** session/prompt（SSE）：收集 session/update 通知与最终 response；onFirstChunk 触发时回调（用于 cancel 竞速） */
async function acpPrompt(cred, appId, body, onFirstChunk) {
  const res = await fetch(`${BASE}/api/external/app/${appId}/acp`, { method: 'POST', headers: acpHeaders(cred), body: JSON.stringify(body) })
  const contentType = res.headers.get('content-type') || ''
  if (!contentType.includes('text/event-stream')) {
    const text = await res.text()
    let json = null
    try { json = JSON.parse(text) } catch { /* 非 JSON */ }
    return { status: res.status, contentType, notifications: [], final: json, text }
  }

  const notifications = []
  let final = null
  let firstChunkFired = false
  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    for (;;) {
      const idx = buffer.indexOf('\n\n')
      if (idx < 0) break
      const raw = buffer.slice(0, idx)
      buffer = buffer.slice(idx + 2)
      const dataLine = raw.split('\n').find((l) => l.startsWith('data: '))
      if (!dataLine) continue
      let msg = null
      try { msg = JSON.parse(dataLine.slice(6)) } catch { continue }
      if (msg?.method === 'session/update') {
        notifications.push(msg)
        if (!firstChunkFired) {
          firstChunkFired = true
          if (onFirstChunk) await onFirstChunk()
        }
      } else {
        final = msg
      }
    }
  }
  return { status: res.status, contentType, notifications, final, text: '' }
}

const chunkTexts = (notifications, sessionId) => notifications
  .filter((n) => n.params?.sessionId === sessionId && n.params?.update?.sessionUpdate === 'agent_message_chunk')
  .map((n) => n.params.update.content?.text ?? '')

// ===== 本地 OpenAI 兼容桩：chat 固定回显，SLOW 标记触发慢速流（供 session/cancel 竞速） =====

function startStubServer() {
  const stub = http.createServer((req, res) => {
    // session/cancel 会让后端中断对桩的请求：静默吞掉断连写错误
    res.on('error', () => {})
    res.socket?.on('error', () => {})
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      if (!String(req.url).includes('/chat/completions')) {
        res.writeHead(404, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: 'not found' }))
        return
      }

      const userMsgs = (payload.messages ?? []).filter((m) => m.role === 'user').map((m) => String(m.content ?? ''))
      const last = userMsgs[userMsgs.length - 1] ?? ''
      const slow = last.includes('SLOW')
      const reply = slow ? 'ABCDEFGH' : `收到：${last.replace(/\s+/g, ' ').slice(0, 60)}`

      if (payload.stream) {
        res.writeHead(200, { 'Content-Type': 'text/event-stream' })
        const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
        const pieces = slow ? ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'] : [reply]
        let i = 0
        const timer = setInterval(() => {
          if (res.destroyed) { clearInterval(timer); res.end(); return }
          if (i < pieces.length) {
            res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: pieces[i] }, finish_reason: null }] }) + '\n\n')
            i++
            return
          }
          clearInterval(timer)
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
          res.write('data: [DONE]\n\n')
          res.end()
        }, slow ? 400 : 0)
        return
      }

      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: reply }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

// ===== 确定性工作流编排（无模型依赖）：start → compute → check → hit/miss → end =====

const DEF_NODES = [
  { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
  {
    key: 'compute', name: '计算', type: 'javaScript',
    config: { code: 'function run(inputs, sys, nodes, system) {\n  return { hasResult: inputs.flag === "yes", summary: "sum-" + inputs.flag, env: system.env }\n}' },
    inputs: { flag: { expressionType: 'variable', value: 'start.question', required: true } },
    outputs: [{ name: 'hasResult', fieldType: 'boolean' }, { name: 'summary', fieldType: 'string' }, { name: 'env', fieldType: 'string' }],
  },
  { key: 'check', name: '是否有结果', type: 'condition', inputs: { condition: { expressionType: 'variable', value: 'compute.hasResult', required: true } }, outputs: [] },
  {
    key: 'hit', name: '命中分支', type: 'javaScript',
    config: { code: 'function run(inputs) { return { answer: inputs.s } }' },
    inputs: { s: { expressionType: 'interpolation', value: '命中:{compute.summary}', required: true } },
    outputs: [{ name: 'answer', fieldType: 'string' }],
  },
  {
    key: 'miss', name: '未命中分支', type: 'javaScript',
    config: { code: 'function run(inputs) { return { answer: inputs.s } }' },
    inputs: { s: { expressionType: 'fixed', value: '未命中', required: true } },
    outputs: [{ name: 'answer', fieldType: 'string' }],
  },
  {
    key: 'end', name: '结束', type: 'end',
    inputs: {
      answer: { expressionType: 'variable', value: 'hit.answer', required: false },
      missAnswer: { expressionType: 'variable', value: 'miss.answer', required: false },
    },
    outputs: [{ name: 'answer', fieldType: 'string' }, { name: 'missAnswer', fieldType: 'string' }],
  },
]
const DEF_CONNECTIONS = [
  { id: 'c1', source: 'start', target: 'compute' },
  { id: 'c2', source: 'compute', target: 'check' },
  { id: 'c3', source: 'check', target: 'hit', condition: 'true', label: '满足' },
  { id: 'c4', source: 'check', target: 'miss', condition: 'false', label: '不满足' },
  { id: 'c5', source: 'hit', target: 'end' },
  { id: 'c6', source: 'miss', target: 'end' },
]
const POS = { start: { x: 80, y: 200 }, compute: { x: 280, y: 200 }, check: { x: 480, y: 200 }, hit: { x: 680, y: 120 }, miss: { x: 680, y: 300 }, end: { x: 880, y: 200 } }
const buildDefinition = () => ({ id: '', name: 'acp-wf', version: 0, status: 'draft', nodes: DEF_NODES, connections: DEF_CONNECTIONS, variables: [], ui: { nodePositions: POS } })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ao')
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken ?? ''

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: `acp-team-${TS}` } })).json.value)

  // 桩渠道 + conversation 模型并授权给团队
  const stub = await startStubServer()
  const stubPort = stub.address().port
  const chName = `acp-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'acp-stub-key', enabled: true, description: 'app-acp E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const modelName = `acp-stub-chat-${TS}`
  await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'app-acp E2E 桩模型' }, enabled: true, isPublic: false } })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  const auth = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken })
  await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken, body: { modelId: MODEL_ID, teamIds: [...new Set([...(auth.json?.items ?? []).map((i) => Number(i.teamId)), TID])] } })

  // 应用：Agent（已发布 A1 / 未发布 A2）+ Workflow（已发布 W1）
  const A1 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `acp-agent-${TS}`, appType: 'agent' } })).json.value)
  await api('PUT', `/api/app/${A1}/agent-config`, { token: owner.token, body: { modelId: MODEL_ID, prompt: '你是回声助手', wikiIds: [], plugins: [] } })
  await api('POST', `/api/app/${A1}/publish`, { token: owner.token })

  const A2 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `acp-draft-${TS}`, appType: 'agent' } })).json.value)

  const W1 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `acp-wf-${TS}`, appType: 'workflow' } })).json.value)
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: W1, teamId: TID, definition: JSON.stringify(buildDefinition()), editorData: JSON.stringify({ nodes: POS }) } })
  await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: W1, teamId: TID } })

  // A3：外部应用（is_auth=false，用户 token 换取要求 IsExternal）
  const A3 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `acp-ext-${TS}`, appType: 'agent', isExternal: true, isAuth: false } })).json.value)
  await api('PUT', `/api/app/${A3}/agent-config`, { token: owner.token, body: { modelId: MODEL_ID, prompt: '你是回声助手', wikiIds: [], plugins: [] } })
  await api('POST', `/api/app/${A3}/publish`, { token: owner.token })

  // 接入 key：K1（app_acp+app_chat）、K2（无 app_acp）、K3（不传=默认全量）、K4（跨团队）
  const mkKey = async (name, scopes) => {
    const body = { teamId: TID, name }
    if (scopes) body.scopes = scopes
    const r = await api('POST', '/api/access-app', { token: owner.token, body })
    return { id: String(r.json?.accessAppId ?? ''), key: String(r.json?.key ?? '') }
  }
  const K1 = await mkKey(`acp-k1-${TS}`, ['app_acp', 'app_chat'])
  const K2 = await mkKey(`acp-k2-${TS}`, ['wiki_read'])
  const K3 = await mkKey(`acp-k3-${TS}`, null)
  const owner2 = await mkuser('bo')
  const TID2 = Number((await api('POST', '/api/team', { token: owner2.token, body: { name: `acp-team2-${TS}` } })).json.value)
  const K4r = await api('POST', '/api/access-app', { token: owner2.token, body: { teamId: TID2, name: `acp-k4-${TS}`, scopes: ['app_acp'] } })
  const K4 = { id: String(K4r.json?.accessAppId ?? ''), key: String(K4r.json?.key ?? '') }

  // ===== ACP-S01 ~ S05 认证 / 范围 / 协议基础 =====
  check('ACP-S01 无凭证 401', (await acp(null, A1, { jsonrpc: '2.0', id: 1, method: 'initialize', params: {} })).status === 401)

  const noScope = await acp({ key: K2.key }, A1, { jsonrpc: '2.0', id: 1, method: 'initialize', params: {} })
  check('ACP-S02 无 app_acp 范围 403 insufficient_scope',
    noScope.status === 403 && noScope.json?.error?.code === 'insufficient_scope', JSON.stringify(noScope.json).slice(0, 140))

  const get405 = await fetch(`${BASE}/api/external/app/${A1}/acp`, { method: 'GET', headers: acpHeaders({ key: K1.key }) })
  check('ACP-S03 GET 405', get405.status === 405)

  const badJson = await fetch(`${BASE}/api/external/app/${A1}/acp`, { method: 'POST', headers: acpHeaders({ key: K1.key }), body: '{bad json' })
  check('ACP-S04 非法 JSON -32700', badJson.status === 200 && (await badJson.json())?.error?.code === -32700)

  const unknown = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 2, method: 'session/frobnicate', params: {} })
  check('ACP-S05 未知方法 -32601', unknown.status === 200 && unknown.json?.error?.code === -32601, JSON.stringify(unknown.json).slice(0, 140))

  // ===== ACP-S06 initialize 握手 =====
  const init = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 3, method: 'initialize', params: { protocolVersion: 1, clientCapabilities: {} } })
  check('ACP-S06 initialize 握手（版本/能力/鉴权方法）',
    init.status === 200 && init.json?.result?.protocolVersion === 1 && init.json.result.agentCapabilities?.loadSession === true
    && Array.isArray(init.json.result.authMethods) && init.json.id === 3,
    JSON.stringify(init.json).slice(0, 200))

  // ===== ACP-S07/S08 应用门禁 =====
  const cross = await acp({ key: K4.key }, A1, { jsonrpc: '2.0', id: 4, method: 'initialize', params: {} })
  check('ACP-S07 跨团队应用 404', cross.status === 404 && cross.json?.error?.code === 'app_not_found', JSON.stringify(cross.json).slice(0, 140))

  const draft = await acp({ key: K1.key }, A2, { jsonrpc: '2.0', id: 5, method: 'initialize', params: {} })
  check('ACP-S08 未发布应用 403', draft.status === 403 && draft.json?.error?.code === 'permission_denied', JSON.stringify(draft.json).slice(0, 140))

  // ===== ACP-S09 Agent 应用对话（key 直连）：session/new → session/prompt SSE =====
  const sn = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 6, method: 'session/new', params: { cwd: '/', mcpServers: [] } })
  check('ACP-S09a session/new 返回 sessionId', sn.status === 200 && isGuid(sn.json?.result?.sessionId), JSON.stringify(sn.json).slice(0, 140))
  const SID = String(sn.json?.result?.sessionId ?? '')

  const marker = `回声测试${TS}`
  const p1 = await acpPrompt({ key: K1.key }, A1, { jsonrpc: '2.0', id: 7, method: 'session/prompt', params: { sessionId: SID, prompt: [{ type: 'text', text: marker }] } })
  const reply1 = chunkTexts(p1.notifications, SID).join('')
  check('ACP-S09b prompt 流式回复 end_turn 且含回显',
    p1.status === 200 && p1.contentType.includes('text/event-stream') && p1.notifications.length >= 1
    && p1.final?.result?.stopReason === 'end_turn' && reply1.includes(marker),
    `chunks=${reply1.slice(0, 60)} final=${JSON.stringify(p1.final).slice(0, 140)}`)

  // ===== ACP-S10 续聊 + 会话消息落库 =====
  const p2 = await acpPrompt({ key: K1.key }, A1, { jsonrpc: '2.0', id: 8, method: 'session/prompt', params: { sessionId: SID, prompt: [{ type: 'text', text: `第二轮${marker}` }] } })
  check('ACP-S10a 续聊 end_turn', p2.final?.result?.stopReason === 'end_turn', JSON.stringify(p2.final).slice(0, 140))
  const msgs = await api('GET', `/api/external/session/${SID}/messages`, { token: K1.key })
  const userMsgs = (msgs.json?.items ?? []).filter((m) => m.role === 'user')
  check('ACP-S10b 会话消息落库（两轮用户消息）', msgs.status === 200 && userMsgs.length === 2, `status=${msgs.status} items=${(msgs.json?.items ?? []).length}`)

  // ===== ACP-S11 用户 token 路径（外部应用 A3：用户 token 换取要求 IsExternal） =====
  const EXT_UID = `acp-user-${TS}`
  const tok = await api('POST', '/api/external/token', { body: { accessAppKey: K1.key, appId: A3, externalUserId: EXT_UID, nickname: 'acp用户' } })
  const userToken = String(tok.json?.accessToken ?? '')
  const snU = await acp({ token: userToken }, A3, { jsonrpc: '2.0', id: 9, method: 'session/new', params: {} })
  const SIDU = String(snU.json?.result?.sessionId ?? '')
  const pU = await acpPrompt({ token: userToken }, A3, { jsonrpc: '2.0', id: 10, method: 'session/prompt', params: { sessionId: SIDU, prompt: [{ type: 'text', text: `用户token${TS}` }] } })
  check('ACP-S11 用户 token 会话与对话 end_turn',
    tok.status === 200 && isGuid(SIDU) && pU.final?.result?.stopReason === 'end_turn', `tok=${tok.status} final=${JSON.stringify(pU.final).slice(0, 140)}`)

  // ===== ACP-S12 会话隔离：同团队另一把 key 不能续聊他人会话 =====
  const hijack = await acpPrompt({ key: K3.key }, A1, { jsonrpc: '2.0', id: 11, method: 'session/prompt', params: { sessionId: SID, prompt: [{ type: 'text', text: '越权' }] } })
  check('ACP-S12 他人会话 prompt -32000', hijack.final?.error?.code === -32000 && String(hijack.final?.error?.message || '').includes('会话不存在'),
    JSON.stringify(hijack.final).slice(0, 140))

  // ===== ACP-S13 session/load =====
  const loadOk = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 12, method: 'session/load', params: { sessionId: SID, cwd: '/' } })
  const loadMiss = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 13, method: 'session/load', params: { sessionId: '01924f5e-0000-7000-8000-00000000ffff' } })
  check('ACP-S13 session/load 本人 200 / 未知会话 -32000', loadOk.status === 200 && loadOk.json?.result !== undefined && loadMiss.json?.error?.code === -32000,
    `ok=${JSON.stringify(loadOk.json).slice(0, 80)} miss=${JSON.stringify(loadMiss.json).slice(0, 120)}`)

  // ===== ACP-S14 Workflow 应用 ACP 对话（节点 tool_call + 最终回复） =====
  const snW = await acp({ key: K1.key }, W1, { jsonrpc: '2.0', id: 14, method: 'session/new', params: {} })
  const SIDW = String(snW.json?.result?.sessionId ?? '')
  const pW = await acpPrompt({ key: K1.key }, W1, { jsonrpc: '2.0', id: 15, method: 'session/prompt', params: { sessionId: SIDW, prompt: [{ type: 'text', text: 'yes' }] } })
  const nodeCalls = pW.notifications.filter((n) => n.params?.update?.sessionUpdate === 'tool_call')
  const wfReply = chunkTexts(pW.notifications, SIDW).join('')
  check('ACP-S14 workflow prompt（end_turn + 节点 tool_call + 回复 sum-yes）',
    isGuid(SIDW) && pW.final?.result?.stopReason === 'end_turn' && nodeCalls.length >= 1 && wfReply.includes('sum-yes'),
    `calls=${JSON.stringify(nodeCalls).slice(0, 160)} reply=${wfReply.slice(0, 60)} final=${JSON.stringify(pW.final).slice(0, 120)}`)

  // ===== ACP-S15 session/cancel 中断慢速流 =====
  let cancelDone = false
  const snC = await acp({ key: K1.key }, A1, { jsonrpc: '2.0', id: 16, method: 'session/new', params: {} })
  const SIDC = String(snC.json?.result?.sessionId ?? '')
  const pC = await acpPrompt({ key: K1.key }, A1, { jsonrpc: '2.0', id: 17, method: 'session/prompt', params: { sessionId: SIDC, prompt: [{ type: 'text', text: `SLOW${TS}` }] } },
    async () => {
      if (cancelDone) return
      cancelDone = true
      await fetch(`${BASE}/api/external/app/${A1}/acp`, {
        method: 'POST',
        headers: acpHeaders({ key: K1.key }),
        body: JSON.stringify({ jsonrpc: '2.0', method: 'session/cancel', params: { sessionId: SIDC } }),
      })
    })
  check('ACP-S15 session/cancel 中断（stopReason=cancelled）', pC.final?.result?.stopReason === 'cancelled', JSON.stringify(pC.final).slice(0, 140))

  // ===== ACP-S16/S17 范围缓存立即性（失效钩子，无 TTL 等待） =====
  await api('PUT', `/api/access-app/${K2.id}`, { token: owner.token, body: { name: `acp-k2-${TS}`, scopes: ['wiki_read', 'app_acp'] } })
  const granted = await acp({ key: K2.key }, A1, { jsonrpc: '2.0', id: 18, method: 'initialize', params: {} })
  check('ACP-S16 授权 app_acp 后立即放行', granted.status === 200 && granted.json?.result?.protocolVersion === 1, JSON.stringify(granted.json).slice(0, 120))

  await api('PUT', `/api/access-app/${K2.id}`, { token: owner.token, body: { name: `acp-k2-${TS}`, scopes: ['wiki_read'] } })
  const revoked = await acp({ key: K2.key }, A1, { jsonrpc: '2.0', id: 19, method: 'initialize', params: {} })
  check('ACP-S17 吊销 app_acp 后立即 403', revoked.status === 403 && revoked.json?.error?.code === 'insufficient_scope', JSON.stringify(revoked.json).slice(0, 120))

  // ===== ACP-S18 app token（无外部用户语义）403 =====
  const appTok = await api('POST', '/api/external/token', { body: { accessAppKey: K1.key } })
  const appTokAcp = await acp({ token: String(appTok.json?.accessToken ?? '') }, A1, { jsonrpc: '2.0', id: 20, method: 'initialize', params: {} })
  check('ACP-S18 app token 403 external_user_token_unsupported',
    appTokAcp.status === 403 && appTokAcp.json?.error?.code === 'external_user_token_unsupported', JSON.stringify(appTokAcp.json).slice(0, 140))

  // ===== ACP-S19 默认范围含 app_acp（不传 scopes = 存量全量口径） =====
  const accList = await api('GET', `/api/access-app/list?teamId=${TID}`, { token: owner.token })
  const k3item = (accList.json?.items ?? []).find((i) => i.accessAppId === K3.id)
  check('ACP-S19 默认范围回显含 app_acp', Array.isArray(k3item?.scopes) && k3item.scopes.includes('app_acp'), JSON.stringify(k3item?.scopes))

  // ===== 清理 =====
  if (CH_ID) await api('DELETE', `/api/ai/channel/${CH_ID}`, { token: adminToken })
  for (const k of [K1, K2, K3, K4]) {
    if (k.id) await api('DELETE', `/api/access-app/${k.id}`, { token: owner.token })
  }
  stub.close()

  console.log(`\nRESULT: PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((err) => {
  console.error('FATAL', err)
  process.exit(1)
})
