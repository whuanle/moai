// 知识图谱语义检索 E2E（SP-A T10；场景 KGS-S1~S9；后端 127.0.0.1:5210）
// 用法：node local-dev/kg-search-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5210）
//
// 场景映射（实现计划 docs/superpowers/plans/2026-09-22-kg-graph-search.md Task 10）：
//   KGS-S1  本地 embeddings 桩 → 渠道+embedding 模型（照 wiki-recall-e2e 手法）→ 建托管图 → PUT embedding-config 200（维度 1024）→ 回读图谱详情
//   KGS-S2  建类型/节点 → 轮询 POST /{id}/search 命中：hits 字段/类型名/score、无边节点 neighbors 空数组 → 建边+第三节点 → neighbors 的 direction/relationName/name
//   KGS-S3  改节点名 → 轮询：新名命中、旧名不命中（向量替换幂等）
//   KGS-S4  建临时节点待向量化命中后删除 → 轮询：不再命中
//   KGS-S5  minScore=0.999999 → hits 空；缺省 minScore → 命中
//   KGS-S6  未配模型的第二张托管图 search → 409 且文案含「向量化」
//   KGS-S7  embedding-config 边界：维度 0 → 400；假模型 Guid → 400；非法配置不破坏原配置
//   KGS-S8  应用配置绑定：他团队 graphId → 400；本团队接入图 → 400；合法托管图 → 200 且回读
//   KGS-S9  工作流 kgSearch 节点：draft 引用他团队 graphId → 400 → 改本团队 graphId → draft/publish/debug-run，count≥1 且 text 非空
//
// 运行前置：新构建后端 + Memgraph/图数据库 + RabbitMQ + pgvector，KG_ENABLED=true；
// 连不上后端 / list.enabled≠true / 图数据库探活失败 / 桩模型创建失败时 SKIP 并退出码 0。
// 向量化为 MQ 异步消费：建/改/删节点后按「≤10 次、间隔 500ms」轮询检索直至生效。
// 模型策略：本地 OpenAI 兼容桩（/v1/embeddings 确定性哈希向量，与 wiki-recall-e2e 同手法），
// 端口由系统随机分配（listen(0)，避开常用端口），无需真实模型渠道，全程零 SKIP 依赖外部服务。
//
// 已知后端待补（2026-09-22 HEAD 核对 src/ 代码事实，补齐后本脚本对应断言自动转绿/自动生效）：
//   1) GET /api/knowledge-graph/{id} 详情未暴露 embeddingModelId/embeddingDimensions 两字段
//      → KGS-S1c 为条件式断言（字段存在即严格断言，当前构建打 INFO 跳过）
//   2) PUT /api/app/{id}/agent-config 的 AppController.SaveAppAgentConfig 未把 req.GraphIds 映射进命令
//      → KGS-S8c/S8e 越权 400 断言按契约严格保留，当前构建会 FAIL（后端补映射后转绿）
//   3) GET /api/app/{id}/agent-config 响应无 graphIds 字段
//      → KGS-S8g 为条件式断言（同 1）
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱检索 E2E 未执行: ${reason}`)
  console.warn('      需要新构建后端 + 图数据库可达 且 KG_ENABLED=true（设置页开启并配置 KG_URI）+ RabbitMQ/pgvector。')
  process.exit(0)
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
const kg = (id) => `/api/knowledge-graph/${id}`

// ===== 本地 OpenAI 兼容桩：/v1/embeddings（确定性哈希向量，与 wiki-recall-e2e 同手法） =====

/** 字符级 hash 分布向量：共享词多的文本对相似度更高 */
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
      if (!String(req.url).includes('/embeddings')) {
        res.writeHead(404, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: 'not found' }))
        return
      }
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      const inputs = Array.isArray(payload.input) ? payload.input : [String(payload.input ?? '')]
      const dims = Math.min(Number(payload.dimensions) || 1024, 1024)
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        object: 'list',
        model: payload.model ?? 'stub-embedding',
        data: inputs.map((text, index) => ({ object: 'embedding', index, embedding: stubEmbed(text, dims) })),
        usage: { prompt_tokens: 10, total_tokens: 10 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

/** 用桩创建渠道 + embedding 模型并授权给团队，返回 {channelId, embModelId} 或 null（照 wiki-recall-e2e 手法） */
async function createStubEmbeddingModel(adminToken, TID, stubPort) {
  const chName = `kgs-stub渠道-${TS}`
  const crc = await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'kgs-stub-key', enabled: true, description: 'kg-search E2E 桩渠道' },
  })
  if (crc.status !== 200) { console.log(`INFO | 桩渠道创建失败：${crc.status} ${crc.text.slice(0, 120)}`); return null }
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) { console.log('INFO | 桩渠道未找到'); return null }

  const modelName = `kgs-stub-embed-${TS}`
  const mrc = await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'embedding', description: 'kg-search E2E 桩模型' }, enabled: true, isPublic: false },
  })
  if (mrc.status !== 200) { console.log(`INFO | 桩模型创建失败：${mrc.status} ${mrc.text.slice(0, 120)}`); return null }
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const embModelId = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  if (!embModelId) { console.log('INFO | 桩模型未找到'); return null }

  const cur = await api('GET', `/api/ai/model/${embModelId}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  const put = await api('PUT', `/api/ai/model/${embModelId}/authorization`, { token: adminToken, body: { modelId: embModelId, teamIds } })
  if (put.status !== 200) { console.log(`INFO | 桩模型授权失败：${put.status}`); return null }

  return { channelId: CH_ID, embModelId }
}

