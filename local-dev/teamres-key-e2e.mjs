// 团队外部接入大改 E2E：moai-ac- 去范围化 + 团队资源 key（teamres_key）「一资源一把 + 功能开关自动生成」
// 场景编号 TR-*（TA-* 已随 access_app.scopes 退役；TR-S12 原口径已随「app 团队级一把 key」退役，编号永久不复用）
// 用法: node local-dev/teamres-key-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5000）
// 覆盖：
//   moai-ac- 去范围化：创建接入不传/传 scopes 均 200（字段退役被忽略）、列表无 scopes 回显、
//     key 直连 wiki 读写 + KG 列表全放行、换 app token 200、用户 token 无需 app_chat；
//   功能开关（key 唯一生成时机）：wiki 未开 MCP / 应用未开 ACP+A2A 时手动建 key 400；
//     PUT mcp-config / PUT feature 开启即自动生成 key 并在 GET 回显；关闭后 GET 只回显开关不回显 key；
//   团队资源 key（moai-tr-，resourceId=资源实例 id 字符串）：get-or-create 幂等、一资源一把（不同应用/不同库 key 不同）、
//     资源归属校验 404、key 直连包围圈（wiki key 仅本库 REST+MCP、跨库 403、越圈 403、团队级 list 过滤）、
//     app key 仅 ACP/A2A（agent 端点 403）、关闭 W1 MCP 后其 key REST+MCP 403 feature_disabled 重开恢复、
//     关闭应用 ACP 后 app key 与 moai-ac- 访问 ACP 均 403 feature_disabled 而 A2A 仍 200、
//     重置后旧 key 401 新 key 200、停用 401 启用恢复、删除 401、资源 key 不可换 token、伪造/旧前缀 key 401、管理端团队门禁。
// 零外部依赖：ACP/A2A 仅到 initialize/agent.json（发布不要求模型配置），KG 列表仅查库不连 Memgraph。
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
const crypto = await import('node:crypto')

let pass = 0
let fail = 0

