// 个人记忆管理 E2E（跨应用全局记忆，后端默认 127.0.0.1:5000）
// 覆盖：人工写入端点下线断言（新建/编辑全局、应用记忆编辑、「存为全局」promote → 404/405；记忆内容一律由 AI 自动维护）
//       → 平台记忆设置门禁与校验（MEMORY_*：未选模型开启 400/非法值 400/非 root 403；应用配置不携带模型/维度字段）
//       → 全局记忆仅 AI 写入（memory_save scope=global 工具轮落库）
//       → 记忆总览 overview（应用列表 + 全局概况）与关键字检索（全局/应用记忆列表 keyword 过滤）
//       → 跨应用注入（应用 B 对话命中 A 时代入的全局记忆，注入行带「全局|」标记；用户隔离）
//       → 统一向量化模型锁定（任意记忆存在换模型 400 → root 清空全部（应用+全局）解锁 → 换模型 200）
//       → 删除与清空（唯一保留的人工操作：删单条/重复删除 404/清空本人全局）。
// 模型策略：本地 OpenAI 兼容桩（确定性哈希向量 embedding + 提示词条件 chat），零 SKIP；统一模型须为公开 embedding 模型。
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
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf-8')).toString('base64')
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

const FACT_INVOICE = '用户的全局偏好：发票抬头是星辰科技有限公司'

// ===== 桩模型：embedding 确定性哈希向量 + chat 提示词条件行为 =====

/** 捕获全部 chat 请求（供失败诊断） */
const chatLog = []

/** 已发起过 memory_forget 的全局记忆 id（防工具循环；会话落库后工具结果可能被折叠，需从本会话先前请求提取 id） */
const forgottenGlobalIds = new Set()

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
        const dims = Math.min(Number(payload.dimensions) || 1024, 1024)
        const vectors = inputs.map((text) => stubEmbed(text, dims))
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          object: 'list',
          model: payload.model ?? 'stub-embedding',
          data: vectors.map((embedding, index) => ({ object: 'embedding', index, embedding })),
          usage: { prompt_tokens: 10, total_tokens: 10 },
        }))
        return
      }

      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      chatLog.push({ url: String(req.url), tools: payload.tools ?? [], messages })

      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''

      // 1) 用户要求忘掉全局记忆 → memory_forget（最具体，优先于通用工具结果回复）；
      //    全局记忆 id 从本会话先前请求的工具结果文本提取（落库历史可能折叠工具结果）；
      //    已发起过删除的 id 不再重复发起，避免工具循环
      if (messages.some((m) => m.role === 'tool') && (userText.includes('忘') || userText.includes('删除'))) {
        const toolTexts = chatLog.flatMap((c) => c.messages.filter((m) => m.role === 'tool').map((m) => String(m.content ?? '')))
        const globalId = toolTexts
          .flatMap((c) => [...c.matchAll(/已保存全局记忆 ([0-9a-f-]{36})/g)].map((x) => x[1]))
          .find((id) => !forgottenGlobalIds.has(id))
        if (globalId) {
          forgottenGlobalIds.add(globalId)
          return sseToolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'memory_forget', argumentsJson: JSON.stringify({ memoryId: globalId }) }))
        }
      }

      // 2) 工具结果已在历史（role=tool）：回最终文案
      if (messages.some((m) => m.role === 'tool')) {
        return sseReply(res, payload, '已按要求完成记忆操作。')
      }

      // 3) 主代理：用户要求记住 → memory_save 工具调用（「全局记住」携带 scope=global；优先于记忆命中判定，确保保存意图先执行）
      if (allText.includes('memory_save') && (userText.includes('记住') || userText.includes('保存记忆'))) {
        const isGlobal = userText.includes('全局记住')
        const content = userText
          .replace(/^(请|帮我)?(全局)?记住[:：]?/, '')
          .replace(/^(请|帮我)?记住[:：]?/, '')
          .trim() || '用户偏好简洁回复'
        const args = isGlobal
          ? { content, type: 'semantic', scope: 'global' }
          : { content, type: 'semantic' }
        return sseToolCall(res, payload, 'call_tool', JSON.stringify({ toolName: 'memory_save', argumentsJson: JSON.stringify(args) }))
      }

      // 4) 指令注入了「全局|」标记的记忆行 → 全局记忆跨应用命中
      if (allText.includes('全局|')) {
        return sseReply(res, payload, 'GLOBAL_MEMORY_HIT')
      }

      // 4) 指令注入了长期记忆块（应用内）→ 命中标记
      if (allText.includes('跨会话长期记忆')) {
        return sseReply(res, payload, 'MEMORY_HIT')
      }

      // 5) 兜底普通回复
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

