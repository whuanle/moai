// Agent 应用长期记忆 E2E（后端 127.0.0.1:5210）
// 覆盖：平台门禁（系统设置未开启长期记忆时应用开启 400）→ 配置保存与回显（模型/维度统一在系统设置，应用配置不携带）
//       → 门禁（非成员 404/管理员清空 Member 403）
//       → memory_save 工具显式写入 → mine 列表 → 自动提取（flush → MQ → mem0 两段：抽取+合并）落库
//       → 每轮相关记忆注入（MEMORY_HIT）→ 平台模型锁定（有任意记忆时换模型/换维度 400、应用关闭再开启不受影响）
//       → 用户隔离（B 不可见）→ 用户开关两态（跨应用功能已移除，记忆仅应用内使用）
//       → 单条删除/清空本应用/清空应用 → 清空后解锁（平台换模型+维度，恢复后写入可查询）
//       → 压缩参数下 flush 视图折叠工具结果（toolResultTriggerMessages=2）。
// 模型策略：本地 OpenAI 兼容桩（/v1/embeddings 确定性哈希向量 + /v1/chat/completions 提示词条件行为），零 SKIP。
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

const FACT_INVOICE = '用户的发票抬头是星辰科技有限公司，税号 91330100MEMO1234'
const FACT_CITY = '用户当前常驻城市是杭州'

// ===== 桩模型：embedding 确定性哈希向量 + chat 提示词条件行为 =====

function stubEmbed(text, dims = 1024) {
  const v = new Array(dims).fill(0)
  const tokens = String(text).match(/[\u4e00-\u9fa5A-Za-z0-9]+|./g) ?? [String(text)]
  for (const token of tokens) {
    for (const ch of token) {
      const h = crypto.createHash('md5').update(ch).digest()
      const idx = ((h[0] << 8) | h[1]) % dims
      v[idx] += (h[2] & 1) ? 1 : -1
    }
  }
  const norm = Math.sqrt(v.reduce((s, x) => s + x * x, 0)) || 1
  return v.map((x) => x / norm)
}

/** 捕获全部 chat 请求（供压缩断言检查消息历史） */
const chatLog = []

function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      if (String(req.url).includes('/embeddings')) {
        const inputs = Array.isArray(payload.input) ? payload.input : [String(payload.input ?? '')]
        const dims = Math.min(Number(payload.dimensions) || 1024, 1024)
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          object: 'list',
          model: payload.model ?? 'stub-embedding',
          data: inputs.map((text, index) => ({ object: 'embedding', index, embedding: stubEmbed(text, dims) })),
          usage: { prompt_tokens: 10, total_tokens: 10 },
        }))
        return
      }

      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      chatLog.push({ url: String(req.url), messages })

      // 1) 记忆事实抽取桩（mem0 第一段）
      if (allText.includes('你是对话记忆提取器')) {
        return sseReply(res, payload, JSON.stringify([
          { content: FACT_INVOICE, type: 'semantic', importance: 5 },
          { content: FACT_CITY, type: 'episodic', importance: 3 },
        ]))
      }

      // 2) 记忆合并决策桩（mem0 第二段）
      if (allText.includes('你是记忆合并器')) {
        return sseReply(res, payload, JSON.stringify([
          { action: 'ADD', id: '', content: FACT_INVOICE, type: 'semantic' },
          { action: 'ADD', id: '', content: FACT_CITY, type: 'episodic' },
        ]))
      }

      // 3) 子代理请求（独立上下文）：直接回结论
      if (allText.includes('[子代理模式]')) {
        return sseReply(res, payload, '子代理结论：Q3 数据整理完成，共 12 条记录，异常 0 条。')
      }

      // 4) 工具结果已在历史（role=tool）：回最终文案
      if (messages.some((m) => m.role === 'tool')) {
        return sseReply(res, payload, '已按要求完成记忆操作。')
      }

      // 5) 主代理：指令里出现 spawn_subagent（渐进披露列表）且用户要求委派 → 发起工具调用
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''
      if (allText.includes('spawn_subagent') && userText.includes('委派子代理')) {
        return sseToolCall(res, payload, 'spawn_subagent', JSON.stringify({ task: '整理Q3销售数据并给出结论' }))
      }

      // 6) 主代理：用户要求记住 → memory_save 工具调用
      if (allText.includes('memory_save') && (userText.includes('记住') || userText.includes('保存记忆'))) {
        const content = userText.replace(/^(请|帮我)?记住[:：]?/, '').trim() || '用户偏好简洁回复'
        return sseToolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'memory_save', argumentsJson: JSON.stringify({ content, type: 'semantic' }) }))
      }

      // 7) 指令中注入了长期记忆块 → 命中标记（验证按需激活）
      if (allText.includes('跨会话长期记忆')) {
        return sseReply(res, payload, 'MEMORY_HIT ' + userText.slice(0, 30))
      }

      // 8) 兜底普通回复
      return sseReply(res, payload, '好的，这是普通回复。')
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

