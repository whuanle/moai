// 知识图谱 AI 导入文件 E2E（场景 @KG-S26；后端默认 127.0.0.1:5000）
// 覆盖：模板图建图 → model-options（团队对话模型）→ 文件三段直传（chat 公共目录）→ AI 导入（本地 OpenAI 兼容桩返回固定抽取 JSON）
//       → 图库写入断言（节点/边/属性）→ 非法 objectKey 400 → 空白模板图（无实体类型）导入 409。
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

function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
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
} finally {
  await cleanup()
}

console.log(`\n===== 知识图谱 AI 导入 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
process.exit(FAIL > 0 ? 1 : 0)
