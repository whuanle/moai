// 知识库 MCP 服务器 E2E（场景 @WM-S1 ~ @WM-S6；后端 127.0.0.1:5330，可用参数覆盖）
// 端点：/api/external/wiki/{wikiId}/mcp（MCP streamable HTTP，无状态模式，免 Mcp-Session-Id）
// 覆盖：鉴权门禁（无凭证 401 / 伪造 key 401 / 无 wiki_mcp 范围 403 / wikiId 不存在与跨团队 404）
//       → tools/list 三个只读工具 → list_knowledge_bases（团队知识库列表）
//       → search_knowledge_base_files（默认路径 wikiId / 显式 wikiId / 关键字 / 分页 / 跨团队拒绝）
//       → search_knowledge_base_recall（向量召回 / 文档范围 / 阈值 / top / 参数校验）
//       → 协议行为（未知工具 -32602 / GET 405 / 无 initialize 直接调用 / 通知 202 / 外部 REST 回归）。
// 模型策略：与 wiki-recall-e2e 一致——本地 OpenAI 兼容桩（确定性哈希向量）自举渠道与模型，
//       桩不可用时不影响鉴权/文件搜索/协议场景，仅召回正 向用例跳过。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5330'
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
// 工具结果文本（SDK 以 SSE 帧 data: 包裹 JSON-RPC；工具返回对象序列化为 content[0].text）
function toolPayload(res) {
  const text = res.json?.result?.content?.find((c) => c.type === 'text')?.text ?? ''
  try { return { isError: res.json?.result?.isError === true, text, data: JSON.parse(text) } } catch { return { isError: res.json?.result?.isError === true, text, data: null } }
}
const initHandshake = (mcpPath, key, mode) => mcp(mcpPath, rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'wiki-mcp-e2e', version: '0' } }), key, mode)

const TS = Date.now().toString().slice(-8)
const mcpOf = (wikiId) => `/api/external/wiki/${wikiId}/mcp`

async function uploadWikiDoc(token, wikiId, fileName, content, contentType) {
  const sha256 = crypto.createHash('sha256').update(content).digest('hex')
  const pre = await api('POST', `/api/wiki/${wikiId}/documents/preupload`, {
    token,
    body: { wikiId, fileName, contentType, fileSize: content.length, sha256 },
  })
  if (pre.status !== 200) return { ok: false, documentId: null, res: pre }
  const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: content })
  if (put.status !== 200) return { ok: false, documentId: null, res: { status: put.status, text: '' } }
  const complete = await api('POST', `/api/wiki/${wikiId}/documents/complete`, {
    token,
    body: { wikiId, isSuccess: true, fileId: pre.json.fileId, fileName },
  })
  if (complete.status !== 200) return { ok: false, documentId: null, res: complete }
  const list = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const item = (list.json?.items ?? []).find((i) => Number(i.fileId) === Number(pre.json.fileId))
  return { ok: true, documentId: item ? Number(item.documentId) : null, res: complete }
}

const makeMd = (title, keywords, words = 8) => Buffer.from(
  `# ${title}\n\n## 说明\n\n` +
  `本文用于验证知识库 MCP 召回。${keywords}。`.repeat(words) +
  `\n\n## 要点\n\n1. ${keywords}是本文核心主题\n2. 如需了解更多请联系管理员\n`,
  'utf8',
)