async function rootLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.status === 200 ? l.json.accessToken : null
}

async function mkuser(p) {
  const name = uname(p)
  await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function createStubModels(rootToken, TID, stubPort) {
  const chName = `mgmt-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: rootToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'mgmt-stub-key', enabled: true, description: 'memory-management E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: rootToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) return null

  const mkModel = async (modelName, kind, isPublic) => {
    await api('POST', '/api/ai/model', {
      token: rootToken,
      body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: kind, description: 'memory-management E2E 桩模型' }, enabled: true, isPublic },
    })
    const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: rootToken })
    return String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  }

  // 全局记忆模型须为公开 embedding 模型（teamId=0 解析口径）；对话模型按团队授权
  const publicEmbeddingId = await mkModel(`mgmt-stub-pub-embed-${TS}`, 'embedding', true)
  const publicEmbeddingId2 = await mkModel(`mgmt-stub-pub-embed2-${TS}`, 'embedding', true)
  const conversationModelId = await mkModel(`mgmt-stub-chat-${TS}`, 'conversation', false)
  if (!publicEmbeddingId || !publicEmbeddingId2 || !conversationModelId) return null

  const cur = await api('GET', `/api/ai/model/${conversationModelId}/authorization`, { token: rootToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${conversationModelId}/authorization`, { token: rootToken, body: { modelId: conversationModelId, teamIds } })

  return { channelId: CH_ID, publicEmbeddingId, publicEmbeddingId2, conversationModelId }
}

const saveSetting = (rootToken, key, value) => api('PUT', '/api/settings', { token: rootToken, body: { key, value } })

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

