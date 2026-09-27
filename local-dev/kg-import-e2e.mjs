// 知识图谱 AI 导入文件 + JSON 结构化导入 E2E（场景 @KG-S26、@KG-S29；后端默认 127.0.0.1:5000）
// 覆盖：模板图建图 → model-options（团队对话模型）→ 文件三段直传（chat 公共目录）→ AI 导入（本地 OpenAI 兼容桩返回固定抽取 JSON）
//       → 图库写入断言（节点/边/属性）→ 非法 objectKey 400 → 空白模板图（无实体类型）导入 409。
//       KG-S29：JSON 结构化导入（/import-json，与外部 /import 同管线）——类型名引用+autoCreateTypes、业务 key upsert、
//       端点按 key/名称引用、逐条失败报告、预检 validateOnly 不落库、非法 JSON/超限 400、重复导入幂等。
// 桩模式与 wiki-recall / bocha E2E 一致，无需真实模型渠道；前置依赖：后端 + 图数据库可达且 KG_ENABLED=true。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱导入 E2E 未执行: ${reason}`)
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
  try { json = JSON.parse(text) } catch { /* */ }
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
// 桩返回固定抽取 JSON：与物流模板模型匹配（1 港口 + 1 航段 + 1 抵达边），节点带属性
const STUB_REPLY = JSON.stringify({
  nodes: [
    { name: '青岛港', entityType: '港口', description: '桩导入的港口', properties: { 港口代码: 'CNTAO', 国家区域: '中国' } },
    { name: '青岛 → 上海', entityType: '航段', properties: { 运输方式: '海运', 距离公里: '700', 运输价格: '2100', 时效天: '1' } },
  ],
  edges: [{ source: '青岛 → 上海', target: '青岛港', relationType: '出发' }],
})

// 确定性哈希向量：同词文本向量相近（按 3-gram 哈希落桶），保证近似名称相似度达标
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

function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      if (String(req.url).includes('/embeddings')) {
        let payload = {}
        try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
        const inputs = Array.isArray(payload.input) ? payload.input : [String(payload.input ?? '')]
        const dims = Math.min(Number(payload.dimensions) || 1024, 1024)
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          object: 'list', model: payload.model ?? 'stub-embedding',
          data: inputs.map((text, index) => ({ object: 'embedding', index, embedding: stubEmbed(text, dims) })),
          usage: { prompt_tokens: 10, total_tokens: 10 },
        }))
        return
      }
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        id: 'chatcmpl-stub',
        object: 'chat.completion',
        created: Math.floor(Date.now() / 1000),
        model: 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: STUB_REPLY }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

const si = await api('GET', '/api/common/serverinfo')
RSA_KEY = si.json.rsaPublic
const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
if (login.status !== 200 || !login.json?.accessToken) throw new Error(`root 登录失败: ${login.status}`)
const token = login.json.accessToken

// 能力与图数据库探活：建一个模板图后查 schema 即证明可用；此处用 list.enabled 快速短路
const team = await api('POST', '/api/team', { token, body: { name: 'kg-imp-team-' + TS } })
const TID = Number(team.json?.value)
if (!Number.isFinite(TID)) throw new Error('创建团队失败')
const list0 = await api('GET', `/api/knowledge-graph/list?teamId=${TID}`, { token })
if (list0.json?.enabled !== true) skip(`list.enabled=${list0.json?.enabled}`)

