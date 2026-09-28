// 应用 A2A（Google Agent2Agent）E2E（场景 @A2A-S01 ~ @A2A-S14；后端 127.0.0.1:5000）
// 覆盖：认证与 scope 门禁（无凭证 401 / 无 app_a2a 403 / GET JSON-RPC 端点 405 / 非法 JSON -32700 / 未知方法 -32601）
//       → Agent Card（agent.json：name/url/protocolVersion/streaming）→ 应用门禁（跨团队 404 / 未发布 403）
//       → Agent 应用对话：message/send（无 contextId 自动建会话，task completed + artifacts 回显 + contextId）
//       → 续聊（携带 contextId）→ 会话消息落库 → tasks/get（completed / 幽灵 -32001）
//       → message/stream（SSE：Task working → artifact-update 增量 → status-update completed final → 最终 Task）
//       → tasks/cancel 中断慢速流（SLOW 桩）→ 用户 token 路径 → 范围缓存立即性（授权/吊销即生效）→ 默认范围含 app_a2a。
// 模型策略：内置本地 OpenAI 兼容桩（/v1/chat/completions 固定回显文案，SLOW 标记触发慢速流），无需真实模型渠道。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
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

// ===== A2A 调用封装 =====

function a2aHeaders(cred) {
  const headers = { 'Content-Type': 'application/json' }
  if (cred?.key) headers['x-api-key'] = cred.key
  if (cred?.token) headers['Authorization'] = `Bearer ${cred.token}`
  return headers
}