const globalMine = (token, page = 1, pageSize = 50) => api('GET', `/api/memory/global/mine?page=${page}&pageSize=${pageSize}`, { token })
const appMine = (token, appId, page = 1, pageSize = 50) => api('GET', `/api/app/${appId}/memory/mine?page=${page}&pageSize=${pageSize}`, { token })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const rootToken = await rootLogin()
  const owner = await mkuser('gm')
  const userB = await mkuser('gb')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'mgmt-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: userB.userId, role: 0 } })

  const stub = await startStubServer()
  const stubPort = stub.address().port
  const models = rootToken ? await createStubModels(rootToken, TID, stubPort) : null
  if (!models) { console.log('FAIL | 桩模型创建失败，终止'); process.exit(1) }

  try {
    // 起始收敛：确保平台记忆开关为关且未选模型（上一次运行可能残留开启态/模型值），再验证默认门禁
    await saveSetting(rootToken, 'MEMORY_ENABLED', 'false')
    await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'false')
    await saveSetting(rootToken, 'MEMORY_EMBEDDING_MODEL', '')

    // ===== 人工写入端点下线断言（记忆内容一律由 AI 自动维护，人工仅可删除/清空） =====
    // 路由模板仍在（同模板保留 GET/DELETE），下线后统一返回 404/405
    check('MM-01a 新建全局记忆端点已下线 404/405', [404, 405].includes((await api('POST', '/api/memory/global/mine', { token: owner.token, body: { content: 'x', memoryType: 0 } })).status))
    check('MM-01b 存为全局 promote 端点已下线 404/405', [404, 405].includes((await api('POST', '/api/memory/global/promote', { token: owner.token, body: { appId: crypto.randomUUID(), memoryId: crypto.randomUUID() } })).status))
    check('MM-01c 编辑全局记忆端点已下线 404/405', [404, 405].includes((await api('PUT', `/api/memory/global/mine/${crypto.randomUUID()}`, { token: owner.token, body: { memoryId: crypto.randomUUID(), content: 'x', memoryType: 0 } })).status))
    check('MM-01d 编辑应用记忆端点已下线 404/405', [404, 405].includes((await api('PUT', `/api/app/${crypto.randomUUID()}/memory/mine/${crypto.randomUUID()}`, { token: owner.token, body: { appId: crypto.randomUUID(), memoryId: crypto.randomUUID(), content: 'x', memoryType: 0 } })).status))

    check('MM-02 非 root 保存平台记忆设置 403', (await saveSetting(owner.token, 'MEMORY_ENABLED', 'true')).status === 403)

    const noModel = await saveSetting(rootToken, 'MEMORY_ENABLED', 'true')
    check('MM-03 未选模型直接开启 400', noModel.status === 400, `${noModel.status} ${noModel.text.slice(0, 120)}`)

    check('MM-04 非法开关值 400', (await saveSetting(rootToken, 'MEMORY_ENABLED', 'yes')).status === 400)
    check('MM-05 非法模型 id 400', (await saveSetting(rootToken, 'MEMORY_EMBEDDING_MODEL', 'not-a-guid')).status === 400)
    check('MM-06 非法维度 400', (await saveSetting(rootToken, 'MEMORY_EMBEDDING_DIMENSIONS', '4096')).status === 400)
    check('MM-06b 跨应用记忆开关非法值 400', (await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'yes')).status === 400)
    const globalEarly = await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'true')
    check('MM-06c 未开启总开关时开启跨应用记忆 400', globalEarly.status === 400 && globalEarly.text.includes('长期记忆'), `${globalEarly.status} ${globalEarly.text.slice(0, 120)}`)

    const options = await api('GET', '/api/memory/global/model-options', { token: rootToken })
    const optionIds = (options.json?.items ?? []).map((i) => String(i.id))
    check('MM-07 root 查询可选模型含公开桩模型', options.status === 200 && optionIds.includes(models.publicEmbeddingId) && !optionIds.includes(models.conversationModelId),
      `${options.status} ids=${optionIds.length}`)

    check('MM-08 非 root 查询可选模型 403', (await api('GET', '/api/memory/global/model-options', { token: owner.token })).status === 403)

    // 保存顺序：先模型与维度，再开关
    await saveSetting(rootToken, 'MEMORY_EMBEDDING_MODEL', models.publicEmbeddingId)
    await saveSetting(rootToken, 'MEMORY_EMBEDDING_DIMENSIONS', '1024')
    check('MM-09 选定模型后开启长期记忆 200', (await saveSetting(rootToken, 'MEMORY_ENABLED', 'true')).status === 200)
    check('MM-09b 总开关开启后跨应用记忆开关开启 200', (await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'true')).status === 200)

    // ===== 应用记忆：AI 工具写入 + 全局记忆工具写入（人工写入端点已下线） =====
    const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '记忆应用A-' + TS, appType: 'agent' } })
    const APP1 = String(ca.json?.value ?? '')

    // 应用跨应用共享门禁：平台 MEMORY_GLOBAL_ENABLED 关闭时应用开启 allowGlobalMemory 400；开启后放行
    await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'false')
    const cfgGlobalOff = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId,
        executionSettings: { sandbox: { enabled: false }, memory: { enabled: true, allowGlobalMemory: true, autoExtract: false, minScore: 0.2 } },
      },
    })
    check('MM-18a 平台未开启跨应用记忆时应用开启全局共享 400', cfgGlobalOff.status === 400 && cfgGlobalOff.text.includes('平台未开启跨应用记忆'), `${cfgGlobalOff.status} ${cfgGlobalOff.text.slice(0, 140)}`)
    check('MM-09c 平台跨应用记忆开关恢复开启 200', (await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'true')).status === 200)

    const cfg1 = await api('PUT', `/api/app/${APP1}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId,
        // 携带模型/维度字段也会被剥离：统一在系统设置；allowGlobalMemory 供工具 scope=global 写入
        executionSettings: { sandbox: { enabled: false }, memory: { enabled: true, allowGlobalMemory: true, embeddingModelId: models.publicEmbeddingId, embeddingDimensions: 1024, autoExtract: false, minScore: 0.2 } },
      },
    })
    const pub1 = await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })
    if (cfg1.status !== 200 || pub1.status !== 200) {
      console.log(`FAIL | MM-18 前置失败 cfg=${cfg1.status}/${cfg1.text.slice(0, 120)} pub=${pub1.status}/${pub1.text.slice(0, 120)}`)
      FAIL++
    }

    const cfg1Back = await api('GET', `/api/app/${APP1}/agent-config`, { token: owner.token })
    check('MM-18b 应用配置回显不含模型/维度字段（统一系统设置）', cfg1Back.json?.executionSettings?.memory?.enabled === true
      && cfg1Back.json?.executionSettings?.memory?.embeddingModelId === undefined
      && cfg1Back.json?.executionSettings?.memory?.embeddingDimensions === undefined,
      cfg1Back.text.slice(0, 200))

    const SID1 = await newSession(owner.token, APP1, 'mm-写入')
    const chat1 = await chatSse(APP1, owner.token, SID1, '请记住：用户的收货地址是杭州未来科技城')

    let appItems = []
    for (let i = 0; i < 15; i++) {
      const r = await appMine(owner.token, APP1)
      appItems = r.json?.items ?? []
      if (appItems.some((x) => (x.content ?? '').includes('收货地址'))) break
      await new Promise((r2) => setTimeout(r2, 2000))
    }
    const appItem = appItems.find((x) => (x.content ?? '').includes('收货地址'))
    check('MM-18 应用记忆写入可查询', !!appItem, `count=${appItems.length} chat=${chat1.status} reply=${(chat1.reply ?? '').slice(0, 80)} err=${chat1.runError}`)

    if (!appItem) {
      // 应用记忆写入失败时，后续全局写入/注入链路无法继续，提前收尾
      chatLog.forEach((c, i) => console.log(`  [stub#${i}] tools=${JSON.stringify((c.tools ?? []).map?.((t2) => t2?.function?.name ?? t2?.name ?? '?'))} msgs=${JSON.stringify(c.messages.map((m) => ({ role: m.role, len: String(typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')).length, head: String(typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')).slice(0, 80) })))}`))
      console.log('\n===== memory-management E2E 提前终止：应用记忆未写入 =====')
      await api('DELETE', '/api/memory/all', { token: rootToken })
      await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'false')
      await saveSetting(rootToken, 'MEMORY_ENABLED', 'false')
      if (rootToken && models?.channelId) await api('DELETE', `/api/ai/channel/${models.channelId}`, { token: rootToken })
      stub.close()
      console.log(`\n===== memory-management E2E 完成：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
      process.exit(1)
    }

    // 全局记忆同样仅 AI 写入：工具轮 scope=global 落库（原人工新建端点已下线）
    const SIDG1 = await newSession(owner.token, APP1, 'mm-全局写入')
    const chatSeedGlobal = await chatSse(APP1, owner.token, SIDG1, '请全局记住：' + FACT_INVOICE)
    const pollSeedGlobal = await (async () => {
      for (let i = 0; i < 15; i++) {
        const r = await globalMine(owner.token)
        if ((r.json?.items ?? []).some((x) => (x.content ?? '').includes('星辰科技'))) return true
        await new Promise((r2) => setTimeout(r2, 2000))
      }
      return false
    })()
    check('MM-12b 全局记忆仅 AI 工具写入（scope=global）落库可查询', chatSeedGlobal.status === 200 && !chatSeedGlobal.runError && pollSeedGlobal,
      `reply=${(chatSeedGlobal.reply ?? '').slice(0, 80)} err=${chatSeedGlobal.runError}`)

    // ===== 记忆总览 =====
    const overview = await api('GET', '/api/memory/overview', { token: owner.token })
    const ovApps = overview.json?.apps ?? []
    const ovApp1 = ovApps.find((a) => String(a.appId) === APP1)
    check('MM-27 总览含应用与计数', overview.status === 200 && ovApp1 && (ovApp1.memoryCount ?? 0) >= 1 && (ovApp1.appName ?? '').includes('记忆应用A') && (ovApp1.teamName ?? '').length > 0,
      `apps=${ovApps.length} ovApp1=${JSON.stringify(ovApp1 ?? null).slice(0, 140)}`)
    const mineForOverview = await globalMine(owner.token)
    check('MM-28 总览全局概况计数与列表一致', (overview.json?.global?.enabled ?? false) === true && (overview.json?.global?.myCount ?? 0) === (mineForOverview.json?.total ?? 0),
      `global=${JSON.stringify(overview.json?.global ?? null)} listTotal=${mineForOverview.json?.total}`)

    // ===== 工具栏关键字检索（服务端 keyword 过滤） =====
    const globalKwHit = await api('GET', '/api/memory/global/mine?page=1&pageSize=50&keyword=' + encodeURIComponent('星辰'), { token: owner.token })
    check('MM-28b 全局记忆按内容关键字命中', globalKwHit.status === 200 && (globalKwHit.json?.total ?? 0) >= 1 && (globalKwHit.json?.items ?? []).every((x) => (x.content ?? '').includes('星辰')),
      `total=${globalKwHit.json?.total}`)
    const globalKwMiss = await api('GET', '/api/memory/global/mine?page=1&pageSize=50&keyword=' + encodeURIComponent('无匹配关键字xyz'), { token: owner.token })
    check('MM-28c 全局记忆无匹配关键字返回 0', globalKwMiss.status === 200 && (globalKwMiss.json?.total ?? 0) === 0, `total=${globalKwMiss.json?.total}`)
    const appKwHit = await api('GET', `/api/app/${APP1}/memory/mine?page=1&pageSize=50&keyword=` + encodeURIComponent('收货'), { token: owner.token })
    check('MM-28d 应用记忆按内容关键字命中', appKwHit.status === 200 && (appKwHit.json?.total ?? 0) >= 1 && (appKwHit.json?.items ?? []).every((x) => (x.content ?? '').includes('收货')),
      `total=${appKwHit.json?.total}`)

    // ===== 跨应用注入：应用 B（无应用内记忆）命中全局记忆，三级开关（平台已开启 → 管理员允许 → 用户接入）=====
    const cb = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '记忆应用B-' + TS, appType: 'agent' } })
    const APP2 = String(cb.json?.value ?? '')
    await api('PUT', `/api/app/${APP2}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId,
        executionSettings: { sandbox: { enabled: false }, memory: { enabled: true, autoExtract: false, minScore: 0.15 } },
      },
    })
    await api('POST', `/api/app/${APP2}/publish`, { token: owner.token })

    // 管理员未允许接入全局记忆：即使平台已开启、用户默认接入，也不注入（userconfig 回显 memoryGlobalAllowed=false）
    const ucBefore = await api('GET', `/api/app/${APP2}/userconfig`, { token: owner.token })
    check('MM-29 未允许时 userconfig 回显 memoryGlobalAllowed=false', ucBefore.json?.memoryGlobalAllowed === false && ucBefore.json?.memoryGlobalMode === 0,
      `uc=${JSON.stringify({ allowed: ucBefore.json?.memoryGlobalAllowed, mode: ucBefore.json?.memoryGlobalMode })}`)

    const SID2 = await newSession(owner.token, APP2, 'mm-跨应用')
    const chatNoAllow = await chatSse(APP2, owner.token, SID2, '我的发票抬头是什么？')
    check('MM-29b 管理员未允许时不注入全局记忆', chatNoAllow.status === 200 && !(chatNoAllow.reply ?? '').includes('GLOBAL_MEMORY_HIT'),
      `reply=${(chatNoAllow.reply ?? '').slice(0, 80)}`)

    // 管理员开启「允许用户接入全局记忆」并重新发布
    const allowCfg = await api('PUT', `/api/app/${APP2}/agent-config`, {
      token: owner.token,
      body: {
        prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId,
        executionSettings: { sandbox: { enabled: false }, memory: { enabled: true, allowGlobalMemory: true, autoExtract: false, minScore: 0.15 } },
      },
    })
    const allowPublish = await api('POST', `/api/app/${APP2}/publish`, { token: owner.token })
    check('MM-29c 允许接入配置保存并发布 200', allowCfg.status === 200 && allowPublish.status === 200, `cfg=${allowCfg.status} pub=${allowPublish.status}`)

    const ucAllowed = await api('GET', `/api/app/${APP2}/userconfig`, { token: owner.token })
    check('MM-29d 允许后 userconfig 回显 memoryGlobalAllowed=true', ucAllowed.json?.memoryGlobalAllowed === true, `allowed=${ucAllowed.json?.memoryGlobalAllowed}`)

    // 用户关闭全局接入（memoryGlobalMode=1）：允许后仍不注入
    check('MM-29e 用户关闭全局接入 200', (await api('PUT', `/api/app/${APP2}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryMode: 2, memoryGlobalMode: 1 } })).status === 200)
    const ucOff = await api('GET', `/api/app/${APP2}/userconfig`, { token: owner.token })
    check('MM-29f 关闭接入回显 memoryGlobalMode=1', ucOff.json?.memoryGlobalMode === 1, `mode=${ucOff.json?.memoryGlobalMode}`)

    const chatUserOff = await chatSse(APP2, owner.token, SID2, '我的发票抬头是什么？')
    check('MM-29g 用户关闭接入后不注入全局记忆', !(chatUserOff.reply ?? '').includes('GLOBAL_MEMORY_HIT'), `reply=${(chatUserOff.reply ?? '').slice(0, 80)}`)

    // 用户恢复默认接入（memoryGlobalMode=0）→ 注入生效
    await api('PUT', `/api/app/${APP2}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryMode: 2, memoryGlobalMode: 0 } })
    const chat2 = await chatSse(APP2, owner.token, SID2, '我的发票抬头是什么？')
    check('MM-29h 恢复接入后全局记忆跨应用注入', chat2.status === 200 && (chat2.reply ?? '').includes('GLOBAL_MEMORY_HIT'),
      `status=${chat2.status} reply=${(chat2.reply ?? '').slice(0, 80)} err=${chat2.runError}`)

    // 未开启记忆的应用设置全局接入偏好被拒绝（400）
    const ca3 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '记忆应用C-' + TS, appType: 'agent' } })
    const APP3 = String(ca3.json?.value ?? '')
    await api('PUT', `/api/app/${APP3}/agent-config`, {
      token: owner.token,
      body: { prompt: '你是测试助手', wikiIds: [], plugins: [], modelId: models.conversationModelId, executionSettings: { sandbox: { enabled: false } } },
    })
    await api('POST', `/api/app/${APP3}/publish`, { token: owner.token })
    check('MM-29i 未开启记忆的应用设置全局接入 400', (await api('PUT', `/api/app/${APP3}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryGlobalMode: 1 } })).status === 400)
    check('MM-29j 全局接入模式非法值 400', (await api('PUT', `/api/app/${APP2}/userconfig`, { token: owner.token, body: { promptId: 0, skills: [], memoryGlobalMode: 5 } })).status === 400)

    const SIDB = await newSession(userB.token, APP2, 'mm-隔离')
    const chatB = await chatSse(APP2, userB.token, SIDB, '我的发票抬头是什么？')
    const mineB2 = await globalMine(userB.token)
    const bHasInvoice = (mineB2.json?.items ?? []).some((x) => (x.content ?? '').includes('星辰科技'))
    check('MM-30 跨应用注入按用户隔离', !(chatB.reply ?? '').includes('GLOBAL_MEMORY_HIT') && !bHasInvoice, `reply=${(chatB.reply ?? '').slice(0, 60)}`)

    // ===== 平台跨应用记忆开关运行时收口：关闭后存量 allowGlobalMemory=true 的应用不再注入/回显全局 =====
    check('MM-30b 平台关闭跨应用记忆 200', (await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'false')).status === 200)
    const chatGateOff = await chatSse(APP2, owner.token, SID2, '我的发票抬头是什么？')
    check('MM-30c 平台关闭后存量允许应用不注入全局记忆', !(chatGateOff.reply ?? '').includes('GLOBAL_MEMORY_HIT'), `reply=${(chatGateOff.reply ?? '').slice(0, 80)}`)
    const ucGateOff = await api('GET', `/api/app/${APP2}/userconfig`, { token: owner.token })
    check('MM-30d 平台关闭后 userconfig 回显 memoryGlobalAllowed=false', ucGateOff.json?.memoryGlobalAllowed === false, `allowed=${ucGateOff.json?.memoryGlobalAllowed}`)
    check('MM-30e 平台恢复开启跨应用记忆 200', (await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'true')).status === 200)

    // ===== 记忆工具集：全局保存（scope=global）与全局遗忘（memory_forget 全局 id 兜底）=====
    const SIDG = await newSession(owner.token, APP2, 'mm-全局工具')
    const globalTotalBeforeTool = (await globalMine(owner.token)).json?.total ?? 0
    const chatSaveGlobal = await chatSse(APP2, owner.token, SIDG, '请全局记住：我周末常去西湖骑行')
    const pollGlobalSaved = await (async () => {
      for (let i = 0; i < 15; i++) {
        const r = await globalMine(owner.token)
        if ((r.json?.items ?? []).some((x) => (x.content ?? '').includes('西湖骑行'))) return true
        await new Promise((r2) => setTimeout(r2, 2000))
      }
      return false
    })()
    check('MM-44 memory_save scope=global 写入全局记忆', chatSaveGlobal.status === 200 && !chatSaveGlobal.runError && pollGlobalSaved,
      `reply=${(chatSaveGlobal.reply ?? '').slice(0, 80)} err=${chatSaveGlobal.runError}`)
    const app2MineAfterTool = await appMine(owner.token, APP2)
    check('MM-44b 全局保存不写入应用内记忆', !(app2MineAfterTool.json?.items ?? []).some((x) => (x.content ?? '').includes('西湖骑行')),
      `appItems=${(app2MineAfterTool.json?.items ?? []).map((x) => x.content?.slice(0, 10)).join('|')}`)

    // 同会话第二轮：要求忘记刚才那条全局记忆（工具结果历史携带全局记忆 id，桩提取后发起 memory_forget）
    const chatForgetGlobal = await chatSse(APP2, owner.token, SIDG, '把我刚才那条全局记忆忘掉')
    const pollGlobalForgotten = await (async () => {
      for (let i = 0; i < 15; i++) {
        const r = await globalMine(owner.token)
        if (!(r.json?.items ?? []).some((x) => (x.content ?? '').includes('西湖骑行'))) return true
        await new Promise((r2) => setTimeout(r2, 2000))
      }
      return false
    })()
    check('MM-45 memory_forget 兜底删除全局记忆', chatForgetGlobal.status === 200 && !chatForgetGlobal.runError && pollGlobalForgotten,
      `reply=${(chatForgetGlobal.reply ?? '').slice(0, 80)} err=${chatForgetGlobal.runError} msgs=${JSON.stringify((chatLog.slice(-2)).map((c) => c.messages.map((m) => `${m.role}:${String(typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')).slice(0, 90)}`).join(' | ')))}`)

    // 再写入一条全局记忆供删除/清空用例使用（人工写入端点已下线，只能走 AI 工具）
    const SIDG2 = await newSession(owner.token, APP2, 'mm-全局写入2')
    const chatSeedGlobal2 = await chatSse(APP2, owner.token, SIDG2, '请全局记住：我偏好纸质说明书')
    const pollSeedGlobal2 = await (async () => {
      for (let i = 0; i < 15; i++) {
        const r = await globalMine(owner.token)
        if ((r.json?.items ?? []).some((x) => (x.content ?? '').includes('纸质说明书'))) return true
        await new Promise((r2) => setTimeout(r2, 2000))
      }
      return false
    })()
    check('MM-45b 工具再次写入全局记忆', chatSeedGlobal2.status === 200 && !chatSeedGlobal2.runError && pollSeedGlobal2,
      `reply=${(chatSeedGlobal2.reply ?? '').slice(0, 80)} err=${chatSeedGlobal2.runError}`)

    // ===== 删除与清空（唯一保留的人工操作） =====
    const mineForDel = await globalMine(owner.token)
    const delTarget = (mineForDel.json?.items ?? []).find((x) => (x.content ?? '').includes('纸质说明书'))
    const delB = delTarget ? await api('DELETE', `/api/memory/global/mine/${delTarget.id}`, { token: owner.token }) : { status: 0 }
    check('MM-31 删除单条全局记忆 200', delB.status === 200, `target=${JSON.stringify(delTarget ?? null).slice(0, 100)}`)
    check('MM-32 重复删除 404', delTarget ? (await api('DELETE', `/api/memory/global/mine/${delTarget.id}`, { token: owner.token })).status === 404 : false)

    const clearB = await api('DELETE', '/api/memory/global/mine', { token: owner.token })
    check('MM-33 清空我的全局记忆 200', clearB.status === 200)
    check('MM-34 清空后我的全局为空', ((await globalMine(owner.token)).json?.items ?? []).length === 0)

    // ===== 统一模型锁定与 root 清空全部（应用记忆 + 全局记忆任一存在即锁） =====
    const lockModel = await saveSetting(rootToken, 'MEMORY_EMBEDDING_MODEL', models.publicEmbeddingId2)
    check('MM-35 已有记忆（应用+全局）换统一模型 400', lockModel.status === 400 && lockModel.text.includes('清空'), `${lockModel.status} ${lockModel.text.slice(0, 140)}`)

    check('MM-36 非 root 清空平台全部记忆 403', (await api('DELETE', '/api/memory/all', { token: owner.token })).status === 403)

    check('MM-37 root 清空平台全部记忆 200（应用+全局一并清空）', (await api('DELETE', '/api/memory/all', { token: rootToken })).status === 200)
    check('MM-38 清空后 owner 全局为空', ((await globalMine(owner.token)).json?.items ?? []).length === 0)
    check('MM-38b 清空后应用内记忆为空', ((await appMine(owner.token, APP1)).json?.items ?? []).length === 0)

    check('MM-39 清空后解锁可换统一模型 200', (await saveSetting(rootToken, 'MEMORY_EMBEDDING_MODEL', models.publicEmbeddingId2)).status === 200)

    // ===== 收尾：清空平台全部记忆并关闭开关，保证脚本可重复跑 =====
    await api('DELETE', '/api/memory/all', { token: rootToken })
    await saveSetting(rootToken, 'MEMORY_GLOBAL_ENABLED', 'false')
    await saveSetting(rootToken, 'MEMORY_ENABLED', 'false')
  } finally {
    if (rootToken && models?.channelId) await api('DELETE', `/api/ai/channel/${models.channelId}`, { token: rootToken })
    stub.close()
  }

  console.log(`\n===== memory-management E2E 完成：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