const stub = await startStubServer()
const stubPort = stub.address().port
let channelId = ''
const cleanup = async () => {
  if (channelId) await api('DELETE', `/api/ai/channel/${channelId}`, { token })
  stub.close()
}
try {
  // 建物流模板图（自带模型与预置数据，供导入校验目标类型）
  const graph = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-imp-' + TS, templateKey: 'logistics' } })
  const GID = Number(graph.json?.value)
  check('KG-S26a 创建物流模板图 200', graph.status === 200 && GID > 0, `${graph.status}`)

  // 桩渠道 + 对话模型 + 授权团队
  const chName = `kg-imp-stub渠道-${TS}`
  const crc = await api('POST', '/api/ai/channel', { token, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'kg-imp-stub-key', enabled: true, description: 'kg-import E2E 桩渠道' } })
  const channels = await api('GET', '/api/ai/channel', { token })
  channelId = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const mrc = await api('POST', '/api/ai/model', { token, body: { channelId, meta: { modelId: 'kg-imp-stub-chat', name: `kg-imp-stub-chat-${TS}`, modelKind: 'conversation', description: 'kg-import E2E 桩模型' }, enabled: true, isPublic: false } })
  const models = await api('GET', `/api/ai/model?channelId=${channelId}`, { token })
  const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === `kg-imp-stub-chat-${TS}`)?.id ?? '')
  if (channelId && MODEL_ID) {
    const cur = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token, body: { modelId: MODEL_ID, teamIds } })
  }
  const mo = await api('GET', `/api/knowledge-graph/model-options?teamId=${TID}`, { token })
  check('KG-S26b model-options 含桩对话模型', mo.status === 200 && (mo.json?.conversationModels ?? []).some((m) => m.id === MODEL_ID), JSON.stringify(mo.json).slice(0, 160))

  // 非法 objectKey（非 public/chat/ 前缀）→ 400
  const bad = await api('POST', `/api/knowledge-graph/${GID}/import-file`, { token, body: { objectKey: 'wiki/doc/evil.txt', fileName: 'evil.txt', aiModelId: MODEL_ID } })
  check('KG-S26c 非法 objectKey 400', bad.status === 400, `${bad.status} ${bad.text.slice(0, 120)}`)

  // 直传 txt（chat 公共目录三段直传）
  const content = Buffer.from('青岛港位于中国，港口代码 CNTAO。青岛到上海航段 700 公里，运价 2100 元。', 'utf8')
  const sha = crypto.createHash('sha256').update(content).digest('hex')
  const pre = await api('POST', '/api/storage/public/pre_upload_chat_file', { token, body: { fileName: `kg-imp-${TS}.txt`, contentType: 'text/plain', fileSize: content.length, shA256: sha } })
  if (pre.status !== 200 || (!pre.json?.uploadUrl && !pre.json?.isExist)) skip(`文件预上传失败 ${pre.status}`)
  if (!pre.json?.isExist) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/plain' }, body: content })
    if (!put.ok) skip(`文件直传失败 ${put.status}`)
    await api('POST', '/api/storage/complate_url', { token, body: { fileId: pre.json.fileId, isSuccess: true } })
  }

  // AI 导入
  const imp = await api('POST', `/api/knowledge-graph/${GID}/import-file`, { token, body: { objectKey: pre.json.objectKey, fileName: `kg-imp-${TS}.txt`, aiModelId: MODEL_ID } })
  check('KG-S26d AI 导入 200 且新增 2 节点 1 边', imp.status === 200 && Number(imp.json?.nodesCreated) === 2 && Number(imp.json?.edgesCreated) === 1, `${imp.status} ${imp.text.slice(0, 200)}`)

  // 画布断言：模板 11 + 导入 2 = 13 节点；15 + 1 = 16 边
  const cv = await api('POST', `/api/knowledge-graph/${GID}/canvas`, { token, body: { limit: 200 } })
  const nodes = cv.json?.nodes ?? []
  check('KG-S26e 画布 13 节点 16 边', cv.status === 200 && nodes.length === 13 && (cv.json?.edges ?? []).length === 16, JSON.stringify({ n: nodes.length, e: cv.json?.edges?.length }))

  // 导入的航段属性可读
  const leg = nodes.find((n) => n.name === '青岛 → 上海')
  check('KG-S26f 导入航段带距离/价格属性', !!leg && leg.properties?.['距离公里'] === '700' && leg.properties?.['运输价格'] === '2100', JSON.stringify(leg?.properties))

  // 空白模板图（无实体类型）导入 → 409
  const blank = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-imp-blank-' + TS, templateKey: 'blank' } })
  const BGID = Number(blank.json?.value)
  const impBlank = await api('POST', `/api/knowledge-graph/${BGID}/import-file`, { token, body: { objectKey: pre.json.objectKey, fileName: `kg-imp-${TS}.txt`, aiModelId: MODEL_ID } })
  check('KG-S26g 空白模板图导入 409（无实体类型）', impBlank.status === 409, `${impBlank.status} ${impBlank.text.slice(0, 120)}`)

  // ===== KG-S29 JSON 结构化导入（/import-json，与外部 /import 同管线）=====
  {
    const jg = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-imp-json-' + TS, templateKey: 'blank' } })
    const JGID = Number(jg.json?.value)
    if (!Number.isFinite(JGID)) throw new Error('KG-S29 建图失败')

    // 向量化准备：桩 embedding 模型 + 授权 + 图 embedding-config（疑似重复检测与节点向量增量都依赖它）
    const embName = `kg-imp-stub-embed-${TS}`
    let EMB_ID = ''
    {
      const mrc = await api('POST', '/api/ai/model', { token, body: { channelId, meta: { modelId: embName, name: embName, modelKind: 'embedding', description: 'kg-import E2E 桩向量模型' }, enabled: true, isPublic: false } })
      const models = await api('GET', `/api/ai/model?channelId=${channelId}`, { token })
      EMB_ID = String((models.json?.items ?? []).find((m) => m.name === embName)?.id ?? '')
      if (EMB_ID) {
        const cur = await api('GET', `/api/ai/model/${EMB_ID}/authorization`, { token })
        const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
        await api('PUT', `/api/ai/model/${EMB_ID}/authorization`, { token, body: { modelId: EMB_ID, teamIds } })
      }
      const bind = EMB_ID ? await api('PUT', `/api/knowledge-graph/${JGID}/embedding-config`, { token, body: { embeddingModelId: EMB_ID, embeddingDimensions: 1024 } }) : { status: 0 }
      if (bind.status !== 200) console.log(`INFO | embedding-config 绑定失败 ${bind.status}，疑似重复正向用例将跳过`)
    }

    const jsonPayload = {
      nodes: [
        { key: 'json-p-1', entityTypeName: 'JSON人员', name: 'JSON张三-' + TS, description: 'JSON 导入', properties: { 城市: '深圳', 级别: 'P7' } },
        { key: 'json-c-1', entityTypeName: 'JSON公司', name: 'JSONAcme-' + TS },
        { entityTypeId: 999999, name: 'JSON坏类型行-' + TS },
        { key: 'json-p-1', entityTypeName: 'JSON人员', name: 'JSON重复key-' + TS },
      ],
      edges: [
        { relationTypeName: 'JSON任职', source: { key: 'json-p-1' }, target: { key: 'json-c-1' } },
        { relationTypeName: 'JSON任职', source: { name: 'JSON张三-' + TS, entityTypeName: 'JSON人员' }, target: { name: 'JSONAcme-' + TS, entityTypeName: 'JSON公司' } },
        { relationTypeName: 'JSON任职', source: { key: 'json-none' }, target: { key: 'json-c-1' } },
      ],
    }

    // a) 非法 JSON 400
    const badJson = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: '{ nodes: [缩进错误' } })
    check('KG-S29a 非法 JSON 400 且消息含定位', badJson.status === 400 && badJson.text.includes('JSON 解析失败'), `${badJson.status} ${badJson.text.slice(0, 140)}`)

    // b) nodes/edges 双空 400
    const emptyPayload = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: '{"nodes":[],"edges":[]}' } })
    check('KG-S29b nodes/edges 双空 400', emptyPayload.status === 400 && emptyPayload.text.includes('不能同时为空'), `${emptyPayload.status} ${emptyPayload.text.slice(0, 140)}`)

    // c) 预检 validateOnly：预测计数 + 不落库（schema 无新类型、节点列表 0）
    const preview = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ ...jsonPayload, validateOnly: true, autoCreateTypes: true }) } })
    check('KG-S29c 预检 200 预测：节点 created=2 failed=2 / 边 created=1 skipped=1 failed=1', preview.status === 200
      && preview.json?.nodeCreatedCount === 2 && preview.json?.nodeFailedCount === 2
      && preview.json?.edgeCreatedCount === 1 && preview.json?.edgeSkippedCount === 1 && preview.json?.edgeFailedCount === 1, `${preview.status} ${preview.text.slice(0, 240)}`)
    const schemaPre = await api('GET', `/api/knowledge-graph/${JGID}/schema`, { token })
    check('KG-S29d 预检不建类型（schema 仅 blank 模板类型）', (schemaPre.json?.entityTypes ?? []).every((x) => !String(x.name).startsWith('JSON')), JSON.stringify((schemaPre.json?.entityTypes ?? []).map((x) => x.name)))

    // d) 正式导入：类型自动创建 + 节点/边落库 + 坏行逐条报告
    const imp = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ ...jsonPayload, autoCreateTypes: true }) } })
    check('KG-S29e 导入 200：节点 created=2 failed=2 / 边 created=1 skipped=1 failed=1（等价边幂等跳过）', imp.status === 200
      && imp.json?.nodeCreatedCount === 2 && imp.json?.nodeFailedCount === 2
      && imp.json?.edgeCreatedCount === 1 && imp.json?.edgeSkippedCount === 1 && imp.json?.edgeFailedCount === 1, `${imp.status} ${imp.text.slice(0, 240)}`)
    check('KG-S29f 自动创建类型回显（2 实体类型 + 1 关系类型）', (imp.json?.createdEntityTypeNames ?? []).includes('JSON人员')
      && (imp.json?.createdEntityTypeNames ?? []).includes('JSON公司') && (imp.json?.createdRelationTypeNames ?? []).includes('JSON任职'), JSON.stringify(imp.json?.createdEntityTypeNames))
    const nodeResults = imp.json?.results?.filter((x) => x.kind === 'node') ?? []
    check('KG-S29g 坏行逐条报告（类型不存在/重复 key）', nodeResults[2]?.ok === false && (nodeResults[2]?.message ?? '').includes('实体类型')
      && nodeResults[3]?.ok === false && (nodeResults[3]?.message ?? '').includes('key'), JSON.stringify(nodeResults))

    // e) 重复导入（页面控件覆盖：mode=upsert）幂等：updated=1 / 边 skipped=1
    const reImp = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ ...jsonPayload, autoCreateTypes: true, mode: 'create' }), mode: 'upsert' } })
    check('KG-S29h 重复导入幂等（控件 mode=upsert 覆盖）：节点 updated=2 / 边 skipped=2', reImp.status === 200
      && reImp.json?.nodeUpdatedCount === 2 && reImp.json?.nodeCreatedCount === 0 && reImp.json?.edgeSkippedCount === 2, `${reImp.status} ${reImp.text.slice(0, 240)}`)

    // f) 接入图不可在此验证（模板 managed）；补：超限 501 节点 400
    const tooMany = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ nodes: Array.from({ length: 501 }, (_, i) => ({ entityTypeName: 't', name: `超限-${i}` })) }) } })
    check('KG-S29i 501 节点超限 400', tooMany.status === 400 && tooMany.text.includes('500'), `${tooMany.status} ${tooMany.text.slice(0, 140)}`)

    // ===== KG-S29j~m 疑似重复检测（向量相似度；依赖 embedding-config 已绑定 + 既有节点已向量入库）=====
    if (EMB_ID) {
      // j) 轮询等 e) 导入的 JSON张三 向量入库（节点写入 → MQ 增量 → pgvector）
      let vectorReady = false
      for (let i = 0; i < 30 && !vectorReady; i++) {
        const sr = await api('POST', `/api/knowledge-graph/${JGID}/search`, { token, body: { query: 'JSON张三-' + TS, topK: 5 } })
        vectorReady = sr.status === 200 && (sr.json?.hits ?? []).some((h) => h.name === 'JSON张三-' + TS)
        if (!vectorReady) await new Promise((resolve) => setTimeout(resolve, 2000))
      }
      check('KG-S29j 既有节点向量就绪（search 命中）', vectorReady, '')

      if (vectorReady) {
        // k) 同名不同类型（不触发 upsert 匹配）新建 → duplicateSuspects 命中图谱已有同名节点（向量化文本全同 score=1.0）
        const nearName = 'JSONAcme-' + TS
        const nearDup = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ nodes: [{ key: 'json-p-9', entityTypeName: 'JSON人员', name: nearName }] }) } })
        const nearSuspects = nearDup.json?.duplicateSuspects ?? []
        const existingHit = nearSuspects.find((x) => x.kind === 'existing' && x.matchName === nearName)
        check('KG-S29k 近似名导入报疑似重复（对图谱已有节点，score≥0.85）', nearDup.status === 200 && nearDup.json?.nodeCreatedCount === 1 && !!existingHit && Number(existingHit.score) >= 0.85 && !!existingHit.matchNodeId, `${nearDup.status} ${JSON.stringify(nearSuspects).slice(0, 240)}`)

        // l) 同批两行近似名（不同类型不互相 upsert）→ inbatch 疑似重复
        const inBatch = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ autoCreateTypes: true, nodes: [
          { entityTypeName: 'JSON批内A-' + TS, name: '批内样本-' + TS },
          { entityTypeName: 'JSON批内B-' + TS, name: '批内样本-' + TS },
        ] }) } })
        const inBatchSuspects = inBatch.json?.duplicateSuspects ?? []
        check('KG-S29l 同批近似名报 inbatch 疑似重复', inBatch.status === 200 && inBatch.json?.nodeCreatedCount === 2 && inBatchSuspects.some((x) => x.kind === 'inbatch' && x.matchIndex != null), `${inBatch.status} ${JSON.stringify(inBatchSuspects).slice(0, 240)}`)

        // m) detectDuplicates=false 关闭检测
        const off = await api('POST', `/api/knowledge-graph/${JGID}/import-json`, { token, body: { content: JSON.stringify({ nodes: [{ key: 'json-p-10', entityTypeName: 'JSON人员', name: 'JSON张三-' + TS + '-3' }] }), detectDuplicates: false } })
        check('KG-S29m 关闭检测时无疑似重复结果', off.status === 200 && (off.json?.duplicateSuspects ?? []).length === 0, `${off.status} ${JSON.stringify(off.json?.duplicateSuspects)}`)
      } else {
        console.warn('WARN | KG-S29k~m 跳过：向量未就绪')
      }
    } else {
      console.warn('WARN | KG-S29j~m 跳过：embedding 模型链路不可用')
    }
  }
} finally {
  await cleanup()
}

console.log(`\n===== 知识图谱 AI 导入 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
process.exit(FAIL > 0 ? 1 : 0)