/** 流式普通回复 */
function sseReply(res, payload, reply) {
  if (payload.stream) {
    res.writeHead(200, { 'Content-Type': 'text/event-stream' })
    const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: reply }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
    res.write('data: [DONE]\n\n')
    res.end()
    return
  }
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({
    id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
    choices: [{ index: 0, message: { role: 'assistant', content: reply }, finish_reason: 'stop' }],
    usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
  }))
}

/** 流式工具调用回复 */
function sseToolCall(res, payload, toolName, argsJson) {
  if (payload.stream) {
    res.writeHead(200, { 'Content-Type': 'text/event-stream' })
    const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', tool_calls: [{ index: 0, id: 'call_' + Date.now(), type: 'function', function: { name: toolName, arguments: '' } }] }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { tool_calls: [{ index: 0, function: { arguments: argsJson } }] }, finish_reason: null }] }) + '\n\n')
    res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] }) + '\n\n')
    res.write('data: [DONE]\n\n')
    res.end()
    return
  }
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({
    id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
    choices: [{ index: 0, message: { role: 'assistant', tool_calls: [{ id: 'call_' + Date.now(), type: 'function', function: { name: toolName, arguments: argsJson } }] }, finish_reason: 'tool_calls' }],
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

async function createStubModels(adminToken, TID, stubPort) {
  const chName = `mem-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'mem-stub-key', enabled: true, description: 'agent-memory E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null

  const mkModel = async (modelName, kind) => {
    await api('POST', '/api/ai/model', {
      token: adminToken,
      body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: kind, description: 'agent-memory E2E 桩模型' }, enabled: true, isPublic: false },
    })
    const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
    return String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  }

  const embeddingModelId = await mkModel(`mem-stub-embed-${TS}`, 'embedding')
  const embeddingModelId2 = await mkModel(`mem-stub-embed2-${TS}`, 'embedding')
  const conversationModelId = await mkModel(`mem-stub-chat-${TS}`, 'conversation')
  if (!embeddingModelId || !embeddingModelId2 || !conversationModelId) return null

  for (const modelId of [embeddingModelId, embeddingModelId2, conversationModelId]) {
    const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  }

  return { channelId: CH_ID, embeddingModelId, embeddingModelId2, conversationModelId }
}

async function cleanupStubModels(adminToken, channelId) {
  if (adminToken && channelId) await api('DELETE', `/api/ai/channel/${channelId}`, { token: adminToken })
}

const memorySettings = (extra = {}) => ({
  sandbox: { enabled: false },
  // 向量化模型/维度统一由系统设置（MEMORY_*）决定：应用配置不再携带这两个字段
  memory: { enabled: true, autoExtract: true, minScore: 0.2, ...extra },
})

async function saveConfig(token, appId, modelId, extra = {}) {
  return api('PUT', `/api/app/${appId}/agent-config`, {
    token,
    body: {
      prompt: '你是测试助手', wikiIds: [], plugins: [], modelId,
      executionSettings: memorySettings(extra),
    },
  })
}

async function saveSetting(token, key, value) {
  return api('PUT', '/api/settings', { token, body: { key, value } })
}

async function newSession(token, appId, title) {
  const r = await api('POST', `/api/app/${appId}/session`, { token, body: { title } })
  return String(r.json?.value ?? '')
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

async function mine(token, appId, page = 1, pageSize = 50) {
  return api('GET', `/api/app/${appId}/memory/mine?page=${page}&pageSize=${pageSize}`, { token })
}

async function pollMine(token, appId, predicate, timeoutMs = 60000) {
  const start = Date.now()
  let last = null
  while (Date.now() - start < timeoutMs) {
    last = await mine(token, appId)
    const items = last.json?.items ?? []
    if (predicate(items, last)) return { ok: true, items, last }
    await new Promise((r) => setTimeout(r, 2000))
  }
  const items = last?.json?.items ?? []
  return { ok: false, items, last }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const adminToken = await adminLogin()
  const owner = await mkuser('mo')
  const memberB = await mkuser('mb')
  const outsider = await mkuser('mx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'mem-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: memberB.userId, role: 0 } })

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = adminToken ? await createStubModels(adminToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  try {
    // ===== 配置校验（平台门禁：系统设置未开启长期记忆） =====
    const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '记忆应用-' + TS, appType: 'agent' } })
    const APP1 = String(ca.json?.value ?? '')

    const bad = await saveConfig(owner.token, APP1, models.conversationModelId)
    check('AM-01 平台未开启长期记忆时应用开启 400', bad.status === 400 && bad.text.includes('平台未开启长期记忆'), `${bad.status} ${bad.text.slice(0, 120)}`)

    // 平台记忆设置：先模型与维度，再开总开关（root；统一模型为桩 embedding 模型，已授权该团队）
    const setModel = await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', models.embeddingModelId)
    const setDims = await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')
    const setEnabled = await saveSetting(adminToken, 'MEMORY_ENABLED', 'true')
    check('AM-01b 平台记忆设置保存（模型/维度/开关）', setModel.status === 200 && setDims.status === 200 && setEnabled.status === 200,
      `${setModel.status}/${setDims.status}/${setEnabled.status} ${setModel.text.slice(0, 120)}`)
    const badEnabled = await saveSetting(adminToken, 'MEMORY_ENABLED', 'yes')
    check('AM-01c 平台开关非法值 400', badEnabled.status === 400, `${badEnabled.status} ${badEnabled.text.slice(0, 120)}`)
    const badModel = await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', 'not-a-guid')
    check('AM-01d 平台模型非法值 400', badModel.status === 400, `${badModel.status} ${badModel.text.slice(0, 120)}`)

    const good = await saveConfig(owner.token, APP1, models.conversationModelId)
    check('AM-02 记忆配置保存 200', good.status === 200, `${good.status} ${good.text.slice(0, 120)}`)

    const cfgBack = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('AM-03 记忆配置回显 enabled 且不携带模型/维度字段', cfgBack.json?.executionSettings?.memory?.enabled === true
      && cfgBack.json?.executionSettings?.memory?.embeddingModelId === undefined
      && cfgBack.json?.executionSettings?.memory?.embeddingDimensions === undefined,
      cfgBack.text.slice(0, 200))

    check('AM-04 发布应用 200', (await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })).status === 200)

    // ===== 门禁 =====
    check('AM-05 非成员查本人记忆 404', (await mine(outsider.token, APP1)).status === 404)
    check('AM-06 Member 清空应用记忆 403', (await api('DELETE', `/api/app/${APP1}/memory`, { token: memberB.token })).status === 403)

    // ===== 显式写入（memory_save 工具） =====
    const SID1 = await newSession(owner.token, APP1, 'am-工具写入')
    const chat1 = await chatSse(APP1, owner.token, SID1, '请记住：我的发票抬头是星辰科技有限公司')
    check('AM-07 memory_save 工具轮完成', chat1.status === 200 && !chat1.runError && chat1.reply.includes('记忆操作'), `reply=${chat1.reply.slice(0, 80)} err=${chat1.runError}`)

    const poll1 = await pollMine(owner.token, APP1, (items) => items.some((x) => (x.content ?? '').includes('发票抬头')))
    let toolResultDump = ''
    if (!poll1.ok) {
      const msgs1 = await api('GET', `/api/app/${APP1}/logs/${SID1}/messages`, { token: owner.token })
      toolResultDump = (msgs1.json?.items ?? [])
        .map((m) => `${m.role}:${String(m.content ?? '').slice(0, 150)}|calls=${String(m.toolCalls ?? '').slice(0, 120)}`)
        .join(' || ')
    }
    check('AM-08 工具写入落库可查询', poll1.ok, `count=${poll1.items.length} allMsgs=${toolResultDump}`)

    // ===== 自动提取（flush → MQ → 两段式） =====
    const SID2 = await newSession(owner.token, APP1, 'am-自动提取')
    await chatSse(APP1, owner.token, SID2, '我们来对一下账，公司抬头信息就按发票上的来，另外这个月我主要在杭州办公。')
    // 提取写入以税号 MEMO1234 标记（与工具写入的口语文本区分）；合并/去重由桩向量相似度决定条数，不做强断言
    const poll2 = await pollMine(owner.token, APP1, (items) => items.some((x) => (x.content ?? '').includes('MEMO1234')), 75000)
    if (!poll2.ok) {
      // 诊断：打印桩收到过的全部 chat 请求角色/片段（判断提取调用是否到达桩）
      chatLog.forEach((c, i) => console.log(`  [stub#${i}] ${c.url} :: ${c.messages.map((m) => `${m.role}:${String(m.content ?? '').slice(0, 60).replace(/\n/g, ' ')}`).join(' | ')}`))
    }
    check('AM-09 自动提取落库（抽取+合并）', poll2.ok, `count=${poll2.items.length} items=${poll2.items.map((x) => x.content?.slice(0, 20)).join('|')}`)

    // ===== 每轮相关记忆注入 =====
    const SID3 = await newSession(owner.token, APP1, 'am-注入')
    const chat3 = await chatSse(APP1, owner.token, SID3, '我的发票抬头是什么？')
    check('AM-10 相关记忆自动注入上下文', chat3.reply.includes('MEMORY_HIT'), `reply=${chat3.reply.slice(0, 80)}`)

    // ===== 平台模型/维度锁定（应用或全局任一记忆存在即锁，须先清空全部记忆；应用配置不再存模型，关闭再开启不受影响） =====
    const lockModel = await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', models.embeddingModelId2)
    check('AM-10b 已有记忆更换平台向量化模型 400', lockModel.status === 400 && lockModel.text.includes('清空'), `${lockModel.status} ${lockModel.text.slice(0, 140)}`)

    const lockDims = await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '768')
    check('AM-10c 已有记忆更换平台向量维度 400', lockDims.status === 400, `${lockDims.status} ${lockDims.text.slice(0, 140)}`)

    const disableMemory = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId, executionSettings: { memory: { enabled: false } } },
    })
    check('AM-10d 关闭记忆仍可保存 200', disableMemory.status === 200, `${disableMemory.status} ${disableMemory.text.slice(0, 140)}`)

    // 关闭态草稿不携带模型/维度（统一在系统设置），平台锁定不因应用关闭而解除
    const cfgDisabled = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('AM-10e 关闭态草稿不携带模型字段',
      cfgDisabled.json?.executionSettings?.memory?.enabled === false
      && cfgDisabled.json?.executionSettings?.memory?.embeddingModelId === undefined
      && cfgDisabled.json?.executionSettings?.memory?.embeddingDimensions === undefined,
      cfgDisabled.text.slice(0, 220))

    const reEnable = await saveConfig(owner.token, APP1, models.conversationModelId)
    check('AM-10f 重新开启记忆 200（模型口径随平台设置）', reEnable.status === 200, `${reEnable.status} ${reEnable.text.slice(0, 140)}`)

    // ===== 用户隔离 =====
    await api('POST', `/api/app/${APP1}/session`, { token: memberB.token, body: { title: 'b-session' } })
    const bSession = String((await api('GET', `/api/app/${APP1}/session/list`, { token: memberB.token })).json?.items?.[0]?.sessionId ?? '')
    const chatB = await chatSse(APP1, memberB.token, bSession, '我的发票抬头是什么？')
    const mineB = await mine(memberB.token, APP1)
    check('AM-11 用户B不可见A的记忆', !(chatB.reply ?? '').includes('MEMORY_HIT') && (mineB.json?.items ?? []).length === 0, `reply=${chatB.reply.slice(0, 60)} mineB=${mineB.text.slice(0, 80)}`)

    // ===== 用户模式（跨应用为用户自主设置，应用层无授权闸门）=====
    check('AM-12 历史值 memoryMode=3 接受（按开启生效）', (await api('PUT', `/api/app/${APP1}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryMode: 3 } })).status === 200)
    check('AM-12b 用户关闭记忆 200', (await api('PUT', `/api/app/${APP1}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryMode: 1 } })).status === 200)

    const SID4 = await newSession(owner.token, APP1, 'am-关闭')
    const chat4 = await chatSse(APP1, owner.token, SID4, '我的发票抬头是什么？')
    check('AM-14 关闭后不注入记忆', !(chat4.reply ?? '').includes('MEMORY_HIT'), `reply=${chat4.reply.slice(0, 60)}`)
    check('AM-15 关闭后数据保留', ((await mine(owner.token, APP1)).json?.items ?? []).length >= 2)

    // ===== 用户开关两态（跨应用功能已移除，记忆仅应用内使用）=====
    await api('PUT', `/api/app/${APP1}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryMode: 2 } })
    const SID5 = await newSession(owner.token, APP1, 'am-重新开启')
    const chat5 = await chatSse(APP1, owner.token, SID5, '我的发票抬头是什么？')
    check('AM-16 重新开启后记忆注入恢复', (chat5.reply ?? '').includes('MEMORY_HIT'), `reply=${chat5.reply.slice(0, 60)}`)

    // mine/all 端点已随跨应用功能下线
    check('AM-16b 清空全部端点已下线 404', (await api('DELETE', `/api/app/${APP1}/memory/mine/all`, { token: owner.token })).status === 404)

    // 等待最近一轮对话（AM-16 重新开启后的注入对话）的异步提取落地完成，避免清空后又被延迟写入
    await new Promise((r) => setTimeout(r, 9000))

    // ===== 单条删除 / 清空 =====
    const mineA = await mine(owner.token, APP1)
    const firstId = (mineA.json?.items ?? [])[0]?.id
    check('AM-19 删除单条记忆 200', firstId && (await api('DELETE', `/api/app/${APP1}/memory/mine/${firstId}`, { token: owner.token })).status === 200)
    check('AM-20 重复删除同条 404', firstId && (await api('DELETE', `/api/app/${APP1}/memory/mine/${firstId}`, { token: owner.token })).status === 404)

    check('AM-21 清空本应用记忆 200', (await api('DELETE', `/api/app/${APP1}/memory/mine`, { token: owner.token })).status === 200)
    const afterClear = await mine(owner.token, APP1)
    check('AM-22 清空后本人记忆为空', (afterClear.json?.items ?? []).length === 0, `residual=${JSON.stringify((afterClear.json?.items ?? []).map((x) => x.content))}`)

    // 再写入一条（工具路径）→ 团队管理员清空应用全部用户记忆
    const SID7 = await newSession(owner.token, APP1, 'am-管理员清空前')
    await chatSse(APP1, owner.token, SID7, '请记住：测试管理员清空场景')
    await pollMine(owner.token, APP1, (items) => items.length >= 1, 30000)
    // 等待该轮对话的异步提取落地完成，避免清空后又被延迟写入
    await new Promise((r) => setTimeout(r, 9000))
    check('AM-23 Owner 清空应用全部记忆 200', (await api('DELETE', `/api/app/${APP1}/memory`, { token: owner.token })).status === 200)
    check('AM-24 清空后应用内记忆为空', ((await mine(owner.token, APP1)).json?.items ?? []).length === 0)

    // ===== 清空后解锁：平台换模型/维度恢复可用，恢复口径后写入与检索正常 =====
    const unlockedModel = await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', models.embeddingModelId2)
    const unlockedDims = await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '768')
    check('AM-24b 清空后平台模型与维度可更换 200', unlockedModel.status === 200 && unlockedDims.status === 200, `${unlockedModel.status}/${unlockedDims.status} ${unlockedModel.text.slice(0, 140)}`)
    const restoreModel = await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', models.embeddingModelId)
    const restoreDims = await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')
    check('AM-24b2 平台模型与维度恢复 200', restoreModel.status === 200 && restoreDims.status === 200, `${restoreModel.status}/${restoreDims.status}`)
    check('AM-24c 重新发布 200', (await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })).status === 200)

    const SID9 = await newSession(owner.token, APP1, 'am-解锁写入')
    await chatSse(APP1, owner.token, SID9, '请记住：解锁后的记忆写入')
    const poll9 = await pollMine(owner.token, APP1, (items) => items.some((x) => (x.content ?? '').includes('解锁后的记忆写入')), 30000)
    check('AM-24d 解锁恢复后记忆写入可查询', poll9.ok, `count=${poll9.items.length} items=${poll9.items.map((x) => x.content?.slice(0, 20)).join('|')}`)

    // ===== 压缩：flush 视图折叠旧工具结果（toolResultTriggerMessages=2） =====
    const ca3 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '压缩应用-' + TS, appType: 'agent' } })
    const APP3 = String(ca3.json?.value ?? '')
    await api('PUT', `/api/app/${APP3}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId,
        executionSettings: { memory: { enabled: true, scopeDefault: 'app' }, toolResultTriggerMessages: 2, preserveTurns: 6, hardTokenLimit: 24000 },
      },
    })
    await api('POST', `/api/app/${APP3}/publish`, { token: owner.token })

    const SID8 = await newSession(owner.token, APP3, 'am-压缩')
    for (const round of ['请记住：压缩测试第一轮', '请记住：压缩测试第二轮', '请记住：压缩测试第三轮']) {
      await chatSse(APP3, owner.token, SID8, round)
    }
    // flush 为压缩后物理替换视图：3 轮工具结果后，保留的 tool 消息不应超过触发阈值
    await new Promise((r) => setTimeout(r, 4000))
    const msgs = await api('GET', `/api/app/${APP3}/logs/${SID8}/messages`, { token: owner.token })
    const toolMsgs = (msgs.json?.items ?? []).filter((m) => String(m.role).toLowerCase() === 'tool')
    check('AM-25 压缩后落库视图工具结果不超过阈值', toolMsgs.length > 0 && toolMsgs.length <= 2, `toolMsgs=${toolMsgs.length} total=${(msgs.json?.items ?? []).length}`)
  } finally {
    // 平台记忆设置恢复（脚本会临时开启 MEMORY_*，收尾还原避免污染开发实例）：
    // 先清空平台全部记忆（脚本遗留的应用记忆会锁住模型重置），再重置设置键
    if (adminToken) {
      await api('DELETE', '/api/memory/all', { token: adminToken })
      await saveSetting(adminToken, 'MEMORY_ENABLED', 'false')
      await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', '')
      await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')
    }
    await cleanupStubModels(adminToken, models.channelId)
    stub.close()
  }

  console.log(`\n===== agent-memory E2E 完成：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