async function listDocs(token, wikiId) {
  const r = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  return new Map((r.json?.items ?? []).map((i) => [Number(i.documentId), i]))
}
async function waitUntil(token, wikiId, predicate, timeoutMs = 120000) {
  const start = Date.now()
  while (Date.now() - start < timeoutMs) {
    const docs = await listDocs(token, wikiId)
    if (predicate(docs)) return { ok: true, docs }
    await new Promise((resolve) => setTimeout(resolve, 2000))
  }
  const docs = await listDocs(token, wikiId)
  return { ok: false, docs }
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

// 确定性哈希向量：同词文本向量相近（按 3-gram 哈希落桶），保证「退货政策」查询能命中退货文档
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

  // ===== 准备：团队 + 两个知识库 + 应用接入 key + 团队接入 key（含/不含 wiki_mcp） =====
  const T = Number((await retryApi('POST', '/api/team', { name: 'wm-team-' + TS }, admin)).json?.value)
  if (!Number.isFinite(T)) throw new Error('创建团队失败')
  const w1Res = await retryApi('POST', '/api/wiki', { teamId: T, name: 'wm-main-' + TS, description: 'wiki mcp e2e 主库' }, admin)
  const w2Res = await retryApi('POST', '/api/wiki', { teamId: T, name: 'wm-second-' + TS, description: 'wiki mcp e2e 副库' }, admin)
  const W1 = Number(w1Res.json?.value)
  const W2 = Number(w2Res.json?.value)
  if (!Number.isFinite(W1) || !Number.isFinite(W2)) throw new Error(`创建知识库失败: W1=${w1Res.status} ${w1Res.text.slice(0, 160)} | W2=${w2Res.status} ${w2Res.text.slice(0, 160)}`)

  // 应用接入勾选 wiki_read + wiki_mcp（wiki_read 供末尾 REST 回归，wiki_mcp 供 MCP 端点）
  const acc = await retryApi('POST', '/api/access-app', { teamId: T, name: 'wm接入-' + TS, description: 'wiki mcp e2e', scopes: ['wiki_read', 'wiki_mcp'] }, admin)
  const ACCESS_KEY = acc.json?.key
  if (!ACCESS_KEY) throw new Error(`创建应用接入失败: ${acc.status} ${acc.text.slice(0, 120)}`)

  // 团队接入 key（moai-）已下线：MCP 场景改用应用接入 key（moai-ac-）
  const kMcp = await retryApi('POST', '/api/access-app', { teamId: T, name: 'wm接入MCP-' + TS, description: 'wiki mcp e2e', scopes: ['wiki_mcp'] }, admin)
  const TEAM_KEY = kMcp.json?.key
  const kRead = await retryApi('POST', '/api/access-app', { teamId: T, name: 'wm接入READ-' + TS, description: 'wiki mcp e2e', scopes: ['wiki_read'] }, admin)
  const TEAM_KEY_READ = kRead.json?.key
  if (!TEAM_KEY || !TEAM_KEY_READ) throw new Error(`创建应用接入 key 失败: ${kMcp.status}/${kRead.status}`)

  // 跨团队知识库（404 用例）
  const T2 = Number((await retryApi('POST', '/api/team', { name: 'wm-team2-' + TS }, admin)).json?.value)
  const W3 = Number((await retryApi('POST', '/api/wiki', { teamId: T2, name: 'wm-other-' + TS, description: '别的团队' }, admin)).json?.value)

  // ===== 向量准备：桩渠道 + 模型授权 + embedding 配置 + 两文档切割向量化 =====
  let hasEmb = false
  let doc1Id = null, doc2Id = null
  try {
    const stub = await startStubServer()
    const stubPort = stub.address().port
    const chName = `wm-stub渠道-${TS}`
    const crc = await api('POST', '/api/ai/channel', {
      token: admin,
      body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'wm-stub-key', enabled: true, description: 'wiki-mcp E2E 桩渠道' },
    })
    const channels = await api('GET', '/api/ai/channel', { token: admin })
    const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
    const embName = `wm-stub-embed-${TS}`
    const mrc = CH_ID ? await api('POST', '/api/ai/model', {
      token: admin,
      body: { channelId: CH_ID, meta: { modelId: embName, name: embName, modelKind: 'embedding', description: 'wiki-mcp E2E 桩模型' }, enabled: true, isPublic: false },
    }) : { status: 0 }
    const models = CH_ID ? await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: admin }) : { json: {} }
    const EMB_ID = String((models.json?.items ?? []).find((m) => m.name === embName)?.id ?? '')
    if (EMB_ID) {
      const cur = await api('GET', `/api/ai/model/${EMB_ID}/authorization`, { token: admin })
      const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), T])]
      await api('PUT', `/api/ai/model/${EMB_ID}/authorization`, { token: admin, body: { modelId: EMB_ID, teamIds } })
    }
    hasEmb = crc.status === 200 && CH_ID !== '' && EMB_ID !== ''
    if (!hasEmb) console.log(`INFO | 桩模型链路不可用 ch=${crc.status}/${CH_ID} model=${mrc.status}/${EMB_ID}，召回正向用例跳过`)

    if (hasEmb) {
      const bind = await api('PUT', `/api/wiki/${W1}/embedding-config`, { token: admin, body: { wikiId: W1, embeddingModelId: EMB_ID, embeddingDimensions: 1024 } })
      hasEmb = bind.status === 200
      if (!hasEmb) console.log(`INFO | embedding 配置绑定失败 ${bind.status} ${bind.text.slice(0, 120)}`)
    }

    if (hasEmb) {
      const d1 = await uploadWikiDoc(admin, W1, `wm-return-${TS}.md`, makeMd('退货政策说明', '商品签收后 7 天内可无理由退货，退货时需保证商品完好，运费由买家承担'), 'text/markdown')
      const d2 = await uploadWikiDoc(admin, W1, `wm-invoice-${TS}.md`, makeMd('发票开具流程', '发票在订单完成后 30 天内开具，支持电子发票和纸质发票，抬头信息需准确'), 'text/markdown')
      doc1Id = d1.documentId
      doc2Id = d2.documentId
      const upOk = Boolean(doc1Id) && Boolean(doc2Id)
      if (!upOk) { hasEmb = false; console.log('INFO | 文档上传失败，召回正向用例跳过') }
      if (upOk) {
        const part = await api('POST', `/api/wiki/${W1}/documents/batch-workflow`, {
          token: admin,
          body: { wikiId: W1, documentIds: [doc1Id, doc2Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' },
        })
        const embed = await api('POST', `/api/wiki/${W1}/documents/batch-workflow`, {
          token: admin,
          body: { wikiId: W1, documentIds: [doc1Id, doc2Id], isEmbedding: true, embedSourceText: true, embedMetadata: false },
        })
        const done = await waitUntil(admin, W1, (docs) => docs.get(doc1Id)?.isEmbedding === true && docs.get(doc2Id)?.isEmbedding === true, 120000)
        hasEmb = done.ok
        if (!done.ok) console.log(`INFO | 向量化未完成 part=${part.status} embed=${embed.status}，召回正向用例跳过`)
      }
    }
  } catch (err) {
    console.log(`INFO | 向量准备异常：${err.message}，召回正向用例跳过`)
    hasEmb = false
  }

  // ===== @WM-S1 鉴权门禁 =====
  const noAuth = await mcp(mcpOf(W1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), null)
  check('WM-S1a 无凭证 401', noAuth.status === 401, `${noAuth.status} ${noAuth.text.slice(0, 120)}`)

  const fakeKey = await mcp(mcpOf(W1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), 'moai-not-a-real-key')
  check('WM-S1b 伪造 key 401', fakeKey.status === 401, `${fakeKey.status}`)

  const initApp = await initHandshake(mcpOf(W1), ACCESS_KEY)
  check('WM-S1c 应用接入 key initialize 200', initApp.status === 200 && initApp.json?.result?.serverInfo?.name, `${initApp.status} ${initApp.text.slice(0, 160)}`)

  const initTeamKey = await mcp(mcpOf(W1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY, 'x-api-key')
  check('WM-S1d 团队接入 key（x-api-key）initialize 200', initTeamKey.status === 200 && Boolean(initTeamKey.json?.result?.serverInfo?.name), `${initTeamKey.status}`)

  const noScope = await mcp(mcpOf(W1), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY_READ)
  check('WM-S1e 无 wiki_mcp 范围 403', noScope.status === 403 && noScope.json?.error?.code === 'insufficient_scope', `${noScope.status} ${noScope.text.slice(0, 160)}`)

  const missingWiki = await mcp(mcpOf(99999999), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY)
  check('WM-S1f wikiId 不存在 404', missingWiki.status === 404, `${missingWiki.status}`)

  const crossTeam = await mcp(mcpOf(W3), rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'e2e', version: '0' } }), TEAM_KEY)
  check('WM-S1g 跨团队 wikiId 404', crossTeam.status === 404, `${crossTeam.status}`)

  // ===== @WM-S2 tools/list =====
  const listTools = await mcp(mcpOf(W1), rpc('tools/list', {}), TEAM_KEY)
  const toolNames = (listTools.json?.result?.tools ?? []).map((t) => t.name).sort()
  check('WM-S2 tools/list 返回三个只读工具', listTools.status === 200
    && JSON.stringify(toolNames) === JSON.stringify(['list_knowledge_bases', 'search_knowledge_base_files', 'search_knowledge_base_recall']), JSON.stringify(toolNames))
  const recallTool = (listTools.json?.result?.tools ?? []).find((t) => t.name === 'search_knowledge_base_recall')
  check('WM-S2 工具带 inputSchema', Boolean(recallTool?.inputSchema?.properties), '')

  // ===== @WM-S3 list_knowledge_bases =====
  const listRes = await mcp(mcpOf(W1), rpc('tools/call', { name: 'list_knowledge_bases', arguments: {} }), TEAM_KEY)
  const listData = toolPayload(listRes)
  const listed = Array.isArray(listData.data?.items) ? listData.data.items : []
  check('WM-S3a 列表 200 且含 W1/W2', listRes.status === 200 && !listData.isError && listed.some((x) => Number(x.wikiId) === W1) && listed.some((x) => Number(x.wikiId) === W2), `${listRes.status} ${listData.text.slice(0, 160)}`)
  const w1Item = listed.find((x) => Number(x.wikiId) === W1)
  check('WM-S3b 条目字段完整', w1Item && typeof w1Item.name === 'string' && typeof w1Item.documentCount === 'number' && typeof w1Item.chunkCount === 'number', JSON.stringify(w1Item)?.slice(0, 160))

  // ===== @WM-S4 search_knowledge_base_files =====
  const filesDefault = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_files', arguments: { query: 'wm-return' } }), TEAM_KEY)
  const filesDefaultData = toolPayload(filesDefault)
  check('WM-S4a 不传 wikiId 默认用路径知识库', filesDefault.status === 200 && !filesDefaultData.isError && Number(filesDefaultData.data?.wikiId) === W1 && (filesDefaultData.data?.items ?? []).length >= 1, `${filesDefault.status} ${filesDefaultData.text.slice(0, 200)}`)

  const filesExplicit = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_files', arguments: { wikiId: W1, query: '' } }), TEAM_KEY)
  const filesExplicitData = toolPayload(filesExplicit)
  check('WM-S4b 显式 wikiId + 空关键字返回全部文档', filesExplicit.status === 200 && !filesExplicitData.isError && (filesExplicitData.data?.items ?? []).length >= (doc1Id ? 2 : 0), `${filesExplicitData.text.slice(0, 200)}`)

  const filesMiss = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_files', arguments: { query: '绝对不存在的文件名-x1' } }), TEAM_KEY)
  const filesMissData = toolPayload(filesMiss)
  check('WM-S4c 关键字不命中 total=0', filesMiss.status === 200 && !filesMissData.isError && Number(filesMissData.data?.total) === 0, `${filesMissData.text.slice(0, 160)}`)

  const filesPage = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_files', arguments: { wikiId: W1, pageSize: 1, pageNo: 2 } }), TEAM_KEY)
  const filesPageData = toolPayload(filesPage)
  check('WM-S4d 分页 pageSize=1 第 2 页', filesPage.status === 200 && !filesPageData.isError && (filesPageData.data?.items ?? []).length <= 1 && Number(filesPageData.data?.pageNo) === 2, `${filesPageData.text.slice(0, 160)}`)

  const filesCross = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_files', arguments: { wikiId: W3, query: '' } }), TEAM_KEY)
  const filesCrossData = toolPayload(filesCross)
  check('WM-S4e 跨团队 wikiId 拒绝', filesCross.status === 200 && filesCrossData.isError && filesCrossData.text.includes('知识库不存在'), `${filesCross.status} ${filesCrossData.text.slice(0, 160)}`)

  // ===== @WM-S5 search_knowledge_base_recall =====
  const recallEmpty = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '  ' } }), TEAM_KEY)
  check('WM-S5a 空查询被拒绝', recallEmpty.status === 200 && toolPayload(recallEmpty).isError, `${recallEmpty.status} ${toolPayload(recallEmpty).text.slice(0, 120)}`)

  const badScore = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '退货', minScore: 1.5 } }), TEAM_KEY)
  check('WM-S5b 非法阈值被拒绝', badScore.status === 200 && toolPayload(badScore).isError && toolPayload(badScore).text.includes('0-1'), `${toolPayload(badScore).text.slice(0, 160)}`)

  if (hasEmb && doc1Id && doc2Id) {
    const recallRes = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '退货政策 运费', top: 10 } }), TEAM_KEY)
    const recallData = toolPayload(recallRes)
    const items = Array.isArray(recallData.data?.items) ? recallData.data.items : []
    check('WM-S5c 向量召回有命中', recallRes.status === 200 && !recallData.isError && items.length > 0, `${recallRes.status} ${recallData.text.slice(0, 200)}`)
    const scores = items.map((x) => Number(x.score ?? 0))
    check('WM-S5d 得分降序', scores.every((s, i) => i === 0 || scores[i - 1] >= s), JSON.stringify(scores))
    check('WM-S5e 命中项带文档名与切片 id', items.every((x) => String(x.documentName ?? '') !== '' && Number(x.chunkId ?? 0) > 0), JSON.stringify(items[0] ?? {}).slice(0, 160))
    check('WM-S5f 命中项带内容类型标签', items.every((x) => typeof x.contentType === 'string' && x.contentType.length > 0), JSON.stringify(items[0] ?? {}).slice(0, 120))

    const scoped = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '退货政策 运费', documentIds: [doc2Id], top: 10 } }), TEAM_KEY)
    const scopedItems = toolPayload(scoped).data?.items ?? []
    check('WM-S5g 文档范围过滤生效', scoped.status === 200 && scopedItems.every((x) => Number(x.documentId) === doc2Id), JSON.stringify(scopedItems.map((x) => x.documentId)))

    const highBar = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '退货政策 运费', minScore: 1, top: 10 } }), TEAM_KEY)
    check('WM-S5h 阈值=1 时 0 命中', (toolPayload(highBar).data?.items ?? []).length === 0, '')

    const top1 = await mcp(mcpOf(W1), rpc('tools/call', { name: 'search_knowledge_base_recall', arguments: { queryText: '发票 开具', top: 1 } }), TEAM_KEY)
    check('WM-S5i top=1 仅 1 条', (toolPayload(top1).data?.items ?? []).length <= 1, '')
  } else {
    skip('WM-S5c~i 向量召回正向用例（桩模型链路不可用）')
  }

  // ===== @WM-S6 协议行为 =====
  const unknownTool = await mcp(mcpOf(W1), rpc('tools/call', { name: 'not_a_tool', arguments: {} }), TEAM_KEY)
  check('WM-S6a 未知工具 -32602', unknownTool.status === 200 && unknownTool.json?.error?.code === -32602, `${unknownTool.text.slice(0, 120)}`)

  const getRes = await fetch(BASE + mcpOf(W1), { method: 'GET', headers: { Accept: 'text/event-stream', ...mcpHeaders(TEAM_KEY) } })
  check('WM-S6b GET 405', getRes.status === 405, `${getRes.status}`)

  const noInit = await mcp(mcpOf(W1), rpc('tools/call', { name: 'list_knowledge_bases', arguments: {} }), TEAM_KEY)
  check('WM-S6c 无状态：未 initialize 直接 tools/call 200', noInit.status === 200 && !toolPayload(noInit).isError, `${noInit.status} ${toolPayload(noInit).text.slice(0, 120)}`)

  const note = await mcp(mcpOf(W1), notify('notifications/initialized'), TEAM_KEY)
  check('WM-S6d 通知返回 202', note.status === 202, `${note.status}`)

  // 回归：外部 REST 知识库接口不受 MCP 上线影响
  const appToken = await api('POST', '/api/external/token', { body: { accessAppKey: ACCESS_KEY } })
  const restList = await api('POST', `/api/external/wiki/${W1}/documents/list`, { token: appToken.json?.accessToken, body: { pageNo: 1, pageSize: 10 } })
  check('WM-S6e 外部 REST 接口回归 200', restList.status === 200 && Array.isArray(restList.json?.items), `${restList.status} ${restList.text.slice(0, 120)}`)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  if (FAIL > 0) process.exitCode = 1
}

main().catch((err) => {
  console.error('E2E 执行失败:', err)
  process.exit(1)
})
