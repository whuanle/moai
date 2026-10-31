// 长期记忆向量化模型迁移 E2E（后端 127.0.0.1:5000，可用 argv[2] 覆盖）
// 覆盖：无记忆时迁移 400 → 校验 400（同模型维度/未知模型/维度越界）→ 非管理员 403
//       → 写入应用记忆（memory_save 工具）+ 全局记忆 → 迁移状态回显（锁定口径）
//       → 迁移期间写入 409 + 并发发起 409 → 迁移全链路（MQ 异步重嵌 → 临时表 → 换表 → 设置切换）
//       → 迁移后设置生效/记忆保留/向量检索命中（新维度）→ 失败迁移回滚（死渠道模型：任务失败、设置不变、数据完好）
//       → 失败后可重新发起并成功（重试链路）。
// 模型策略：本地 OpenAI 兼容桩（/v1/embeddings 按请求 dimensions 出确定性哈希向量 + /v1/chat/completions memory_save 条件行为），零 SKIP。
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

const FACT_INVOICE = '用户的发票抬头是星辰科技有限公司，税号 91330100MMIG5678'
const GLOBAL_FACT_1 = '用户偏好的编程语言是 TypeScript'
const GLOBAL_FACT_2 = '用户所在时区是 UTC+8'

// ===== 桩模型：embedding 按请求 dimensions 出确定性哈希向量（embed-b 模型故意放慢以撑开迁移窗口）=====

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

function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      if (String(req.url).includes('/embeddings')) {
        const inputs = Array.isArray(payload.input) ? payload.input : [String(payload.input ?? '')]
        const dims = Math.max(1, Math.min(Number(payload.dimensions) || 1024, 2000))
        const reply = () => {
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify({
            object: 'list',
            model: payload.model ?? 'stub-embedding',
            data: inputs.map((text, index) => ({ object: 'embedding', index, embedding: stubEmbed(text, dims) })),
            usage: { prompt_tokens: 10, total_tokens: 10 },
          }))
        }
        // 目标模型（embed-b）每次调用放慢 2s：撑开迁移进行中窗口供 409 断言
        if (String(payload.model ?? '').includes('embed-b')) {
          setTimeout(reply, 2000)
          return
        }
        reply()
        return
      }

      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''

      // 工具结果已在历史（role=tool）：回最终文案
      if (messages.some((m) => m.role === 'tool')) {
        return sseReply(res, payload, '已按要求完成记忆操作。')
      }

      // 用户要求记住 → memory_save 工具调用
      if (allText.includes('memory_save') && (userText.includes('记住') || userText.includes('保存记忆'))) {
        const content = userText.replace(/^(请|帮我)?记住[:：]?/, '').trim() || '用户偏好简洁回复'
        return sseToolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'memory_save', argumentsJson: JSON.stringify({ content, type: 'semantic' }) }))
      }

      // 指令中注入了长期记忆块 → 命中标记（迁移后新维度向量检索可用）
      if (allText.includes('跨会话长期记忆')) {
        return sseReply(res, payload, 'MEMORY_HIT ' + userText.slice(0, 30))
      }

      return sseReply(res, payload, '好的，这是普通回复。')
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

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

/** 建桩渠道 + 三个模型：embed-a（迁移源）、embed-b（迁移目标，慢响应）、chat（memory_save 工具轮） */
async function createStubModels(adminToken, TID, stubPort) {
  const chName = `mm-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'mm-stub-key', enabled: true, description: 'memory-migration E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null

  // 死渠道：指向无监听端口，迁移失败回滚场景用
  const deadName = `mm-dead渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: deadName, protocolFamily: 'openaiChatCompletions', baseUrl: 'http://127.0.0.1:9/v1', apiKey: 'mm-dead-key', enabled: true, description: 'memory-migration E2E 死渠道' },
  })
  const channels2 = await api('GET', '/api/ai/channel', { token: adminToken })
  const DEAD_CH_ID = String((channels2.json?.items ?? []).find((c) => c.name === deadName)?.id ?? '')

  const mkModel = async (channelId, modelName, kind, isPublic) => {
    await api('POST', '/api/ai/model', {
      token: adminToken,
      body: { channelId, meta: { modelId: modelName, name: modelName, modelKind: kind, description: 'memory-migration E2E 桩模型' }, enabled: true, isPublic },
    })
    const models = await api('GET', `/api/ai/model?channelId=${channelId}`, { token: adminToken })
    return String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  }

  const embeddingModelA = await mkModel(CH_ID, `mm-embed-a-${TS}`, 'embedding', true)
  const embeddingModelB = await mkModel(CH_ID, `mm-embed-b-${TS}`, 'embedding', true)
  const conversationModelId = await mkModel(CH_ID, `mm-chat-${TS}`, 'conversation', true)
  const embeddingModelDead = DEAD_CH_ID ? await mkModel(DEAD_CH_ID, `mm-embed-dead-${TS}`, 'embedding', true) : ''
  if (!embeddingModelA || !embeddingModelB || !conversationModelId || !DEAD_CH_ID) return null

  for (const modelId of [embeddingModelA, embeddingModelB, conversationModelId]) {
    const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  }

  return { channelId: CH_ID, deadChannelId: DEAD_CH_ID, embeddingModelA, embeddingModelB, embeddingModelDead, conversationModelId }
}