async function a2a(cred, appId, body, method = 'POST') {
  const res = await fetch(`${BASE}/api/external/app/${appId}/a2a`, { method, headers: a2aHeaders(cred), body: method === 'GET' ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

/** Agent Card（GET /a2a/agent.json） */
async function agentCard(cred, appId) {
  const res = await fetch(`${BASE}/api/external/app/${appId}/a2a/agent.json`, { headers: a2aHeaders(cred) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

/** message/stream（SSE）：收集事件（result.kind 区分 task/status-update/artifact-update） */
async function a2aStream(cred, appId, body, onFirstEvent) {
  const res = await fetch(`${BASE}/api/external/app/${appId}/a2a`, { method: 'POST', headers: a2aHeaders(cred), body: JSON.stringify(body) })
  const contentType = res.headers.get('content-type') || ''
  if (!contentType.includes('text/event-stream')) {
    const text = await res.text()
    let json = null
    try { json = JSON.parse(text) } catch { /* 非 JSON */ }
    return { status: res.status, contentType, events: [], finalTask: null, text }
  }

  const events = []
  let firstFired = false
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
      if (msg?.result) {
        events.push(msg.result)
        if (!firstFired) {
          firstFired = true
          if (onFirstEvent) await onFirstEvent(events)
        }
      }
    }
  }
  const finalTask = [...events].reverse().find((e) => e.kind === 'task') ?? null
  return { status: res.status, contentType, events, finalTask, text: '' }
}

const artifactText = (events) => events
  .filter((e) => e.kind === 'artifact-update')
  .map((e) => e.artifact?.parts?.map((p) => p.text ?? '').join('') ?? '')
  .join('')

// ===== 本地 OpenAI 兼容桩：chat 固定回显，SLOW 标记触发慢速流（供 tasks/cancel 竞速） =====

function startStubServer() {
  const stub = http.createServer((req, res) => {
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ao')
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken ?? ''

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: `a2a-team-${TS}` } })).json.value)

  // 桩渠道 + conversation 模型并授权给团队
  const stub = await startStubServer()
  const stubPort = stub.address().port
  const chName = `a2a-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'a2a-stub-key', enabled: true, description: 'app-a2a E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const modelName = `a2a-stub-chat-${TS}`
  await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'app-a2a E2E 桩模型' }, enabled: true, isPublic: false } })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  const auth = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken })
  await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken, body: { modelId: MODEL_ID, teamIds: [...new Set([...(auth.json?.items ?? []).map((i) => Number(i.teamId)), TID])] } })

  // 应用：Agent（已发布 A1 / 未发布 A2）+ 外部应用 A3
  const A1 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `a2a-agent-${TS}`, appType: 'agent' } })).json.value)
  await api('PUT', `/api/app/${A1}/agent-config`, { token: owner.token, body: { modelId: MODEL_ID, prompt: '你是回声助手', wikiIds: [], plugins: [] } })
  await api('POST', `/api/app/${A1}/publish`, { token: owner.token })

  const A2 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `a2a-draft-${TS}`, appType: 'agent' } })).json.value)

  const A3 = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `a2a-ext-${TS}`, appType: 'agent', isExternal: true, isAuth: false } })).json.value)
  await api('PUT', `/api/app/${A3}/agent-config`, { token: owner.token, body: { modelId: MODEL_ID, prompt: '你是回声助手', wikiIds: [], plugins: [] } })
  await api('POST', `/api/app/${A3}/publish`, { token: owner.token })

  // 接入 key：K1（app_a2a+app_chat）、K2（无 app_a2a）、K3（不传=默认全量）、K4（跨团队）
  const mkKey = async (name, scopes) => {
    const body = { teamId: TID, name }
    if (scopes) body.scopes = scopes
    const r = await api('POST', '/api/access-app', { token: owner.token, body })
    return { id: String(r.json?.accessAppId ?? ''), key: String(r.json?.key ?? '') }
  }
  const K1 = await mkKey(`a2a-k1-${TS}`, ['app_a2a', 'app_chat'])
  const K2 = await mkKey(`a2a-k2-${TS}`, ['wiki_read'])
  const K3 = await mkKey(`a2a-k3-${TS}`, null)
  const owner2 = await mkuser('bo')
  const TID2 = Number((await api('POST', '/api/team', { token: owner2.token, body: { name: `a2a-team2-${TS}` } })).json.value)
  const K4r = await api('POST', '/api/access-app', { token: owner2.token, body: { teamId: TID2, name: `a2a-k4-${TS}`, scopes: ['app_a2a'] } })
  const K4 = { id: String(K4r.json?.accessAppId ?? ''), key: String(K4r.json?.key ?? '') }

  // ===== A2A-S01 认证门禁 =====
  check('A2A-S01a 无凭证 401', (await a2a(null, A1, { jsonrpc: '2.0', id: 1, method: 'message/send', params: {} })).status === 401)
  check('A2A-S01b 无凭证 agent.json 401', (await agentCard(null, A1)).status === 401)

  // ===== A2A-S02 无 app_a2a 范围 403 =====
  const noScope = await a2a({ key: K2.key }, A1, { jsonrpc: '2.0', id: 1, method: 'message/send', params: {} })
  check('A2A-S02 无 app_a2a 范围 403 insufficient_scope',
    noScope.status === 403 && noScope.json?.error?.code === 'insufficient_scope', JSON.stringify(noScope.json).slice(0, 140))

  // ===== A2A-S03 Agent Card =====
  const card = await agentCard({ key: K1.key }, A1)
  check('A2A-S03 agent.json：name/url/protocolVersion/streaming',
    card.status === 200 && card.json?.protocolVersion === '0.3.0' && String(card.json?.url ?? '').endsWith(`/api/external/app/${A1}/a2a`)
    && card.json?.capabilities?.streaming === true && Array.isArray(card.json?.skills) && card.json.name?.includes('a2a-agent'),
    JSON.stringify(card.json).slice(0, 220))

  // ===== A2A-S04 协议基础 =====
  const getRpc = await fetch(`${BASE}/api/external/app/${A1}/a2a`, { method: 'GET', headers: a2aHeaders({ key: K1.key }) })
  check('A2A-S04a JSON-RPC 端点 GET 404/405（无 GET 路由）', getRpc.status === 404 || getRpc.status === 405)
  const badJson = await fetch(`${BASE}/api/external/app/${A1}/a2a`, { method: 'POST', headers: a2aHeaders({ key: K1.key }), body: '{bad json' })
  check('A2A-S04b 非法 JSON -32700', badJson.status === 200 && (await badJson.json())?.error?.code === -32700)
  const unknown = await a2a({ key: K1.key }, A1, { jsonrpc: '2.0', id: 2, method: 'frobnicate', params: {} })
  check('A2A-S04c 未知方法 -32601', unknown.status === 200 && unknown.json?.error?.code === -32601, JSON.stringify(unknown.json).slice(0, 140))

  // ===== A2A-S05 应用门禁 =====
  const cross = await a2a({ key: K4.key }, A1, { jsonrpc: '2.0', id: 3, method: 'message/send', params: {} })
  check('A2A-S05a 跨团队应用 404', cross.status === 404 && cross.json?.error?.code === 'app_not_found', JSON.stringify(cross.json).slice(0, 140))
  const draft = await a2a({ key: K1.key }, A2, { jsonrpc: '2.0', id: 4, method: 'message/send', params: {} })
  check('A2A-S05b 未发布应用 403', draft.status === 403 && draft.json?.error?.code === 'permission_denied', JSON.stringify(draft.json).slice(0, 140))

  // ===== A2A-S06 message/send：自动建会话 + completed Task + artifacts 回显 =====
  const marker = `回声测试${TS}`
  const send1 = await a2a({ key: K1.key }, A1, {
    jsonrpc: '2.0', id: 5, method: 'message/send',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-1`, parts: [{ kind: 'text', text: marker }] } },
  })
  const task1 = send1.json?.result
  const artifactText1 = task1?.artifacts?.[0]?.parts?.map((p) => p.text).join('') ?? ''
  check('A2A-S06a message/send completed 且 artifacts 含回显、contextId 自动生成',
    send1.status === 200 && task1?.kind === 'task' && task1?.status?.state === 'completed'
    && isGuid(task1?.contextId) && artifactText1.includes(marker), `${send1.status} ${JSON.stringify(task1).slice(0, 260)}`)
  const CONTEXT = String(task1?.contextId ?? '')

  // ===== A2A-S07 续聊（携带 contextId）=====
  const marker2 = `第二轮${TS}`
  const send2 = await a2a({ key: K1.key }, A1, {
    jsonrpc: '2.0', id: 6, method: 'message/send',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-2`, contextId: CONTEXT, parts: [{ kind: 'text', text: marker2 }] } },
  })
  check('A2A-S07 携带 contextId 续聊 completed',
    send2.json?.result?.status?.state === 'completed' && send2.json?.result?.contextId === CONTEXT
    && (send2.json.result.artifacts?.[0]?.parts?.map((p) => p.text).join('') ?? '').includes(marker2),
    JSON.stringify(send2.json?.result).slice(0, 200))

  // ===== A2A-S08 会话消息落库（两轮用户消息）=====
  const msgs = await api('GET', `/api/external/session/${CONTEXT}/messages`, { token: K1.key })
  const userMsgs = (msgs.json?.items ?? []).filter((m) => m.role === 'user')
  check('A2A-S08 会话消息落库（两轮用户消息）', msgs.status === 200 && userMsgs.length === 2, `status=${msgs.status} items=${(msgs.json?.items ?? []).length}`)

  // ===== A2A-S09 tasks/get =====
  const get1 = await a2a({ key: K1.key }, A1, { jsonrpc: '2.0', id: 7, method: 'tasks/get', params: { id: task1?.id } })
  check('A2A-S09a tasks/get 返回 completed Task',
    get1.status === 200 && get1.json?.result?.id === task1?.id && get1.json?.result?.status?.state === 'completed'
    && (get1.json.result.artifacts?.[0]?.parts?.map((p) => p.text).join('') ?? '').includes(marker),
    JSON.stringify(get1.json?.result).slice(0, 200))
  const ghost = await a2a({ key: K1.key }, A1, { jsonrpc: '2.0', id: 8, method: 'tasks/get', params: { id: '01924f5e-0000-7000-8000-00000000ffff' } })
  check('A2A-S09b 幽灵任务 -32001', ghost.json?.error?.code === -32001, JSON.stringify(ghost.json).slice(0, 140))

  // ===== A2A-S10 message/stream（SSE：working → artifact 增量 → completed final → 最终 Task）=====
  const streamMarker = `流式${TS}`
  const stream = await a2aStream({ key: K1.key }, A1, {
    jsonrpc: '2.0', id: 9, method: 'message/stream',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-3`, parts: [{ kind: 'text', text: streamMarker }] } },
  })
  const working = stream.events.find((e) => e.kind === 'task' && e.status?.state === 'working')
  const artifactEvents = stream.events.filter((e) => e.kind === 'artifact-update')
  const statusFinal = stream.events.find((e) => e.kind === 'status-update' && e.final === true)
  const streamedText = artifactText(stream.events)
  check('A2A-S10 message/stream 事件序列完整且文本含回显',
    stream.status === 200 && stream.contentType.includes('text/event-stream') && !!working && artifactEvents.length >= 1
    && statusFinal?.status?.state === 'completed' && stream.finalTask?.status?.state === 'completed'
    && isGuid(stream.finalTask?.contextId) && streamedText.includes(streamMarker),
    `status=${stream.status} events=${JSON.stringify(stream.events.map((e) => e.kind)).slice(0, 160)} text=${streamedText.slice(0, 60)}`)

  // ===== A2A-S11 tasks/cancel 中断慢速流 =====
  let cancelDone = false
  const streamC = await a2aStream({ key: K1.key }, A1, {
    jsonrpc: '2.0', id: 10, method: 'message/stream',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-4`, parts: [{ kind: 'text', text: `SLOW${TS}` }] } },
  }, async (events) => {
    if (cancelDone) return
    cancelDone = true
    // 从 working Task 事件取 taskId 后取消
    const workingTask = events.find((e) => e.kind === 'task')
    if (workingTask?.id) {
      await a2a({ key: K1.key }, A1, { jsonrpc: '2.0', id: 11, method: 'tasks/cancel', params: { id: workingTask.id } })
    }
  })
  const canceledFinal = streamC.finalTask?.status?.state === 'canceled' || streamC.events.some((e) => e.kind === 'status-update' && e.status?.state === 'canceled')
  check('A2A-S11 tasks/cancel 中断（canceled 状态）', canceledFinal, JSON.stringify(streamC.finalTask).slice(0, 200))

  // ===== A2A-S12 用户 token 路径（外部应用 A3）=====
  const EXT_UID = `a2a-user-${TS}`
  const tok = await api('POST', '/api/external/token', { body: { accessAppKey: K1.key, appId: A3, externalUserId: EXT_UID, nickname: 'a2a用户' } })
  const userToken = String(tok.json?.accessToken ?? '')
  const sendU = await a2a({ token: userToken }, A3, {
    jsonrpc: '2.0', id: 12, method: 'message/send',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-5`, parts: [{ kind: 'text', text: `用户token${TS}` }] } },
  })
  check('A2A-S12 用户 token message/send completed',
    tok.status === 200 && sendU.json?.result?.status?.state === 'completed', JSON.stringify(sendU.json?.result).slice(0, 160))

  // ===== A2A-S13 范围缓存立即性 =====
  await api('PUT', `/api/access-app/${K2.id}`, { token: owner.token, body: { name: `a2a-k2-${TS}`, scopes: ['wiki_read', 'app_a2a'] } })
  const granted = await a2a({ key: K2.key }, A1, {
    jsonrpc: '2.0', id: 13, method: 'message/send',
    params: { message: { role: 'user', kind: 'message', messageId: `m-${TS}-6`, parts: [{ kind: 'text', text: `授权后${TS}` }] } },
  })
  check('A2A-S13a 授权 app_a2a 后立即放行（K2 message/send completed）', granted.json?.result?.status?.state === 'completed', JSON.stringify(granted.json?.result).slice(0, 160))
  await api('PUT', `/api/access-app/${K2.id}`, { token: owner.token, body: { name: `a2a-k2-${TS}`, scopes: ['wiki_read'] } })
  const revoked = await a2a({ key: K2.key }, A1, { jsonrpc: '2.0', id: 14, method: 'tasks/get', params: { id: task1?.id } })
  check('A2A-S13b 吊销 app_a2a 后立即 403', revoked.status === 403 && revoked.json?.error?.code === 'insufficient_scope', JSON.stringify(revoked.json).slice(0, 120))

  // ===== A2A-S14 默认范围含 app_a2a =====
  const accList = await api('GET', `/api/access-app/list?teamId=${TID}`, { token: owner.token })
  const k3item = (accList.json?.items ?? []).find((i) => i.accessAppId === K3.id)
  check('A2A-S14 默认范围回显含 app_a2a', Array.isArray(k3item?.scopes) && k3item.scopes.includes('app_a2a'), JSON.stringify(k3item?.scopes))

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