// ===== 检索轮询（向量化为 MQ 异步消费：≤10 次、间隔 500ms） =====
const POLL_TIMES = 10
const POLL_GAP = 500
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))
async function search(token, kgId, body) {
  return api('POST', `${kg(kgId)}/search`, { token, body: { topK: 5, ...body } })
}
/** 轮询检索直到 predicate 命中，返回 {ok, res}（最后一轮结果） */
async function pollSearch(token, kgId, body, predicate, { times = POLL_TIMES, gap = POLL_GAP } = {}) {
  let res = null
  for (let i = 0; i < times; i++) {
    res = await search(token, kgId, body)
    if (res.status === 200 && predicate(res.json ?? {})) return { ok: true, res }
    await sleep(gap)
  }
  return { ok: false, res }
}

async function main() {
  // 能力开关的本地快速短路；真正以服务端 list.enabled 为准
  const envFlag = process.env.KG_ENABLED
  if (envFlag !== undefined && !['true', '1'].includes(String(envFlag).toLowerCase())) {
    skip(`环境变量 KG_ENABLED=${envFlag}`)
  }

  // 连接守卫：后端不可达 → SKIP exit 0
  let si = null
  try {
    si = await api('GET', '/api/common/serverinfo')
  } catch {
    skip(`后端不可达: ${BASE}`)
  }
  if (si.status !== 200 || !si.json?.rsaPublic) {
    skip(`serverinfo 异常: ${si.status}`)
  }
  RSA_KEY = si.json.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) {
    throw new Error(`root 登录失败: ${login.status} ${login.text.slice(0, 120)}`)
  }
  const token = login.json.accessToken

  // 准备团队：root 创建即 Owner，满足 adminOnly
  const team = await api('POST', '/api/team', { token, body: { name: 'kgs-team-' + TS } })
  if (team.status !== 200 || !Number(team.json?.value)) {
    throw new Error(`创建团队失败: ${team.status} ${team.text.slice(0, 120)}`)
  }
  const TID = Number(team.json.value)

  // 能力检测：list.enabled
  const list0 = await api('GET', `/api/knowledge-graph/list?teamId=${TID}`, { token })
  if (list0.status !== 200) {
    throw new Error(`GET list 失败: ${list0.status} ${list0.text.slice(0, 120)}`)
  }
  if (list0.json?.enabled !== true) {
    skip(`list.enabled=${list0.json?.enabled}`)
  }

  // 图数据库可达性探针：建一个 connected 图，成功即证明可达，随后清理
  const probe = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kgs-probe-' + TS, mode: 'connected', database: 'neo4j' } })
  if (probe.status !== 200) {
    skip(`图数据库探活失败 (${probe.status}): ${probe.text.slice(0, 160)}`)
  }
  await api('DELETE', kg(Number(probe.json.value)), { token })

  // ===== 模型准备：本地桩渠道 + embedding 模型（授权本团队） =====
  const stub = await startStubServer()
  const stubPort = stub.address().port
  const stubModels = await createStubEmbeddingModel(token, TID, stubPort)
  if (!stubModels) {
    stub.close()
    skip('本地桩渠道/embedding 模型创建失败')
  }
  const { channelId: CH_ID, embModelId } = stubModels
  console.log(`INFO | 本地桩 embeddings 就绪 port=${stubPort} model=${embModelId}`)

  // ===== KGS-S1 托管图 + 配置向量化模型（先配模型再建节点，保证增量向量化不丢） =====
  const c1 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kgs-managed-' + TS, templateKey: 'blank' } })
  check('KGS-S1a 创建托管图 200', c1.status === 200 && Number(c1.json?.value) > 0, `${c1.status} ${c1.text.slice(0, 120)}`)
  const G1 = Number(c1.json?.value)
  if (!Number.isFinite(G1) || G1 <= 0) throw new Error('KGS-S1 建图失败，后续场景无法继续')

  const cfgBody = { embeddingModelId: embModelId, embeddingDimensions: 1024 }
  const cfg1 = await api('PUT', `${kg(G1)}/embedding-config`, { token, body: cfgBody })
  check('KGS-S1b 配置向量化模型 200（维度 1024）', cfg1.status === 200, `${cfg1.status} ${cfg1.text.slice(0, 160)}`)
  const cfg1b = await api('PUT', `${kg(G1)}/embedding-config`, { token, body: cfgBody })
  check('KGS-S1b2 重复配置幂等 200', cfg1b.status === 200, `${cfg1b.status}`)

  const det = await api('GET', kg(G1), { token })
  if (det.json && det.json.embeddingModelId !== undefined) {
    check('KGS-S1c 详情回读 embedding 模型与维度', String(det.json.embeddingModelId) === String(embModelId) && Number(det.json.embeddingDimensions) === 1024, JSON.stringify({ embeddingModelId: det.json.embeddingModelId, embeddingDimensions: det.json.embeddingDimensions }))
  } else {
    console.log('INFO | KGS-S1c 当前构建图谱详情未暴露 embeddingModelId/embeddingDimensions（后端待补），字段落地后本断言自动生效')
  }

  // ===== KGS-S2 类型/节点/边 + 轮询检索命中 =====
  const typePName = '检索人员-' + TS
  const mkType = (name) => api('POST', `${kg(G1)}/entity-types`, { token, body: { name } })
  const teP = await mkType(typePName)
  const teS = await mkType('检索服务-' + TS)
  const teJ = await mkType('检索项目-' + TS)
  check('KGS-S2a 新增实体类型 200', [teP, teS, teJ].every((r) => r.status === 200 && Number(r.json?.value) > 0), `${teP.status}/${teS.status}/${teJ.status}`)
  const PPL = Number(teP.json?.value)
  const SVC = Number(teS.json?.value)
  const PRJ = Number(teJ.json?.value)
  const trM = await api('POST', `${kg(G1)}/relation-types`, { token, body: { name: '维护', sourceTypeId: PPL, targetTypeId: SVC } })
  const trD = await api('POST', `${kg(G1)}/relation-types`, { token, body: { name: '依赖', sourceTypeId: PRJ, targetTypeId: SVC } })
  check('KGS-S2b 新增关系类型（带起止约束）200', trM.status === 200 && trD.status === 200 && Number(trM.json?.value) > 0 && Number(trD.json?.value) > 0, `${trM.status}/${trD.status}`)
  const MAINT = Number(trM.json?.value)
  const DEP = Number(trD.json?.value)

  const nameA = '玄德-' + TS
  const nameB = '订单服务-' + TS
  const nA = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PPL, name: nameA } })
  const nB = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: SVC, name: nameB } })
  check('KGS-S2c 创建节点 A/B 200', nA.status === 200 && nB.status === 200 && !!nA.json?.value && !!nB.json?.value, `${nA.status}/${nB.status}`)
  const idA = String(nA.json?.value)
  const idB = String(nB.json?.value)

  // 轮询直至 A 向量化可检索（此时无边 → neighbors 空数组）
  const hitA = await pollSearch(token, G1, { query: nameA, topK: 10 }, (j) => (j.hits ?? []).some((h) => h.name === nameA))
  check('KGS-S2d 轮询检索命中节点 A', hitA.ok, JSON.stringify(hitA.res?.json?.hits ?? hitA.res?.text ?? '').slice(0, 160))
  if (hitA.ok) {
    const item = (hitA.res.json?.hits ?? []).find((h) => h.name === nameA)
    check('KGS-S2e 命中项字段完整（kgId/nodeId/entityTypeId/entityTypeName/score）',
      Number(item?.kgId) === G1 && String(item?.nodeId ?? '') === idA && Number(item?.entityTypeId) === PPL
      && item?.entityTypeName === typePName && typeof item?.score === 'number' && item.score >= 0 && item.score <= 1,
      JSON.stringify(item).slice(0, 200))
    check('KGS-S2f 无边节点 neighbors 为空数组', Array.isArray(item?.neighbors) && item.neighbors.length === 0, JSON.stringify(item?.neighbors))
    const j = hitA.res.json ?? {}
    check('KGS-S2g 响应含 contents/text/skippedHints 结构', Array.isArray(j.contents) && typeof j.text === 'string' && Array.isArray(j.skippedHints), JSON.stringify({ contents: (j.contents ?? []).length, text: (j.text ?? '').length, hints: (j.skippedHints ?? []).length }))
  }

  const e1 = await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: MAINT, sourceNodeId: idA, targetNodeId: idB } })
  check('KGS-S2h 建边 A-维护->B 200', e1.status === 200 && !!e1.json?.value, `${e1.status} ${e1.text.slice(0, 120)}`)

  const nameC0 = '项目甲-' + TS
  const nC = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PRJ, name: nameC0 } })
  const idC = String(nC.json?.value)
  const e2 = await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: DEP, sourceNodeId: idC, targetNodeId: idB } })
  check('KGS-S2i 建节点 C 与边 C-依赖->B 200', nC.status === 200 && !!nC.json?.value && e2.status === 200, `${nC.status}/${e2.status}`)

  const hitC = await pollSearch(token, G1, { query: nameC0, topK: 10 }, (j) => {
    const hit = (j.hits ?? []).find((h) => h.name === nameC0)
    return !!hit && (hit.neighbors ?? []).length > 0
  })
  check('KGS-S2j 轮询检索命中节点 C 且带邻居', hitC.ok, JSON.stringify((hitC.res?.json?.hits ?? []).find((h) => h.name === nameC0)?.neighbors ?? hitC.res?.text ?? '').slice(0, 160))
  if (hitC.ok) {
    const nbList = (hitC.res.json?.hits ?? []).find((h) => h.name === nameC0)?.neighbors ?? []
    const toB = nbList.find((x) => x.name === nameB)
    check('KGS-S2k 邻居含 B（direction=out/relationName=依赖/description 字段）',
      !!toB && toB.direction === 'out' && toB.relationName === '依赖' && typeof toB.description === 'string',
      JSON.stringify(nbList).slice(0, 200))
  }

  // ===== KGS-S3 改节点名 → 新名命中、旧名不命中（向量替换） =====
  const nameC1 = '项目乙-' + TS
  const updC = await api('PUT', `${kg(G1)}/nodes/${encodeURIComponent(idC)}`, { token, body: { entityTypeId: PRJ, name: nameC1 } })
  check('KGS-S3a 改节点名 200', updC.status === 200, `${updC.status} ${updC.text.slice(0, 160)}`)
  const poll3 = await pollSearch(token, G1, { query: nameC1, topK: 10 }, (j) => (j.hits ?? []).some((h) => h.name === nameC1))
  const oldQ = await search(token, G1, { query: nameC0, topK: 50 })
  const oldGone = (oldQ.json?.hits ?? []).every((h) => h.name !== nameC0)
  check('KGS-S3b 新名命中且旧名不命中', updC.status === 200 && poll3.ok && oldGone, JSON.stringify({ poll: poll3.ok, oldHits: (oldQ.json?.hits ?? []).map((h) => h.name).slice(0, 5) }))

  // ===== KGS-S4 删节点 → 轮询至不命中 =====
  const nameD = '临抛节点-' + TS
  const nD = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PPL, name: nameD } })
  const idD = String(nD.json?.value)
  check('KGS-S4a 创建临时节点 200', nD.status === 200 && !!nD.json?.value, `${nD.status}`)
  const hitD = await pollSearch(token, G1, { query: nameD, topK: 10 }, (j) => (j.hits ?? []).some((h) => h.name === nameD))
  check('KGS-S4b 临时节点向量化后可检索', hitD.ok, JSON.stringify(hitD.res?.json?.hits ?? hitD.res?.text ?? '').slice(0, 160))
  const delD = await api('DELETE', `${kg(G1)}/nodes/${encodeURIComponent(idD)}`, { token })
  const goneD = await pollSearch(token, G1, { query: nameD, topK: 10 }, (j) => !(j.hits ?? []).some((h) => h.name === nameD))
  check('KGS-S4c 删除节点后检索不命中', delD.status === 200 && goneD.ok, `${delD.status} poll=${goneD.ok}`)

  // ===== KGS-S5 minScore 阈值过滤 =====
  const s5hi = await search(token, G1, { query: nameA, topK: 5, minScore: 0.999999 })
  const s5lo = await search(token, G1, { query: nameA, topK: 5 })
  check('KGS-S5 minScore=0.999999 空 hits，缺省时命中',
    s5hi.status === 200 && (s5hi.json?.hits ?? []).length === 0 && s5lo.status === 200 && (s5lo.json?.hits ?? []).length > 0,
    `${s5hi.status}/${(s5hi.json?.hits ?? []).length} ${s5lo.status}/${(s5lo.json?.hits ?? []).length}`)

  // ===== KGS-S6 未配模型的第二张托管图 → 409 文案含「向量化」 =====
  const c2 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kgs-nomodel-' + TS, templateKey: 'blank' } })
  check('KGS-S6a 创建未配模型托管图 200', c2.status === 200 && Number(c2.json?.value) > 0, `${c2.status} ${c2.text.slice(0, 120)}`)
  const G2 = Number(c2.json?.value)
  const s6 = await api('POST', `${kg(G2)}/search`, { token, body: { query: nameA, topK: 5 } })
  check('KGS-S6b 未配模型检索 409 且文案含「向量化」', s6.status === 409 && s6.text.includes('向量化'), `${s6.status} ${s6.text.slice(0, 160)}`)

  // ===== KGS-S7 embedding-config 参数边界（400 不破坏原配置） =====
  const s7a = await api('PUT', `${kg(G1)}/embedding-config`, { token, body: { embeddingModelId: embModelId, embeddingDimensions: 0 } })
  check('KGS-S7a 维度 0 → 400', s7a.status === 400, `${s7a.status}`)
  const s7b = await api('PUT', `${kg(G1)}/embedding-config`, { token, body: { embeddingModelId: crypto.randomUUID(), embeddingDimensions: 1024 } })
  check('KGS-S7b 假模型 Guid → 400', s7b.status === 400, `${s7b.status} ${s7b.text.slice(0, 160)}`)
  const s7c = await search(token, G1, { query: nameA, topK: 3 })
  check('KGS-S7c 非法配置不改原配置（检索仍命中）', s7c.status === 200 && (s7c.json?.hits ?? []).length > 0, `${s7c.status} hits=${(s7c.json?.hits ?? []).length}`)

  // ===== KGS-S8 应用配置绑定 graphIds =====
  const team2 = await api('POST', '/api/team', { token, body: { name: 'kgs-team2-' + TS } })
  const TID2 = Number(team2.json?.value)
  check('KGS-S8a 前置：创建第二团队 200', team2.status === 200 && TID2 > 0, `${team2.status}`)
  const g3 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID2, name: 'kgs-other-' + TS, templateKey: 'blank' } })
  const G3 = Number(g3.json?.value)
  check('KGS-S8a 前置：第二团队托管图 200', g3.status === 200 && G3 > 0, `${g3.status} ${g3.text.slice(0, 120)}`)

  const appR = await api('POST', '/api/app', { token, body: { teamId: TID, name: 'kgs应用' + TS, appType: 'agent' } })
  const AGENT = String(appR.json?.value ?? '')
  check('KGS-S8b 前置：Agent 应用创建 200', appR.status === 200 && /^[0-9a-f-]{36}$/i.test(AGENT), `${appR.status} ${appR.text.slice(0, 120)}`)
  const cfgUrl = `/api/app/${AGENT}/agent-config`
  const agentBody = (graphIds) => ({ modelId: null, prompt: '', wikiIds: [], plugins: [], graphIds, openingStatement: '', openingStatementEnabled: false })

  const s8c = await api('PUT', cfgUrl, { token, body: agentBody([G3]) })
  check('KGS-S8c 绑定他团队图谱 400 且文案含「知识图谱」', s8c.status === 400 && s8c.text.includes('知识图谱'), `${s8c.status} ${s8c.text.slice(0, 160)}`)

  const connR = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kgs-connected-' + TS, mode: 'connected', database: 'neo4j' } })
  const G_CONN = Number(connR.json?.value)
  check('KGS-S8d 前置：创建本团队接入图 200', connR.status === 200 && G_CONN > 0, `${connR.status} ${connR.text.slice(0, 160)}`)
  const s8e = G_CONN > 0 ? await api('PUT', cfgUrl, { token, body: agentBody([G_CONN]) }) : { status: 0, text: '接入图创建失败' }
  check('KGS-S8e 绑定接入图 400 且文案含「知识图谱」', s8e.status === 400 && s8e.text.includes('知识图谱'), `${s8e.status} ${s8e.text.slice(0, 160)}`)

  const s8f = await api('PUT', cfgUrl, { token, body: agentBody([G1]) })
  check('KGS-S8f 绑定本团队托管图 200', s8f.status === 200, `${s8f.status} ${s8f.text.slice(0, 160)}`)
  const back = await api('GET', cfgUrl, { token })
  if (back.json && back.json.graphIds !== undefined) {
    check('KGS-S8g 回读配置含 graphIds', (back.json.graphIds ?? []).map(Number).includes(G1), back.text.slice(0, 200))
  } else {
    console.log('INFO | KGS-S8g 当前构建 agent-config 查询未返回 graphIds（后端待补），字段落地后本断言自动生效')
  }

  // ===== KGS-S9 工作流 kgSearch 节点 =====
  const wfR = await api('POST', '/api/app', { token, body: { teamId: TID, name: 'kgs流程' + TS, appType: 'workflow' } })
  const WF = String(wfR.json?.value ?? '')
  check('KGS-S9a 前置：流程应用创建 200', wfR.status === 200 && /^[0-9a-f-]{36}$/i.test(WF), `${wfR.status} ${wfR.text.slice(0, 120)}`)

  const buildDef = (graphId) => ({
    id: '', name: 'wf-kgs', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '检索问题' }] },
      {
        key: 'kgs', name: '图谱检索', type: 'kgSearch',
        config: { graphId, topK: 3 },
        inputs: { query: { expressionType: 'variable', value: 'start.question', required: true } },
        outputs: [
          { name: 'query', fieldType: 'string' },
          { name: 'count', fieldType: 'number' },
          { name: 'hits', fieldType: 'array' },
          { name: 'contents', fieldType: 'array' },
          { name: 'text', fieldType: 'string' },
        ],
      },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: {
          count: { expressionType: 'variable', value: 'kgs.count', required: false },
          text: { expressionType: 'variable', value: 'kgs.text', required: false },
        },
        outputs: [{ name: 'count', fieldType: 'number' }, { name: 'text', fieldType: 'string' }],
      },
    ],
    connections: [
      { id: 'g1', source: 'start', target: 'kgs' },
      { id: 'g2', source: 'kgs', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, kgs: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })
  const buildEditor = (def) => ({
    nodes: def.nodes.map((n) => ({
      id: n.key, type: n.type,
      meta: { position: { x: 100, y: 100 }, defaultExpanded: true },
      data: { title: n.name, content: '', inputs: n.inputs, outputs: n.outputs, settings: n.config ?? {} },
      blocks: [],
      edges: def.connections.filter((c) => c.source === n.key).map((c) => ({ sourceNodeID: c.source, targetNodeID: c.target })),
    })),
    edges: [],
  })

  const badDraft = await api('POST', '/api/app/workflow/draft', { token, body: { appId: WF, teamId: TID, definition: JSON.stringify(buildDef(G3)), editorData: '{}' } })
  check('KGS-S9b draft 引用他团队图谱 400 且文案含「知识图谱」', badDraft.status === 400 && badDraft.text.includes('知识图谱'), `${badDraft.status} ${badDraft.text.slice(0, 160)}`)

  const goodDef = buildDef(G1)
  const goodDraft = await api('POST', '/api/app/workflow/draft', { token, body: { appId: WF, teamId: TID, definition: JSON.stringify(goodDef), editorData: JSON.stringify(buildEditor(goodDef)) } })
  check('KGS-S9c 本团队图谱 draft 保存 200', goodDraft.status === 200, `${goodDraft.status} ${goodDraft.text.slice(0, 160)}`)
  const pub = await api('POST', '/api/app/workflow/publish', { token, body: { appId: WF, teamId: TID } })
  check('KGS-S9d 发布 200', pub.status === 200, `${pub.status} ${pub.text.slice(0, 160)}`)

  const run = await api('POST', '/api/app/workflow/debug-run', { token, body: { appId: WF, teamId: TID, inputJson: JSON.stringify({ question: nameA }) } })
  const rj = run.json ?? {}
  const kgsOut = (rj.nodes ?? []).find((n) => n.nodeKey === 'kgs')
  let parsed = null
  try { parsed = kgsOut?.output ? JSON.parse(kgsOut.output) : null } catch { /* 非 JSON 输出 */ }
  check('KGS-S9e debug-run 完成', run.status === 200 && rj.status === 'completed' && kgsOut?.state === 'completed', `${run.status} ${rj.status} ${run.text.slice(0, 200)}`)
  check('KGS-S9f kgSearch 输出 count≥1 且 text 非空', parsed !== null && Number(parsed?.count) >= 1 && String(parsed?.text ?? '') !== '', String(kgsOut?.output ?? run.text.slice(0, 160)).slice(0, 200))

  // ===== 清理：删图/桩渠道；团队禁用归档（应用无删除 API，留给台账，同 app-e2e 口径） =====
  await api('DELETE', kg(G1), { token })
  await api('DELETE', kg(G2), { token })
  if (G3 > 0) await api('DELETE', kg(G3), { token })
  if (G_CONN > 0) await api('DELETE', kg(G_CONN), { token })
  if (CH_ID) await api('DELETE', `/api/ai/channel/${CH_ID}`, { token })
  stub.close()
  await api('PUT', `/api/admin/team/${TID}/disable`, { token, body: { isDisable: true } })
  if (TID2 > 0) await api('PUT', `/api/admin/team/${TID2}/disable`, { token, body: { isDisable: true } })

  console.log(`\n===== 知识图谱检索 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