async function cleanupChannels(adminToken, channelIds) {
  for (const id of channelIds) {
    if (adminToken && id) await api('DELETE', `/api/ai/channel/${id}`, { token: adminToken })
  }
}

const memorySettings = (extra = {}) => ({
  sandbox: { enabled: false },
  // autoExtract 关闭：迁移期间不产生自动提取写入噪声，记忆写入走显式工具/接口
  memory: { enabled: true, autoExtract: false, minScore: 0.2, ...extra },
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

async function migrationStatus(token) {
  return api('GET', '/api/memory/model-migration', { token })
}

async function startMigration(token, modelId, dimensions) {
  return api('POST', '/api/memory/model-migration', { token, body: { modelId, dimensions } })
}

async function pollMigration(adminToken, predicate, timeoutMs = 60000) {
  const start = Date.now()
  let last = null
  while (Date.now() - start < timeoutMs) {
    last = await migrationStatus(adminToken)
    if (predicate(last.json ?? {})) return { ok: true, json: last.json }
    await new Promise((r) => setTimeout(r, 1000))
  }
  return { ok: false, json: last?.json ?? {} }
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

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'mm-team-' + TS } })).json.value)

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = adminToken ? await createStubModels(adminToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  try {
    // ===== 平台设置：模型 A（1024 维）+ 开启 =====
    check('MG-01a 平台设置模型A 200', (await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', models.embeddingModelA)).status === 200)
    check('MG-01b 平台设置维度 1024 200', (await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')).status === 200)
    check('MG-01c 平台开启记忆 200', (await saveSetting(adminToken, 'MEMORY_ENABLED', 'true')).status === 200)

    // ===== 无记忆时迁移 400（直接保存设置即可） =====
    const emptyStart = await startMigration(adminToken, models.embeddingModelB, 768)
    check('MG-02 无记忆时发起迁移 400', emptyStart.status === 400 && emptyStart.text.includes('暂无记忆'), `${emptyStart.status} ${emptyStart.text.slice(0, 120)}`)

    // ===== 写入记忆：应用记忆（工具）+ 全局记忆（接口） =====
    const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '迁移应用-' + TS, appType: 'agent' } })
    const APP1 = String(ca.json?.value ?? '')
    check('MG-03a 应用创建 200', isGuid(APP1), ca.text.slice(0, 120))
    check('MG-03b 记忆配置保存 200', (await saveConfig(owner.token, APP1, models.conversationModelId)).status === 200)
    check('MG-03c 发布应用 200', (await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })).status === 200)

    const SID1 = await newSession(owner.token, APP1, 'mm-工具写入')
    const chat1 = await chatSse(APP1, owner.token, SID1, `请记住：${FACT_INVOICE}`)
    check('MG-04a memory_save 工具轮完成', chat1.status === 200 && !chat1.runError && chat1.reply.includes('记忆操作'), `reply=${chat1.reply.slice(0, 80)} err=${chat1.runError}`)
    const poll1 = await pollMine(owner.token, APP1, (items) => items.some((x) => (x.content ?? '').includes('发票抬头')))
    check('MG-04b 应用记忆落库可查询', poll1.ok, `count=${poll1.items.length}`)

    const g1 = await api('POST', '/api/memory/global/mine', { token: owner.token, body: { content: GLOBAL_FACT_1, memoryType: 0 } })
    const g2 = await api('POST', '/api/memory/global/mine', { token: owner.token, body: { content: GLOBAL_FACT_2, memoryType: 1 } })
    check('MG-05 全局记忆写入 200', g1.status === 200 && g2.status === 200, `${g1.status}/${g2.status} ${g1.text.slice(0, 120)}`)

    // ===== 迁移状态回显（锁定口径） =====
    const st0 = await migrationStatus(adminToken)
    check('MG-06a 状态回显当前模型A/维度1024', st0.json?.currentModelId === models.embeddingModelA && st0.json?.currentDimensions === 1024, st0.text.slice(0, 200))
    check('MG-06b 状态回显记忆总数 >=3', (st0.json?.totalMemoryCount ?? 0) >= 3, `total=${st0.json?.totalMemoryCount}`)

    // ===== 校验 400 / 403 =====
    const sameStart = await startMigration(adminToken, models.embeddingModelA, 1024)
    check('MG-07a 同模型同维度 400', sameStart.status === 400 && sameStart.text.includes('一致'), `${sameStart.status} ${sameStart.text.slice(0, 120)}`)
    const unknownStart = await startMigration(adminToken, crypto.randomUUID(), 768)
    check('MG-07b 未知模型 400', unknownStart.status === 400 && unknownStart.text.includes('模型'), `${unknownStart.status} ${unknownStart.text.slice(0, 120)}`)
    const badDimsStart = await startMigration(adminToken, models.embeddingModelB, 0)
    check('MG-07c 维度越界 400', badDimsStart.status === 400, `${badDimsStart.status} ${badDimsStart.text.slice(0, 120)}`)
    const ownerStart = await startMigration(owner.token, models.embeddingModelB, 768)
    check('MG-07d 非管理员发起迁移 403', ownerStart.status === 403, `${ownerStart.status} ${ownerStart.text.slice(0, 120)}`)
    const ownerStatus = await migrationStatus(owner.token)
    check('MG-07e 非管理员查状态 403', ownerStatus.status === 403, `${ownerStatus.status}`)

    // ===== 发起迁移（A/1024 → B/768）：桩 embed-b 放慢 2s/批，撑开进行中窗口 =====
    const start1 = await startMigration(adminToken, models.embeddingModelB, 768)
    check('MG-08a 发起迁移 200 返回任务id', start1.status === 200 && isGuid(start1.json?.taskId), `${start1.status} ${start1.text.slice(0, 160)}`)

    // 迁移进行中：写入阻断 + 并发发起阻断（任务行已 Wait，写入守卫立即生效）
    const blockedGlobal = await api('POST', '/api/memory/global/mine', { token: owner.token, body: { content: '迁移期间尝试写入的记忆', memoryType: 0 } })
    check('MG-08b 迁移期间全局写入 409', blockedGlobal.status === 409 && blockedGlobal.text.includes('迁移'), `${blockedGlobal.status} ${blockedGlobal.text.slice(0, 120)}`)
    const concurrentStart = await startMigration(adminToken, models.embeddingModelDead, 512)
    check('MG-08c 迁移期间并发发起 409', concurrentStart.status === 409 && concurrentStart.text.includes('迁移任务'), `${concurrentStart.status} ${concurrentStart.text.slice(0, 120)}`)
    const stRunning = await migrationStatus(adminToken)
    check('MG-08d 进行中状态回显目标B/768', [1, 2].includes(stRunning.json?.activeMigration?.state)
      && stRunning.json?.activeMigration?.targetModelId === models.embeddingModelB
      && stRunning.json?.activeMigration?.targetDimensions === 768
      && (stRunning.json?.activeMigration?.totalCount ?? 0) >= 3, stRunning.text.slice(0, 260))

    // ===== 迁移完成：设置切换 + 数据保留 + 进度闭环 =====
    const poll2 = await pollMigration(adminToken, (s) => s.activeMigration?.state === 4, 90000)
    check('MG-09a 迁移任务成功终态', poll2.ok, `state=${poll2.json?.activeMigration?.state} msg=${poll2.json?.activeMigration?.message}`)
    check('MG-09b 进度闭环 migrated==total', poll2.json?.activeMigration?.migratedCount === poll2.json?.activeMigration?.totalCount
      && (poll2.json?.activeMigration?.migratedCount ?? 0) >= 3, `migrated=${poll2.json?.activeMigration?.migratedCount} total=${poll2.json?.activeMigration?.totalCount}`)

    const st1 = await migrationStatus(adminToken)
    check('MG-09c 设置已切换为模型B/768', st1.json?.currentModelId === models.embeddingModelB && st1.json?.currentDimensions === 768, st1.text.slice(0, 200))
    const settingsAfter = await api('GET', '/api/settings', { token: adminToken })
    const settingMap = Object.fromEntries((settingsAfter.json?.items ?? []).map((i) => [i.key, i.value]))
    check('MG-09d 设置表落库模型B/768', settingMap.MEMORY_EMBEDDING_MODEL === models.embeddingModelB && settingMap.MEMORY_EMBEDDING_DIMENSIONS === '768',
      `model=${settingMap.MEMORY_EMBEDDING_MODEL} dims=${settingMap.MEMORY_EMBEDDING_DIMENSIONS}`)

    const mineAfter = await mine(owner.token, APP1)
    check('MG-09e 应用记忆保留', (mineAfter.json?.items ?? []).some((x) => (x.content ?? '').includes('发票抬头')), `count=${(mineAfter.json?.items ?? []).length}`)
    const globalAfter = await api('GET', '/api/memory/global/mine?page=1&pageSize=50', { token: owner.token })
    const globalContents = (globalAfter.json?.items ?? []).map((x) => x.content ?? '')
    check('MG-09f 全局记忆保留', globalContents.some((c) => c.includes('TypeScript')) && globalContents.some((c) => c.includes('UTC+8')), `items=${globalContents.map((c) => c.slice(0, 14)).join('|')}`)
    check('MG-09g 迁移期间写入未落库', !globalContents.includes('迁移期间尝试写入的记忆'))

    // ===== 迁移后检索：新维度向量可召回（注入标记）+ 新写入按新维度 =====
    const SID2 = await newSession(owner.token, APP1, 'mm-迁移后检索')
    const chat2 = await chatSse(APP1, owner.token, SID2, '我的发票抬头是什么？')
    check('MG-10 迁移后向量检索命中', chat2.reply.includes('MEMORY_HIT'), `reply=${chat2.reply.slice(0, 80)} err=${chat2.runError}`)

    const SID3 = await newSession(owner.token, APP1, 'mm-迁移后写入')
    await chatSse(APP1, owner.token, SID3, '请记住：迁移后的新记忆写入')
    const poll3 = await pollMine(owner.token, APP1, (items) => items.some((x) => (x.content ?? '').includes('迁移后的新记忆写入')), 30000)
    check('MG-10b 迁移后新写入可查询', poll3.ok, `count=${poll3.items.length}`)

    // ===== 失败迁移回滚：死渠道模型（嵌入调用必败）→ 任务失败、设置不变、数据完好 =====
    // MM-10b 新增过一条记忆，失败前重取记忆总数作基准（迁移本身不改变条数）
    const totalBeforeFail = (await migrationStatus(adminToken)).json?.totalMemoryCount
    const failStart = await startMigration(adminToken, models.embeddingModelDead, 512)
    check('MG-11a 发起失败迁移 200', failStart.status === 200 && isGuid(failStart.json?.taskId), `${failStart.status} ${failStart.text.slice(0, 160)}`)
    const poll4 = await pollMigration(adminToken, (s) => s.activeMigration?.state === 5, 90000)
    check('MG-11b 失败任务终态 Failed', poll4.ok && (poll4.json?.activeMigration?.message ?? '').includes('迁移失败'), `state=${poll4.json?.activeMigration?.state} msg=${poll4.json?.activeMigration?.message?.slice(0, 120)}`)
    const st2 = await migrationStatus(adminToken)
    check('MG-11c 失败后设置不变（仍 B/768）', st2.json?.currentModelId === models.embeddingModelB && st2.json?.currentDimensions === 768, st2.text.slice(0, 200))
    check('MG-11d 失败后记忆总数不变', st2.json?.totalMemoryCount === totalBeforeFail, `total=${st2.json?.totalMemoryCount} before=${totalBeforeFail}`)
    const mineAfterFail = await mine(owner.token, APP1)
    check('MG-11e 失败后应用记忆完好', (mineAfterFail.json?.items ?? []).length === (mineAfter.json?.items ?? []).length + 1, `count=${(mineAfterFail.json?.items ?? []).length}`)

    // ===== 失败后重试链路：迁移回 A/1024 成功（收尾同时还原设置口径） =====
    const retryStart = await startMigration(adminToken, models.embeddingModelA, 1024)
    check('MG-12a 失败后重新发起迁移 200', retryStart.status === 200 && isGuid(retryStart.json?.taskId), `${retryStart.status} ${retryStart.text.slice(0, 160)}`)
    const poll5 = await pollMigration(adminToken, (s) => s.activeMigration?.state === 4, 90000)
    check('MG-12b 重试迁移成功', poll5.ok, `state=${poll5.json?.activeMigration?.state} msg=${poll5.json?.activeMigration?.message}`)
    const st3 = await migrationStatus(adminToken)
    check('MG-12c 重试后设置切换为模型A/1024', st3.json?.currentModelId === models.embeddingModelA && st3.json?.currentDimensions === 1024, st3.text.slice(0, 200))

    const SID4 = await newSession(owner.token, APP1, 'mm-回迁后检索')
    const chat3 = await chatSse(APP1, owner.token, SID4, '我的发票抬头是什么？')
    check('MG-12d 回迁后向量检索命中', chat3.reply.includes('MEMORY_HIT'), `reply=${chat3.reply.slice(0, 80)} err=${chat3.runError}`)
  } finally {
    // 收尾还原（脚本临时开启 MEMORY_* 并产生记忆，先清空全部记忆解锁再重置设置键，避免污染开发实例）
    if (adminToken) {
      await api('DELETE', '/api/memory/all', { token: adminToken })
      await saveSetting(adminToken, 'MEMORY_ENABLED', 'false')
      await saveSetting(adminToken, 'MEMORY_EMBEDDING_MODEL', '')
      await saveSetting(adminToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')
    }
    await cleanupChannels(adminToken, [models.channelId, models.deadChannelId])
    stub.close()
  }

  console.log(`\n===== memory-migration E2E 完成：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
