// 知识图谱 MCP 服务器 E2E（场景 @KGM-S1 ~ @KGM-S7；后端 127.0.0.1:5000，可用参数覆盖）
// 端点：/api/external/knowledge-graph/{kgId}/mcp（MCP streamable HTTP，无状态模式，免 Mcp-Session-Id）
// 覆盖：鉴权门禁（无凭证 401 / 伪造 key 401 / 无 kg_mcp 范围 403 / kgId 不存在与跨团队 404）
//       → tools/list 恰好四个只读工具（域隔离：KG 端点无 wiki 工具、wiki 端点无 KG 工具，跨域工具调用被拒）
//       → list_knowledge_graphs（团队托管图谱列表）
//       → get_knowledge_graph_schema（实体类型属性定义 + 关系类型起止约束）
//       → search_knowledge_graph_nodes（默认路径 kgId / 关键字 / 分页 / 类型过滤 / 跨团队拒绝）
//       → search_knowledge_graph_recall（向量召回 / 邻居关系 / 阈值 / top / 未配向量化 skippedHint / 参数校验）
//       → 协议行为（未知工具 -32602 / GET 405 / 无 initialize 直接调用 / 通知 202 / 外部 REST 回归）。
// 模型策略：与 kg-search-e2e 一致——本地 OpenAI 兼容桩（确定性 3-gram 哈希向量）自举渠道与模型，
//       桩不可用时不影响鉴权/schema/节点搜索/协议场景，仅召回正向用例跳过。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, reason = '') => {
  SKIP++; console.log(`SKIP | ${name} ${reason ? '— ' + reason : ''}`)
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
async function api(method, path, { token, body } = {}) {
  const h = {}
  if (body !== undefined) h['Content-Type'] = 'application/json'
  if (token) h['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body) })
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

// ===== MCP JSON-RPC 客户端（无状态：不带 Mcp-Session-Id） =====
let idSeq = 0
function mcpHeaders(key, mode = 'bearer') {
  if (!key) return {}
  return mode === 'x-api-key' ? { 'x-api-key': key } : { Authorization: `Bearer ${key}` }
}
async function mcp(mcpPath, payload, key, mode = 'bearer') {
  const res = await fetch(BASE + mcpPath, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', ...mcpHeaders(key, mode) },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  })
  const text = await res.text()
  let json = null
  const dataLine = text.split('\n').find((l) => l.startsWith('data:'))
  try { json = JSON.parse(dataLine ? dataLine.slice(5).trim() : text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}
const rpc = (method, params) => ({ jsonrpc: '2.0', id: ++idSeq, method, params })
const notify = (method, params) => ({ jsonrpc: '2.0', method, params })
function toolPayload(res) {
  const text = res.json?.result?.content?.find((c) => c.type === 'text')?.text ?? ''
  try { return { isError: res.json?.result?.isError === true, text, data: JSON.parse(text) } } catch { return { isError: res.json?.result?.isError === true, text, data: null } }
}
const initHandshake = (mcpPath, key, mode) => mcp(mcpPath, rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'kg-mcp-e2e', version: '0' } }), key, mode)

const TS = Date.now().toString().slice(-8)
const kg = (id) => `/api/knowledge-graph/${id}`
const mcpOf = (kgId) => `/api/external/knowledge-graph/${kgId}/mcp`

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
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: 'stub' }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

// 确定性哈希向量：同词文本向量相近（按 3-gram 哈希落桶）
function stubEmbed(text, dims) {
  const vec = new Array(dims).fill(0)
  const s = String(text)
  for (let i = 0; i < s.length; i++) {
    const g = s.slice(i, i + 3)
    if (g.length < 2) continue
    const h = crypto.createHash('md5').update(g).digest().readUInt32LE(0)
    vec[h % dims] += 1
  }
  const norm = Math.sqrt(vec.reduce((acc, v) => acc + v * v, 0)) || 1
  return vec.map((v) => v / norm)
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status}`)
  const admin = login.json.accessToken

  const cap = await api('GET', `/api/knowledge-graph/list?teamId=1`, { token: admin })
  if (cap.status !== 200 || cap.json?.enabled !== true) {
    console.warn(`SKIP | 知识图谱 MCP E2E 未执行: KG 能力未开启（list.enabled=${cap.json?.enabled}）`)
    process.exit(0)
  }

  // ===== 准备：团队 T1 + 托管图 G1（绑定向量化）/G2（不绑定）；T2 + G3（跨团队 404）；接入 key / 团队 key =====
  const T1 = Number((await retryApi('POST', '/api/team', { name: 'kgm-team1-' + TS }, admin)).json?.value)
  const T2 = Number((await retryApi('POST', '/api/team', { name: 'kgm-team2-' + TS }, admin)).json?.value)
  if (!Number.isFinite(T1) || !Number.isFinite(T2)) throw new Error('创建团队失败')

  const g1Res = await retryApi('POST', '/api/knowledge-graph', { teamId: T1, name: 'kgm-main-' + TS, templateKey: 'blank' }, admin)
  const g2Res = await retryApi('POST', '/api/knowledge-graph', { teamId: T1, name: 'kgm-noemb-' + TS, templateKey: 'blank' }, admin)
  const g3Res = await retryApi('POST', '/api/knowledge-graph', { teamId: T2, name: 'kgm-other-' + TS, templateKey: 'blank' }, admin)
  const G1 = Number(g1Res.json?.value)
  const G2 = Number(g2Res.json?.value)
  const G3 = Number(g3Res.json?.value)
  if (!Number.isFinite(G1) || !Number.isFinite(G2) || !Number.isFinite(G3)) throw new Error(`建图失败: ${g1Res.status}/${g2Res.status}/${g3Res.status}`)

  const acc = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'kgm接入-' + TS, description: 'kg mcp e2e', scopes: ['kg_read', 'kg_mcp'] }, admin)
  const ACCESS_KEY = acc.json?.key
  if (!ACCESS_KEY) throw new Error(`创建应用接入失败: ${acc.status} ${acc.text.slice(0, 120)}`)

  // 团队接入 key（moai-）已下线：MCP 场景改用应用接入 key（moai-ac-，命名带 kgm接入- 前缀便于清理）
  const kMcp = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'kgm接入-MCP-' + TS, description: 'kg mcp e2e', scopes: ['kg_mcp'] }, admin)
  const TEAM_KEY = kMcp.json?.key
  const kRead = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'kgm接入-READ-' + TS, description: 'kg mcp e2e', scopes: ['kg_read'] }, admin)
  const TEAM_KEY_READ = kRead.json?.key
  const kWiki = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'kgm接入-WIKI-' + TS, description: 'kg mcp e2e', scopes: ['wiki_mcp'] }, admin)
  const TEAM_KEY_WIKI_MCP = kWiki.json?.key
  if (!TEAM_KEY || !TEAM_KEY_READ || !TEAM_KEY_WIKI_MCP) throw new Error(`创建应用接入 key 失败: ${kMcp.status}/${kRead.status}/${kWiki.status}`)

  // schema/节点/边准备（向量化随节点写入自动触发）
  const portType = 'MCP港口-' + TS
  const legType = 'MCP航段-' + TS
  const teP = await retryApi('POST', `${kg(G1)}/entity-types`, { name: portType, description: '港口地点', properties: [{ name: '港口代码', type: 'string', required: true, description: 'UN/LOC 代码' }] }, admin)
  const teS = await retryApi('POST', `${kg(G1)}/entity-types`, { name: legType, description: '运输航段', properties: [] }, admin)
  const PORT = Number(teP.json?.value)
  const LEG = Number(teS.json?.value)
  const trD = await retryApi('POST', `${kg(G1)}/relation-types`, { name: 'MCP出发-' + TS, sourceTypeId: PORT, targetTypeId: LEG }, admin)
  const DEP = Number(trD.json?.value)
  if (!PORT || !LEG || !DEP) throw new Error(`建类型失败: ${teP.status}/${teS.status}/${trD.status}`)

  const namePort = '上海港-' + TS
  const nameLeg = '上海到宁波航段-' + TS
  const nP = await retryApi('POST', `${kg(G1)}/nodes`, { entityTypeId: PORT, name: namePort, description: '华东集装箱大港', properties: { 港口代码: 'CNSHA' } }, admin)
  const nL = await retryApi('POST', `${kg(G1)}/nodes`, { entityTypeId: LEG, name: nameLeg, description: '短程沿海航段' }, admin)
  const idPort = String(nP.json?.value)
  const idLeg = String(nL.json?.value)
  const eD = await retryApi('POST', `${kg(G1)}/edges`, { relationTypeId: DEP, sourceNodeId: idPort, targetNodeId: idLeg }, admin)
  if (!idPort || !idLeg || eD.status !== 200) throw new Error(`建节点/边失败: ${nP.status}/${nL.status}/${eD.status}`)

  // 跨团队图谱类型与节点（工具跨团队 kgId 参数拒绝用例）
  await retryApi('POST', `${kg(G3)}/entity-types`, { name: '别家类型-' + TS }, admin)

  // wiki MCP 端点（域隔离反向断言用，建一个空知识库即可）
  const wRes = await retryApi('POST', '/api/wiki', { teamId: T1, name: 'kgm-wiki-' + TS, description: 'kg mcp e2e 域隔离' }, admin)
  const W1 = Number(wRes.json?.value)

  // ===== 向量准备：桩渠道 + 模型授权 + G1 embedding 配置 =====
  let hasEmb = false
  try {
    const stub = await startStubServer()
    const stubPort = stub.address().port
    const chName = `kgm-stub渠道-${TS}`
    const crc = await api('POST', '/api/ai/channel', {
      token: admin,
      body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'kgm-stub-key', enabled: true, description: 'kg-mcp E2E 桩渠道' },
    })
    const channels = await api('GET', '/api/ai/channel', { token: admin })
    const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
    const embName = `kgm-stub-embed-${TS}`
    const mrc = CH_ID ? await api('POST', '/api/ai/model', {
      token: admin,
      body: { channelId: CH_ID, meta: { modelId: embName, name: embName, modelKind: 'embedding', description: 'kg-mcp E2E 桩模型' }, enabled: true, isPublic: false },
    }) : { status: 0 }
    const models = CH_ID ? await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: admin }) : { json: {} }
    const EMB_ID = String((models.json?.items ?? []).find((m) => m.name === embName)?.id ?? '')
    if (EMB_ID) {
      const cur = await api('GET', `/api/ai/model/${EMB_ID}/authorization`, { token: admin })
      const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), T1])]
      await api('PUT', `/api/ai/model/${EMB_ID}/authorization`, { token: admin, body: { modelId: EMB_ID, teamIds } })
    }
    hasEmb = crc.status === 200 && CH_ID !== '' && EMB_ID !== ''
    if (hasEmb) {
      const bind = await api('PUT', `${kg(G1)}/embedding-config`, { token: admin, body: { embeddingModelId: EMB_ID, embeddingDimensions: 1024 } })
      hasEmb = bind.status === 200
      if (!hasEmb) console.log(`INFO | embedding 配置绑定失败 ${bind.status}，召回正向用例跳过`)
    } else {
      console.log(`INFO | 桩模型链路不可用 ch=${crc.status}/${CH_ID} model=${mrc.status}/${EMB_ID}，召回正向用例跳过`)
    }
  } catch (err) {
    console.log(`INFO | 向量准备异常：${err.message}，召回正向用例跳过`)
    hasEmb = false
  }

  try {
    // ===== @KGM-S1 鉴权门禁 =====
    const noAuth = await mcp(mcpOf(G1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), null)
    check('KGM-S1a 无凭证 401', noAuth.status === 401, `${noAuth.status} ${noAuth.text.slice(0, 120)}`)

    const fakeKey = await mcp(mcpOf(G1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), 'moai-not-a-real-key')
    check('KGM-S1b 伪造 key 401', fakeKey.status === 401, `${fakeKey.status}`)

    const initApp = await initHandshake(mcpOf(G1), ACCESS_KEY)
    check('KGM-S1c 应用接入 key initialize 200', initApp.status === 200 && initApp.json?.result?.serverInfo?.name, `${initApp.status} ${initApp.text.slice(0, 160)}`)

    const initTeamKey = await mcp(mcpOf(G1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY, 'x-api-key')
    check('KGM-S1d 团队接入 key（x-api-key）initialize 200', initTeamKey.status === 200 && Boolean(initTeamKey.json?.result?.serverInfo?.name), `${initTeamKey.status}`)

    const noScope = await mcp(mcpOf(G1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY_READ)
    check('KGM-S1e 无 kg_mcp 范围 403', noScope.status === 403 && noScope.json?.error?.code === 'insufficient_scope', `${noScope.status} ${noScope.text.slice(0, 160)}`)

    const missingKg = await mcp(mcpOf(99999999), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY)
    check('KGM-S1f kgId 不存在 404', missingKg.status === 404, `${missingKg.status}`)

    const crossTeam = await mcp(mcpOf(G3), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY)
    check('KGM-S1g 跨团队 kgId 404', crossTeam.status === 404, `${crossTeam.status}`)

    // ===== @KGM-S2 tools/list 域隔离 =====
    const listTools = await mcp(mcpOf(G1), rpc('tools/list', {}), TEAM_KEY)
    const toolNames = (listTools.json?.result?.tools ?? []).map((t) => t.name).sort()
    check('KGM-S2a KG 端点 tools/list 恰好四个只读工具', listTools.status === 200
      && JSON.stringify(toolNames) === JSON.stringify(['get_knowledge_graph_schema', 'list_knowledge_graphs', 'search_knowledge_graph_nodes', 'search_knowledge_graph_recall']), JSON.stringify(toolNames))
    const recallTool = (listTools.json?.result?.tools ?? []).find((t) => t.name === 'search_knowledge_graph_recall')
    check('KGM-S2b 工具带 inputSchema', Boolean(recallTool?.inputSchema?.properties), '')

    if (Number.isFinite(W1) && W1 > 0) {
      const wikiTools = await mcp(`/api/external/wiki/${W1}/mcp`, rpc('tools/list', {}), TEAM_KEY_WIKI_MCP)
      const wikiNames = (wikiTools.json?.result?.tools ?? []).map((t) => t.name).sort()
      check('KGM-S2c wiki 端点 tools/list 仍恰好三个知识库工具（反向域隔离）', wikiTools.status === 200
        && JSON.stringify(wikiNames) === JSON.stringify(['list_knowledge_bases', 'search_knowledge_base_files', 'search_knowledge_base_recall']), `status=${wikiTools.status} tools=${JSON.stringify(wikiNames)}`)

      const crossCall = await mcp(mcpOf(G1), rpc('tools/call', { name: 'list_knowledge_bases', arguments: {} }), TEAM_KEY)
      const crossRejected = crossCall.status !== 200 || crossCall.json?.error != null || toolPayload(crossCall).isError
      check('KGM-S2d KG 端点调用 wiki 工具被门禁拒绝', crossRejected, `${crossCall.status} ${crossCall.text.slice(0, 160)}`)
    } else {
      skip('KGM-S2c/d 域隔离反向断言（wiki 创建失败）')
    }

    // ===== @KGM-S3 list_knowledge_graphs =====
    const listRes = await mcp(mcpOf(G1), rpc('tools/call', { name: 'list_knowledge_graphs', arguments: {} }), TEAM_KEY)
    const listData = toolPayload(listRes)
    const listed = Array.isArray(listData.data?.items) ? listData.data.items : []
    check('KGM-S3a 列表 200 且含 G1/G2（仅本团队托管图）', listRes.status === 200 && !listData.isError && listed.some((x) => Number(x.kgId) === G1) && listed.some((x) => Number(x.kgId) === G2) && !listed.some((x) => Number(x.kgId) === G3), `${listRes.status} ${listData.text.slice(0, 200)}`)
    const g1Item = listed.find((x) => Number(x.kgId) === G1)
    check('KGM-S3b 条目字段完整（kgId/name/description）', g1Item && typeof g1Item.name === 'string' && typeof g1Item.description === 'string', JSON.stringify(g1Item)?.slice(0, 160))

    // ===== @KGM-S4 get_knowledge_graph_schema =====
    const schemaRes = await mcp(mcpOf(G1), rpc('tools/call', { name: 'get_knowledge_graph_schema', arguments: {} }), TEAM_KEY)
    const schemaData = toolPayload(schemaRes)
    const portDef = (schemaData.data?.entityTypes ?? []).find((x) => Number(x.entityTypeId) === PORT)
    check('KGM-S4a 不传 kgId 默认用路径图谱且 schema 200', schemaRes.status === 200 && !schemaData.isError && Number(schemaData.data?.kgId) === G1, `${schemaRes.status} ${schemaData.text.slice(0, 200)}`)
    check('KGM-S4b 实体类型含属性定义（港口代码/必填）', portDef && (portDef.properties ?? []).some((p) => p.name === '港口代码' && p.required === true), JSON.stringify(portDef)?.slice(0, 200))
    const depDef = (schemaData.data?.relationTypes ?? []).find((x) => Number(x.relationTypeId) === DEP)
    check('KGM-S4c 关系类型含起止约束', depDef && Number(depDef.sourceTypeId) === PORT && Number(depDef.targetTypeId) === LEG, JSON.stringify(depDef)?.slice(0, 160))
    const schemaExplicit = await mcp(mcpOf(G1), rpc('tools/call', { name: 'get_knowledge_graph_schema', arguments: { kgId: G3 } }), TEAM_KEY)
    check('KGM-S4d 跨团队 kgId 拒绝', schemaExplicit.status === 200 && toolPayload(schemaExplicit).isError, `${toolPayload(schemaExplicit).text.slice(0, 160)}`)

    // ===== @KGM-S5 search_knowledge_graph_nodes =====
    const nodesDefault = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_nodes', arguments: { query: '上海' } }), TEAM_KEY)
    const nodesDefaultData = toolPayload(nodesDefault)
    check('KGM-S5a 关键字搜索命中且默认路径 kgId', nodesDefault.status === 200 && !nodesDefaultData.isError && Number(nodesDefaultData.data?.kgId) === G1 && (nodesDefaultData.data?.items ?? []).some((x) => x.name === namePort), `${nodesDefault.status} ${nodesDefaultData.text.slice(0, 200)}`)
    const portNode = (nodesDefaultData.data?.items ?? []).find((x) => x.name === namePort)
    check('KGM-S5b 命中项带实体类型名', portNode && portNode.entityTypeName === portType && String(portNode.nodeId) === idPort, JSON.stringify(portNode)?.slice(0, 200))

    const nodesTyped = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_nodes', arguments: { entityTypeId: LEG } }), TEAM_KEY)
    const nodesTypedData = toolPayload(nodesTyped)
    check('KGM-S5c 按实体类型过滤（航段 1 条、无港口）', nodesTyped.status === 200 && (nodesTypedData.data?.items ?? []).length === 1 && (nodesTypedData.data?.items ?? [])[0]?.name === nameLeg, `${nodesTypedData.text.slice(0, 160)}`)

    const nodesPage = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_nodes', arguments: { pageSize: 1, pageNo: 2 } }), TEAM_KEY)
    const nodesPageData = toolPayload(nodesPage)
    check('KGM-S5d 分页 pageSize=1 第 2 页', nodesPage.status === 200 && (nodesPageData.data?.items ?? []).length <= 1 && Number(nodesPageData.data?.pageNo) === 2, `${nodesPageData.text.slice(0, 160)}`)

    const nodesCross = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_nodes', arguments: { kgId: G3 } }), TEAM_KEY)
    check('KGM-S5e 跨团队 kgId 拒绝', nodesCross.status === 200 && toolPayload(nodesCross).isError, `${toolPayload(nodesCross).text.slice(0, 160)}`)

    // ===== @KGM-S6 search_knowledge_graph_recall =====
    const recallEmpty = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '  ' } }), TEAM_KEY)
    check('KGM-S6a 空查询被拒绝', recallEmpty.status === 200 && toolPayload(recallEmpty).isError, `${toolPayload(recallEmpty).text.slice(0, 120)}`)

    const badScore = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '港口', minScore: 1.5 } }), TEAM_KEY)
    check('KGM-S6b 非法阈值被拒绝', badScore.status === 200 && toolPayload(badScore).isError && toolPayload(badScore).text.includes('0-1'), `${toolPayload(badScore).text.slice(0, 160)}`)

    // G2 未配置向量化：召回返回 skippedHint 可读说明（不报错）
    const noEmb = await mcp(mcpOf(G2), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '任何问题' } }), TEAM_KEY)
    const noEmbData = toolPayload(noEmb)
    check('KGM-S6c 未配向量化返回 skippedHint', noEmb.status === 200 && !noEmbData.isError && (noEmbData.data?.items ?? []).length === 0 && typeof noEmbData.data?.skippedHint === 'string' && noEmbData.data.skippedHint.length > 0, `${noEmbData.text.slice(0, 200)}`)

    if (hasEmb) {
      // 轮询等 G1 节点向量就绪（节点写入 → MQ 增量 → pgvector）
      let recallOk = false
      let recallData = null
      for (let i = 0; i < 30 && !recallOk; i++) {
        const recallRes = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '上海港 华东 集装箱', top: 10 } }), TEAM_KEY)
        recallData = toolPayload(recallRes)
        recallOk = recallRes.status === 200 && !recallData.isError && (recallData.data?.items ?? []).some((x) => x.name === namePort)
        if (!recallOk) await new Promise((resolve) => setTimeout(resolve, 2000))
      }
      const items = Array.isArray(recallData?.data?.items) ? recallData.data.items : []
      check('KGM-S6d 向量召回命中上海港', recallOk, `${recallData?.text.slice(0, 200)}`)
      const portHit = items.find((x) => x.name === namePort)
      check('KGM-S6e 命中项带类型名/得分/一跳邻居（MCP出发→航段）', portHit && portHit.entityTypeName === portType && typeof portHit.score === 'number'
        && (portHit.neighbors ?? []).some((n) => n.direction === 'out' && n.name === nameLeg && typeof n.relationName === 'string' && n.relationName.includes('MCP出发')), JSON.stringify(portHit)?.slice(0, 240))
      const scores = items.map((x) => Number(x.score ?? 0))
      check('KGM-S6f 得分降序', scores.every((s, i) => i === 0 || scores[i - 1] >= s), JSON.stringify(scores))

      const highBar = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '上海港 华东 集装箱', minScore: 1, top: 10 } }), TEAM_KEY)
      check('KGM-S6g 阈值=1 时 0 命中', (toolPayload(highBar).data?.items ?? []).length === 0, '')

      const top1 = await mcp(mcpOf(G1), rpc('tools/call', { name: 'search_knowledge_graph_recall', arguments: { queryText: '上海港 华东 集装箱', top: 1 } }), TEAM_KEY)
      check('KGM-S6h top=1 仅 1 条', (toolPayload(top1).data?.items ?? []).length <= 1, '')
    } else {
      skip('KGM-S6d~h 向量召回正向用例（桩模型链路不可用）')
    }

    // ===== @KGM-S7 协议行为 =====
    const unknownTool = await mcp(mcpOf(G1), rpc('tools/call', { name: 'not_a_tool', arguments: {} }), TEAM_KEY)
    check('KGM-S7a 未知工具被拒（JSON-RPC error 或 isError 结果）', unknownTool.status === 200
      && (unknownTool.json?.error?.code === -32602 || toolPayload(unknownTool).isError), `${unknownTool.text.slice(0, 120)}`)

    const getRes = await fetch(BASE + mcpOf(G1), { method: 'GET', headers: { Accept: 'text/event-stream', ...mcpHeaders(TEAM_KEY) } })
    check('KGM-S7b GET 405', getRes.status === 405, `${getRes.status}`)

    const noInit = await mcp(mcpOf(G1), rpc('tools/call', { name: 'list_knowledge_graphs', arguments: {} }), TEAM_KEY)
    check('KGM-S7c 无状态：未 initialize 直接 tools/call 200', noInit.status === 200 && !toolPayload(noInit).isError, `${noInit.status} ${toolPayload(noInit).text.slice(0, 120)}`)

    const note = await mcp(mcpOf(G1), notify('notifications/initialized'), TEAM_KEY)
    check('KGM-S7d 通知返回 202', note.status === 202, `${note.status}`)

    // 回归：外部 REST 知识图谱接口不受 MCP 上线影响
    const restList = await mcp(mcpOf(G1), rpc('tools/call', { name: 'get_knowledge_graph_schema', arguments: { kgId: G1 } }), ACCESS_KEY)
    check('KGM-S7e 应用接入 key 工具调用回归 200', restList.status === 200 && !toolPayload(restList).isError, `${toolPayload(restList).text.slice(0, 160)}`)
  } finally {
    // ===== 清理：删图谱/wiki/接入/key/团队归档，可重复执行 =====
    await api('DELETE', kg(G1), { token: admin })
    await api('DELETE', kg(G2), { token: admin })
    await api('DELETE', kg(G3), { token: admin })
    if (Number.isFinite(W1) && W1 > 0) await api('DELETE', `/api/wiki/${W1}`, { token: admin })
    const accList = await api('GET', '/api/access-app/list?teamId=' + T1, { token: admin }).catch(() => ({ json: { items: [] } }))
    for (const item of (accList.json?.items ?? [])) {
      if (String(item.name ?? '').startsWith('kgm接入-')) await api('DELETE', `/api/access-app/${item.id}`, { token: admin })
    }
    await api('PUT', `/api/admin/team/${T1}/disable`, { token: admin, body: { isDisable: true } })
    await api('PUT', `/api/admin/team/${T2}/disable`, { token: admin, body: { isDisable: true } })
  }

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  if (FAIL > 0) process.exitCode = 1
}

main().catch((err) => {
  console.error('E2E 执行失败:', err)
  process.exit(1)
})