function check(name, cond, detail = '') {
  if (cond) {
    pass++
    console.log(`PASS | ${name}`)
  } else {
    fail++
    console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`)
  }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
const sha256Hex = (content) => crypto.createHash('sha256').update(content).digest('hex')

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
async function retryApi(method, path, body, token, tries = 4) {
  let last = null
  for (let i = 0; i < tries; i++) {
    const r = await api(method, path, { token, body })
    if (r.status !== 500) return r
    last = r
    await new Promise((resolve) => setTimeout(resolve, 1500 * (i + 1)))
  }
  return last
}

const TS = Date.now().toString().slice(-8)
const xw = (wikiId, suffix = '') => `/api/teamres/wiki/${wikiId}${suffix}`
const TEAM_WIKI_LIST = '/api/teamres/wiki/list'
const TEAM_KG_LIST = '/api/teamres/knowledge-graph/list'
const keyHeaders = (key) => ({ 'Authorization': `Bearer ${key}`, 'Content-Type': 'application/json' })
const keyHeadersX = (key) => ({ 'x-api-key': key, 'Content-Type': 'application/json' })

// ===== 本地 OpenAI 兼容桩（仅用于 agent-config 绑模型，ACP/A2A 断言只到 initialize/agent.json）=====
function startStubServer() {
  const stub = http.createServer((req, res) => {
    res.on('error', () => {})
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
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: '收到：' + String(payload.messages?.at(-1)?.content ?? '').slice(0, 40) }, finish_reason: 'stop' }],
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status}`)
  const token = login.json.accessToken

  // ===== 准备：团队 + 三个知识库（W1/W2B 开 MCP、W2 保持关闭）+ 两个已发布 Agent 应用（A 开功能、A2 暂不开）=====
  const T = Number((await retryApi('POST', '/api/team', { name: 'tr-team-' + TS }, token)).json?.value)
  if (!Number.isFinite(T)) throw new Error('创建团队失败')
  const mkWiki = async (n) => {
    const r = await retryApi('POST', '/api/wiki', { teamId: T, name: `tr_wiki${n}_${TS}`, description: 'teamres key e2e' }, token)
    if (r.status !== 200) throw new Error(`建知识库失败: ${r.status} ${r.text.slice(0, 120)}`)
    return Number(r.json?.value)
  }
  const W1 = await mkWiki(1)
  const W2 = await mkWiki(2)
  const W2B = await mkWiki('2b')

  // 桩渠道 + conversation 模型（仅 agent-config 需要；发布本身不要求）
  const stub = await startStubServer()
  const stubPort = stub.address().port
  const chName = `tr-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'tr-stub-key', enabled: true, description: 'teamres key E2E 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const modelName = `tr-stub-chat-${TS}`
  await api('POST', '/api/ai/model', { token, body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'teamres key E2E 桩模型' }, enabled: true, isPublic: true } })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token })
  const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')

  const mkApp = async (n, external) => {
    const r = await retryApi('POST', '/api/app', { teamId: T, name: `tr_agent_${n}_${TS}`, appType: 'agent', isExternal: external, isAuth: false }, token)
    const appId = String(r.json?.value ?? '')
    await api('PUT', `/api/app/${appId}/agent-config`, { token, body: { modelId: MODEL_ID, prompt: '你是回声助手', wikiIds: [], plugins: [] } })
    const p = await retryApi('POST', `/api/app/${appId}/publish`, {}, token)
    if (p.status !== 200) throw new Error(`发布应用 ${n} 失败: ${p.status} ${p.text.slice(0, 120)}`)
    return appId
  }
  const A = await mkApp(1, true) // 主应用：后续开启 ACP/A2A（TR-S12）
  const A2 = await mkApp(2, false) // 第二个应用：先保持功能关闭（TR-S33 反例），TR-S39 再开启

  const createKey = async (body) => api('POST', '/api/team-resource-key', { token, body })

  // ===== TR-S1~S3 moai-ac- 去范围化 =====
  const acc = await retryApi('POST', '/api/access-app', { teamId: T, name: 'tr接入_' + TS, description: 'teamres key e2e' }, token)
  if (acc.status !== 200) throw new Error(`创建应用接入失败: ${acc.status} ${acc.text.slice(0, 120)}`)
  const ACCESS_KEY = acc.json?.key
  const ACCESS_SECRET = acc.json?.secret
  check('TR-S1 创建接入不传 scopes 200', acc.status === 200 && typeof ACCESS_KEY === 'string' && ACCESS_KEY.startsWith('moai-ac-'), `${acc.status}`)

  const accScoped = await retryApi('POST', '/api/access-app', { teamId: T, name: 'tr接入带scopes_' + TS, description: 'scopes 退役', scopes: ['wiki_read', 'hacker'] }, token)
  check('TR-S2 传 scopes 字段被忽略（退役）创建 200', accScoped.status === 200, `${accScoped.status} ${accScoped.text.slice(0, 120)}`)

  const listAcc = await api('GET', `/api/access-app/list?teamId=${T}`, { token })
  const item = (listAcc.json?.items ?? []).find((x) => x.accessAppId === acc.json?.accessAppId)
  check('TR-S3 接入列表不再回显 scopes', listAcc.status === 200 && item != null && !('scopes' in (item ?? {})), `keys=${Object.keys(item ?? {}).join(',')}`)

  // ===== TR-S4~S7 moai-ac- key 直连全量访问 =====
  const dRead = await api('POST', xw(W1, '/documents/list'), { headers: undefined, token: ACCESS_KEY, body: { pageNo: 1, pageSize: 10 } })
  const dWrite = await api('POST', xw(W1, '/documents/preupload'), { token: ACCESS_KEY, body: { fileName: `tr-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`tr-${TS}`) } })
  check('TR-S4 moai-ac- key 直连 wiki 读+写均放行', dRead.status === 200 && dWrite.status === 200, `read=${dRead.status} write=${dWrite.status}`)

  const dReadX2 = await fetch(BASE + xw(W1, '/documents/list'), { method: 'POST', headers: keyHeadersX(ACCESS_KEY), body: JSON.stringify({ pageNo: 1, pageSize: 10 }) })
  check('TR-S5 x-api-key 头直连放行', dReadX2.status === 200, `${dReadX2.status}`)

  const kgList = await api('POST', TEAM_KG_LIST, { token: ACCESS_KEY, body: {} })
  check('TR-S6 moai-ac- key 直连 KG list 放行', kgList.status === 200 && Array.isArray(kgList.json?.items), `${kgList.status} ${kgList.text.slice(0, 120)}`)

  const wikiList = await api('POST', TEAM_WIKI_LIST, { token: ACCESS_KEY, body: {} })
  check('TR-S7 moai-ac- key 直连 wiki 团队级 list 放行且含库', wikiList.status === 200 && (wikiList.json?.items ?? []).some((x) => Number(x.wikiId) === W1) && (wikiList.json?.items ?? []).some((x) => Number(x.wikiId) === W2), `${wikiList.status}`)

  // ===== TR-S8~S9 token 兑换（app token；用户 token 随外部应用授权退役，TR-S9 语义改写编号不复用）=====
  const appTok = await api('POST', '/api/teamres/token', { body: { accessAppKey: ACCESS_KEY, secret: ACCESS_SECRET } })
  check('TR-S8 换 app token 200', appTok.status === 200 && appTok.json?.tokenType === 'app', `${appTok.status}`)

  const retireUserTok = await api('POST', '/api/teamres/token', { body: { accessAppKey: ACCESS_KEY, secret: ACCESS_SECRET, appId: A, externalUserId: `tr-user-${TS}` } })
  check('TR-S9 key+externalUserId 换用户 token 400（外部应用授权已退役，身份穿透改走 /api/subsys/system/app-tokens）', retireUserTok.status === 400 && retireUserTok.text.includes('/api/subsys/system/app-tokens'), `${retireUserTok.status} ${retireUserTok.text.slice(0, 140)}`)

  // ===== TR-S32~S33 未开启对外功能时手动建 key 400（key 唯一生成时机 = 功能开关）=====
  const kW2Fail = await createKey({ teamId: T, resourceType: 'wiki', resourceId: String(W2) })
  check('TR-S32 wiki 未开 MCP 手动建 key 400', kW2Fail.status === 400 && String(kW2Fail.json?.detail ?? '').includes('未开启'), `${kW2Fail.status} ${kW2Fail.text.slice(0, 140)}`)

  const kAppFail = await createKey({ teamId: T, resourceType: 'app', resourceId: A2 })
  check('TR-S33 应用未开 ACP/A2A 手动建 key 400', kAppFail.status === 400 && String(kAppFail.json?.detail ?? '').includes('未开启'), `${kAppFail.status} ${kAppFail.text.slice(0, 140)}`)

  // ===== TR-S34 功能开关自动生成：开启 W1 MCP 即生成 key 并在 GET 回显 =====
  const mcpOn = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: true } })
  const mcpOnGet = await api('GET', `/api/wiki/${W1}/mcp-config`, { token })
  check('TR-S34 开启 W1 MCP 200 且自动生成 key、GET mcp-config 回显 key/status',
    mcpOn.status === 200 && mcpOn.json?.isMcpEnabled === true && String(mcpOn.json?.key ?? '').startsWith('moai-tr-')
    && mcpOnGet.status === 200 && mcpOnGet.json?.isMcpEnabled === true && mcpOnGet.json?.key === mcpOn.json?.key,
    `${mcpOn.status} ${mcpOn.text.slice(0, 200)} get=${mcpOnGet.text.slice(0, 200)}`)
  const W1_AUTO_ROW = String(mcpOn.json?.teamResourceKeyId ?? '')

  // ===== TR-S10~S11 手动建 key（get-or-create / 幂等；key 不支持删除，W1 沿用开启时自动生成的行）=====
  const kW1a = await createKey({ teamId: T, resourceType: 'wiki', resourceId: String(W1) })
  check('TR-S10 手动建 key 复用开启时自动生成的行（created=false 同 key）、resourceId=W1（字符串）',
    kW1a.status === 200 && kW1a.json?.created === false && kW1a.json?.key === mcpOn.json?.key && String(kW1a.json?.resourceId) === String(W1), `${kW1a.status} ${kW1a.text.slice(0, 160)}`)
  const kW1b = await createKey({ teamId: T, resourceType: 'wiki', resourceId: String(W1) })
  check('TR-S11 get-or-create 幂等：同资源再生成 created=false 同 key',
    kW1b.status === 200 && kW1b.json?.created === false && kW1b.json?.key === kW1a.json?.key && kW1b.json?.teamResourceKeyId === kW1a.json?.teamResourceKeyId, `${kW1b.text.slice(0, 160)}`)

  // 第二把开启的库 W2B：一资源一把（不同 wiki key 不同）
  const mcpOnB = await api('PUT', `/api/wiki/${W2B}/mcp-config`, { token, body: { isMcpEnabled: true } })
  const kW2B = await createKey({ teamId: T, resourceType: 'wiki', resourceId: String(W2B) })
  check('TR-S35 不同 wiki 的 key 各不相同（W1 ≠ W2B）且 W2B 建为复用 created=false',
    mcpOnB.status === 200 && kW2B.status === 200 && kW2B.json?.created === false && kW2B.json?.key === mcpOnB.json?.key && kW2B.json?.key !== kW1a.json?.key, `${mcpOnB.status}/${kW2B.status} ${kW2B.text.slice(0, 160)}`)

  // ===== TR-S12~S13 应用功能开关与 app key（resourceId=应用 Guid，一应用一把，ACP/A2A 共用）=====
  const featOn = await api('PUT', `/api/app/${A}/feature`, { token, body: { isAcpA2aEnabled: true } })
  const featGet = await api('GET', `/api/app/${A}/feature`, { token })
  const kApp = await createKey({ teamId: T, resourceType: 'app', resourceId: A })
  check('TR-S12 应用开启 ACP/A2A 自动生成 key，GET feature 与 POST 建 key 回显 resourceId=A',
    featOn.status === 200 && featOn.json?.isAcpA2aEnabled === true && String(featOn.json?.key ?? '').startsWith('moai-tr-')
    && featGet.status === 200 && featGet.json?.isAcpA2aEnabled === true && featGet.json?.key === featOn.json?.key
    && kApp.status === 200 && String(kApp.json?.resourceId) === A && kApp.json?.created === false && kApp.json?.key === featOn.json?.key,
    `put=${featOn.status} get=${featGet.status} key=${kApp.status} ${kApp.text.slice(0, 160)}`)
  const kApp2 = await createKey({ teamId: T, resourceType: 'app', resourceId: A })
  check('TR-S13 app key 幂等（一应用一把，ACP/A2A 共用）', kApp2.status === 200 && kApp2.json?.created === false && kApp2.json?.teamResourceKeyId === kApp.json?.teamResourceKeyId, `${kApp2.text.slice(0, 160)}`)

  const kBad = await createKey({ teamId: T, resourceType: 'wiki', resourceId: '999999999' })
  check('TR-S14 资源不存在 404', kBad.status === 404, `${kBad.status}`)

  const kList = await api('GET', `/api/team-resource-key/list?teamId=${T}`, { token })
  const kListItems = kList.json?.items ?? []
  check('TR-S15 资源 key 列表回显三把（w1/w2b/app）含 key 明文且 resourceId 字符串匹配',
    kList.status === 200 && kListItems.length >= 3
    && kListItems.some((x) => x.resourceType === 'wiki' && String(x.resourceId) === String(W1))
    && kListItems.some((x) => x.resourceType === 'wiki' && String(x.resourceId) === String(W2B))
    && kListItems.some((x) => x.resourceType === 'app' && String(x.resourceId) === A),
    `count=${kListItems.length}`)

  const KEY_W1 = String(kW1a.json?.key ?? '')
  const KEY_W2B = String(kW2B.json?.key ?? '')
  const KEY_APP = String(kApp.json?.key ?? '')

  // ===== TR-S16~S20 wiki 资源 key 包围圈 =====
  const w1Read = await api('GET', xw(W1), { token: KEY_W1 })
  const w1Write = await api('POST', xw(W1, '/documents/preupload'), { token: KEY_W1, body: { fileName: `tr-w1-${TS}.md`, contentType: 'text/markdown', fileSize: 10, sha256: sha256Hex(`tr-w1-${TS}`) } })
  check('TR-S16 wiki key 直连本库 REST 读+写放行（功能已开启）', w1Read.status === 200 && w1Write.status === 200, `read=${w1Read.status} write=${w1Write.status}`)

  const w1Cross = await api('POST', xw(W2B, '/documents/list'), { token: KEY_W1, body: { pageNo: 1, pageSize: 10 } })
  check('TR-S17 wiki key 访问其它库 403 insufficient_scope',
    w1Cross.status === 403 && w1Cross.json?.error?.code === 'insufficient_scope', `${w1Cross.status} ${w1Cross.text.slice(0, 140)}`)

  const w1List = await api('POST', TEAM_WIKI_LIST, { token: KEY_W1, body: {} })
  check('TR-S18 wiki key 团队级 list 只见绑定库',
    w1List.status === 200 && (w1List.json?.items ?? []).some((x) => Number(x.wikiId) === W1)
    && !(w1List.json?.items ?? []).some((x) => Number(x.wikiId) === W2B) && !(w1List.json?.items ?? []).some((x) => Number(x.wikiId) === W2), `${w1List.status}`)

  const w1Out = await api('POST', TEAM_KG_LIST, { token: KEY_W1, body: {} })
  const w1Agent = await api('GET', `/api/teamres/agent/${A}/session/list`, { token: KEY_W1 })
  check('TR-S19 wiki key 越圈（KG/agent 端点）403', w1Out.status === 403 && w1Agent.status === 403, `kg=${w1Out.status} agent=${w1Agent.status}`)

  // streamable HTTP MCP：SDK 以 SSE 帧 data: 包裹 JSON-RPC，须带 Accept 双类型并从 data: 行取结果
  const mcpCall = async (path, key, payload) => {
    const res = await fetch(`${BASE}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', ...keyHeaders(key) },
      body: JSON.stringify(payload),
    })
    const text = await res.text()
    let json = null
    const dataLine = text.split('\n').find((l) => l.startsWith('data:'))
    try { json = JSON.parse(dataLine ? dataLine.slice(5).trim() : text) } catch { /* 非 JSON */ }
    return { status: res.status, json }
  }
  const mcpInit = await mcpCall(xw(W1, '/mcp'), KEY_W1, { jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'tr-e2e', version: '0' } } })
  const mcpCross = await mcpCall(xw(W2B, '/mcp'), KEY_W1, { jsonrpc: '2.0', id: 2, method: 'initialize', params: {} })
  check('TR-S20 wiki key 本库 MCP initialize 200、它库 MCP 403',
    mcpInit.status === 200 && Boolean(mcpInit.json?.result?.serverInfo?.name) && mcpCross.status === 403, `init=${mcpInit.status} cross=${mcpCross.status}`)

  // ===== TR-S21~S23 app 资源 key（ACP/A2A 专用）=====
  const acpInit = await fetch(`${BASE}/api/teamres/app/${A}/acp`, {
    method: 'POST', headers: keyHeadersX(KEY_APP), body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1, clientCapabilities: {} } }),
  })
  const acpBody = await acpInit.json().catch(() => null)
  check('TR-S21 app key ACP initialize 200', acpInit.status === 200 && acpBody?.result?.protocolVersion === 1, `status=${acpInit.status} ${JSON.stringify(acpBody).slice(0, 140)}`)

  const a2aCard = await fetch(`${BASE}/api/teamres/app/${A}/a2a/agent.json`, { headers: keyHeaders(KEY_APP) })
  check('TR-S22 app key A2A agent.json 200', a2aCard.status === 200, `${a2aCard.status}`)

  const acpOut = await api('POST', xw(W1, '/documents/list'), { token: KEY_APP, body: { pageNo: 1, pageSize: 10 } })
  const acpAgent = await api('GET', `/api/teamres/agent/${A}/session/list`, { token: KEY_APP })
  check('TR-S23 app key 越圈（wiki/agent 端点）403', acpOut.status === 403 && acpAgent.status === 403, `wiki=${acpOut.status} agent=${acpAgent.status}`)

  // ===== TR-S36 moai-ac- 直连 ACP（应用已开启功能后放行）=====
  const acpFull = await fetch(`${BASE}/api/teamres/app/${A}/acp`, {
    method: 'POST', headers: keyHeadersX(ACCESS_KEY), body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1, clientCapabilities: {} } }),
  })
  const acpFullBody = await acpFull.json().catch(() => null)
  check('TR-S36 moai-ac- key 直连已开启应用的 ACP initialize 200', acpFull.status === 200 && acpFullBody?.result?.protocolVersion === 1, `status=${acpFull.status} ${JSON.stringify(acpFullBody).slice(0, 140)}`)

  // ===== TR-S24~S28 重置 / 停用 / 删除 / 不可换 token =====
  const reset1 = await api('POST', `/api/team-resource-key/${kW1a.json?.teamResourceKeyId}/reset`, { token })
  check('TR-S24 重置返回新 key 且行 id 不变',
    reset1.status === 200 && reset1.json?.teamResourceKeyId === kW1a.json?.teamResourceKeyId && reset1.json?.key !== KEY_W1, `${reset1.status}`)
  const KEY_W1_NEW = String(reset1.json?.key ?? '')

  const oldKeyAfter = await api('GET', xw(W1), { token: KEY_W1 })
  const newKeyOk = await api('GET', xw(W1), { token: KEY_W1_NEW })
  check('TR-S25 重置后旧 key 401、新 key 200', oldKeyAfter.status === 401 && newKeyOk.status === 200, `old=${oldKeyAfter.status} new=${newKeyOk.status}`)

  // TR-S26（key 停用）与 TR-S28（key 删除）已随「key 无启停状态、不可删除」退役，编号永久不复用：
  // 吊销手段=重置（TR-S25）或关闭资源功能开关（TR-S37/S38）
  const tokByResKey = await api('POST', '/api/teamres/token', { body: { accessAppKey: KEY_W1_NEW } })
  check('TR-S27 资源 key 不可换 token（moai-ac- 专属）', tokByResKey.status === 400 || tokByResKey.status === 401, `${tokByResKey.status}`)

  // ===== TR-S37 关闭 W1 MCP：其资源 key REST+MCP 一律 403 feature_disabled，重开恢复 =====
  const mcpOff = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: false } })
  const mcpOffGet = await api('GET', `/api/wiki/${W1}/mcp-config`, { token })
  const restFeatOff = await api('POST', xw(W1, '/documents/list'), { token: KEY_W1_NEW, body: { pageNo: 1, pageSize: 10 } })
  const mcpFeatOff = await mcpCall(xw(W1, '/mcp'), KEY_W1_NEW, { jsonrpc: '2.0', id: 3, method: 'initialize', params: {} })
  check('TR-S37a 关闭 W1 MCP 后 GET 只回显开关、其资源 key REST+MCP 均 403 feature_disabled',
    mcpOff.status === 200 && mcpOff.json?.isMcpEnabled === false && mcpOff.json?.key === undefined
    && mcpOffGet.status === 200 && mcpOffGet.json?.isMcpEnabled === false && mcpOffGet.json?.key === undefined
    && restFeatOff.status === 403 && restFeatOff.json?.error?.code === 'feature_disabled'
    && mcpFeatOff.status === 403 && mcpFeatOff.json?.error?.code === 'feature_disabled',
    `put=${mcpOff.status} rest=${restFeatOff.status}/${restFeatOff.json?.error?.code} mcp=${mcpFeatOff.status}/${mcpFeatOff.json?.error?.code}`)
  const mcpReOn = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: true } })
  const restReOn = await api('POST', xw(W1, '/documents/list'), { token: KEY_W1_NEW, body: { pageNo: 1, pageSize: 10 } })
  check('TR-S37b 重新开启 MCP 后 W1 key REST 恢复 200 且 key 复用不变',
    mcpReOn.status === 200 && mcpReOn.json?.key === KEY_W1_NEW && restReOn.status === 200, `put=${mcpReOn.status} rest=${restReOn.status}`)

  // ===== TR-S38 关闭应用协议接入（ACP/A2A 单开关整体关闭）：app key 与 moai-ac- 打两类端点均 403 feature_disabled；重开恢复且 key 复用 =====
  const featOff = await api('PUT', `/api/app/${A}/feature`, { token, body: { isAcpA2aEnabled: false } })
  const acpKeyOff = await fetch(`${BASE}/api/teamres/app/${A}/acp`, {
    method: 'POST', headers: keyHeadersX(KEY_APP), body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }),
  })
  const acpKeyOffBody = await acpKeyOff.json().catch(() => null)
  const a2aAcOff = await fetch(`${BASE}/api/teamres/app/${A}/a2a/agent.json`, { headers: keyHeadersX(ACCESS_KEY) })
  const a2aKeyOff = await fetch(`${BASE}/api/teamres/app/${A}/a2a/agent.json`, { headers: keyHeadersX(KEY_APP) })
  check('TR-S38a 关闭协议接入后 app key 与 moai-ac- 打 ACP/A2A 均 403 feature_disabled',
    featOff.status === 200 && featOff.json?.isAcpA2aEnabled === false && featOff.json?.key === undefined
    && acpKeyOff.status === 403 && acpKeyOffBody?.error?.code === 'feature_disabled'
    && a2aAcOff.status === 403 && a2aKeyOff.status === 403,
    `put=${featOff.status} keyAcp=${acpKeyOff.status}/${acpKeyOffBody?.error?.code} acA2a=${a2aAcOff.status} keyA2a=${a2aKeyOff.status}`)
  const featReOn = await api('PUT', `/api/app/${A}/feature`, { token, body: { isAcpA2aEnabled: true } })
  const acpKeyReOn = await fetch(`${BASE}/api/teamres/app/${A}/acp`, {
    method: 'POST', headers: keyHeadersX(KEY_APP), body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }),
  })
  const a2aKeyReOn = await fetch(`${BASE}/api/teamres/app/${A}/a2a/agent.json`, { headers: keyHeadersX(KEY_APP) })
  check('TR-S38b 重新开启后 ACP/A2A 恢复 200 且 key 复用不变',
    featReOn.status === 200 && featReOn.json?.key === KEY_APP && acpKeyReOn.status === 200 && a2aKeyReOn.status === 200, `put=${featReOn.status} acp=${acpKeyReOn.status} a2a=${a2aKeyReOn.status}`)

  // ===== TR-S39 每应用一把：开启第二个应用的协议接入 → 其 key 与 A 的 key 不同且仅作用于自己 =====
  const featA2 = await api('PUT', `/api/app/${A2}/feature`, { token, body: { isAcpA2aEnabled: true } })
  const kAppA2 = await createKey({ teamId: T, resourceType: 'app', resourceId: A2 })
  const acpA2Self = await fetch(`${BASE}/api/teamres/app/${A2}/acp`, {
    method: 'POST', headers: keyHeadersX(String(kAppA2.json?.key ?? '')), body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }),
  })
  check('TR-S39 第二个已发布应用开启 ACP 后生成独立 key（≠A 的 key）且可打自己 ACP 200',
    featA2.status === 200 && kAppA2.status === 200 && String(kAppA2.json?.resourceId) === A2
    && String(kAppA2.json?.key ?? '').startsWith('moai-tr-') && kAppA2.json?.key !== KEY_APP && acpA2Self.status === 200,
    `put=${featA2.status} key=${kAppA2.status} self=${acpA2Self.status}`)

  // ===== TR-S29~S31 伪造 / 门禁 =====
  const forgedTr = await api('GET', xw(W1), { token: 'moai-tr-forgedforgedforgedforgedforgedforged' })
  const forgedAc = await api('GET', xw(W1), { token: 'moai-ac-forgedforgedforgedforgedforgedforged' })
  const legacyMoai = await api('GET', xw(W1), { token: 'moai-legacylegacylegacylegacylegacylegacy' })
  check('TR-S29 伪造 moai-tr-/moai-ac- 与旧 moai- 前缀均 401', forgedTr.status === 401 && forgedAc.status === 401 && legacyMoai.status === 401, `tr=${forgedTr.status} ac=${forgedAc.status} legacy=${legacyMoai.status}`)

  const otherTeam = Number((await retryApi('POST', '/api/team', { name: 'tr-team-other-' + TS }, token)).json?.value)
  const otherAcc = await retryApi('POST', '/api/access-app', { teamId: otherTeam, name: 'tr其他团队接入_' + TS }, token)
  const otherKeyHit = await api('GET', xw(W1), { token: String(otherAcc.json?.key ?? '') })
  check('TR-S30 其它团队 moai-ac- key 访问本团队知识库 404', otherKeyHit.status === 404, `${otherKeyHit.status}`)

  const kOther = await createKey({ teamId: otherTeam, resourceType: 'wiki', resourceId: String(W1) })
  check('TR-S31 其它团队对本团队资源生成 key 404', kOther.status === 404, `${kOther.status}`)

  console.log(`\n===== TR 结果: pass=${pass} fail=${fail} =====`)
  process.exit(fail > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 执行失败:', e)
  process.exit(1)
})
