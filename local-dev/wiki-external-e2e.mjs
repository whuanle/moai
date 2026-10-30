// 知识库外部接口 E2E（真实 HTTP；复用 kg-external-e2e / wiki-embedding-e2e 同款 helper；后端默认 127.0.0.1:5210）
// 场景编号 WX-01..WX-11（新编号，不复用 WK-S* / KX-*）：/api/external/wiki 仅接受应用 token（团队级授权）
//   WX-08 原文件下载（预签名 URL 直下字节一致）；WX-09 向量召回（recall/list 读档分档）；
//   WX-10 切片元数据生成（对话桩模型，全量/策略/追加/指定切片）；
//   WX-11 允许上传文件类型设置（WIKI_ALLOWED_FILE_TYPES：默认回显/保存格式校验/收窄门禁/放开 .sh 纯文本兜底提取）
// 用法：node local-dev/wiki-external-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5210）
// 前置：后端 + MinIO + RabbitMQ 可达。
// 向量化模型：脚本自带本地 embedding 桩服务（同 bocha/paddleocr e2e 的自建桩口径），经管理接口临时登记
//   渠道+embedding 模型并授权给本次团队（同 app-e2e AP-20 口径），WX-05 真实走任务队列→桩服务→pgvector
//   入库全链路，不依赖外部模型可用性；同一桩服务兼答 /chat/completions（WX-10 元数据生成对话桩，
//   固定返回 outline/questions×2/keywords×2/summary/aggregated JSON，断言条数确定）；清理阶段删除模型/渠道并关闭桩服务，可重复执行。
// 说明：内部准备全部用 admin（不依赖普通用户的成员角色语义）；共享开发库偶发 500（并行任务 DDL 竞争），
//   准备阶段对 500 做有限重试。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
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
// 共享开发库偶发 500（并行任务的 DDL/迁移竞争），准备阶段对 500 做有限重试
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
const sha256Hex = (content) => crypto.createHash('sha256').update(content).digest('hex')
const EMBEDDING_DIMENSIONS = 1024

// 本地桩服务：/v1/embeddings 返回确定性的 dims 维伪向量，每次延迟 DELAY_MS（保证 WX-05 有确定的「进行中」
// 窗口）；/v1/chat/completions 服务 WX-10 元数据生成，固定返回 outline+questions×2+keywords×2+summary+aggregated
// 的 JSON 内容（每个切片恰好生成 7 条元数据，断言条数确定），无延迟。
const STUB_DELAY_MS = 3000
const METADATA_STUB_CONTENT = JSON.stringify({
  outline: '外部接口测试切片大纲',
  questions: ['外部接口能上传文档吗？', '外部接口能向量化吗？'],
  keywords: ['外部接口', '知识库'],
  summary: '外部接口全链路测试摘要',
  aggregated: '外部接口语义聚合段',
})
function startEmbeddingStub() {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      let body = ''
      req.on('data', (chunk) => { body += chunk })
      req.on('end', () => {
        if ((req.url ?? '').includes('chat/completions')) {
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify({
            id: 'wx-chat-mock', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: 'wx-metadata-mock',
            choices: [{ index: 0, message: { role: 'assistant', content: METADATA_STUB_CONTENT }, finish_reason: 'stop' }],
            usage: { prompt_tokens: 1, completion_tokens: 1, total_tokens: 2 },
          }))
          return
        }
        let inputs = ['']
        let dims = EMBEDDING_DIMENSIONS
        try {
          const parsed = JSON.parse(body || '{}')
          if (Array.isArray(parsed.input)) inputs = parsed.input
          else if (parsed.input !== undefined) inputs = [String(parsed.input)]
          if (Number.isFinite(parsed.dimensions) && parsed.dimensions > 0) dims = parsed.dimensions
        } catch { /* 保持默认 */ }
        const make = (text, index) => ({ object: 'embedding', index, embedding: Array.from({ length: dims }, (_, j) => (((index + j + String(text).length) % 97) / 97) - 0.5) })
        setTimeout(() => {
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify({ object: 'list', data: inputs.map(make), model: 'wx-embedding-mock', usage: { prompt_tokens: 1, total_tokens: 1 } }))
        }, STUB_DELAY_MS)
      })
    })
    server.on('error', reject)
    server.listen(0, '127.0.0.1', () => resolve(server))
  })
}

const TS = Date.now().toString().slice(-8)
const xw = (wikiId, suffix = '') => `/api/external/wiki/${wikiId}${suffix}`

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) {
    throw new Error(`admin 登录失败: ${login.status} ${login.text.slice(0, 120)}`)
  }
  const token = login.json.accessToken

  // ===== 准备：admin 建两个团队各一个知识库；各建一个接入点换应用 token =====
  const T1 = Number((await retryApi('POST', '/api/team', { name: 'wx-team1-' + TS }, token)).json?.value)
  const T2 = Number((await retryApi('POST', '/api/team', { name: 'wx-team2-' + TS }, token)).json?.value)
  if (!Number.isFinite(T1) || !Number.isFinite(T2)) throw new Error('创建团队失败')
  const w1res = await retryApi('POST', '/api/wiki', { teamId: T1, name: 'wx-wiki1-' + TS, description: 'wiki external e2e' }, token)
  const w2res = await retryApi('POST', '/api/wiki', { teamId: T2, name: 'wx-wiki2-' + TS, description: 'wiki external e2e' }, token)
  if (w1res.status !== 200 || w2res.status !== 200) throw new Error(`建知识库失败: ${w1res.status}/${w2res.status} ${w1res.text.slice(0, 120)}`)
  const W1 = Number(w1res.json?.value)
  const W2 = Number(w2res.json?.value)

  const acc1 = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'wx接入1-' + TS, description: 'wiki external e2e' }, token)
  const acc2 = await retryApi('POST', '/api/access-app', { teamId: T2, name: 'wx接入2-' + TS, description: 'wiki external e2e' }, token)
  if (acc1.status !== 200 || acc2.status !== 200) throw new Error(`创建接入点失败: ${acc1.status}/${acc2.status}`)
  const KEY1 = acc1.json?.key
  const KEY2 = acc2.json?.key
  const ACC1 = acc1.json?.accessAppId
  const ACC2 = acc2.json?.accessAppId

  const tok1 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY1, secret: acc1.json?.secret } })
  const tok2 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY2, secret: acc2.json?.secret } })
  const AT1 = tok1.json?.accessToken
  const AT2 = tok2.json?.accessToken

  // WX-09f 读档分档证明：只读 key（仅 wiki_read）与只写 key（仅 wiki_write）
  let ACC_RO = 0, ACC_WO = 0, AT1_RO = '', AT1_WO = ''
  {
    const accRO = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'wx接入只读-' + TS, description: 'wiki external e2e', scopes: ['wiki_read'] }, token)
    const accWO = await retryApi('POST', '/api/access-app', { teamId: T1, name: 'wx接入只写-' + TS, description: 'wiki external e2e', scopes: ['wiki_write'] }, token)
    ACC_RO = accRO.json?.accessAppId ?? 0
    ACC_WO = accWO.json?.accessAppId ?? 0
    AT1_RO = (await api('POST', '/api/external/token', { body: { accessAppKey: accRO.json?.key, secret: accRO.json?.secret } })).json?.accessToken
    AT1_WO = (await api('POST', '/api/external/token', { body: { accessAppKey: accWO.json?.key, secret: accWO.json?.secret } })).json?.accessToken
  }

  // ===== 向量化模型准备：本地桩渠道 + embedding 模型，临时授权给 T1（清理删除）=====
  let stubServer = null
  let STUB_MODEL_ID = ''
  let STUB_CHANNEL_ID = ''
  let CONV_MODEL_ID = ''
  let unauthorizedModel = null
  {
    stubServer = await startEmbeddingStub()
    const port = stubServer.address().port
    const chName = `wx-mock渠道-${TS}`
    const modelName = `wx-embedding-mock-${TS}`
    const crc = await retryApi('POST', '/api/ai/channel', {
      providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions',
      baseUrl: `http://127.0.0.1:${port}/v1`, apiKey: 'wx-mock-key', enabled: true, description: 'wiki external e2e 桩渠道',
    }, token)
    if (crc.status !== 200) throw new Error(`创建桩渠道失败: ${crc.status} ${crc.text.slice(0, 160)}`)
    const channels = await api('GET', '/api/ai/channel', { token })
    STUB_CHANNEL_ID = (channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? ''
    const mrc = await retryApi('POST', '/api/ai/model', {
      channelId: STUB_CHANNEL_ID,
      meta: { modelId: modelName, name: modelName, modelKind: 'embedding', description: 'wiki external e2e 桩模型' },
      enabled: true, isPublic: false,
    }, token)
    if (mrc.status !== 200) throw new Error(`创建桩模型失败: ${mrc.status} ${mrc.text.slice(0, 160)}`)
    const models = await api('GET', `/api/ai/model?channelId=${STUB_CHANNEL_ID}`, { token })
    STUB_MODEL_ID = (models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? ''
    if (!STUB_CHANNEL_ID || !STUB_MODEL_ID) throw new Error('未取得桩渠道/桩模型 id')
    const grant = await api('PUT', `/api/ai/model/${STUB_MODEL_ID}/authorization`, { token, body: { teamIds: [T1] } })
    if (grant.status !== 200) throw new Error(`桩模型授权失败: ${grant.status} ${grant.text.slice(0, 160)}`)

    // WX-10 元数据生成用的对话桩模型（同一桩渠道，桩服务按 /chat/completions 分流）
    const convName = `wx-metadata-mock-${TS}`
    const convrc = await retryApi('POST', '/api/ai/model', {
      channelId: STUB_CHANNEL_ID,
      meta: { modelId: convName, name: convName, modelKind: 'conversation', description: 'wiki external e2e 元数据桩模型' },
      enabled: true, isPublic: false,
    }, token)
    if (convrc.status !== 200) throw new Error(`创建对话桩模型失败: ${convrc.status} ${convrc.text.slice(0, 160)}`)
    const convModels = await api('GET', `/api/ai/model?channelId=${STUB_CHANNEL_ID}`, { token })
    CONV_MODEL_ID = (convModels.json?.items ?? []).find((m) => m.name === convName)?.id ?? ''
    if (!CONV_MODEL_ID) throw new Error('未取得对话桩模型 id')
    const convGrant = await api('PUT', `/api/ai/model/${CONV_MODEL_ID}/authorization`, { token, body: { teamIds: [T1] } })
    if (convGrant.status !== 200) throw new Error(`对话桩模型授权失败: ${convGrant.status} ${convGrant.text.slice(0, 160)}`)

    // WX-02e 用未授权模型：环境里已启用、未公开、未授权给 T1 的其它 embedding 模型（无则跳过）
    const all = await api('GET', '/api/ai/model', { token })
    const embs = (all.json?.items ?? []).filter((m) => m.modelKind === 'embedding' && m.enabled && m.id !== STUB_MODEL_ID && !m.isPublic)
    for (const m of embs) {
      const cur = await api('GET', `/api/ai/model/${m.id}/authorization`, { token })
      if (!(cur.json?.items ?? []).some((i) => Number(i.teamId) === T1)) { unauthorizedModel = m; break }
    }
    if (!unauthorizedModel) console.warn('WARN | WX-02e 跳过：环境无第二个可用的未授权 embedding 模型')
  }

  // 文档内容：含多段中文文本的 markdown（供提取/切割/向量化）
  const mdName = `wx-doc-${TS}.md`
  const mdContent = Buffer.from(
    `# 知识库外部接口测试\n\n## 背景\n\n` +
    '这是一段用于验证外部接口上传、提取、切割与向量化的中文文本，内容足够长以便切出多个切片。'.repeat(30) +
    `\n\n## 结论\n\n1. 外部上传三段式可用\n2. 提取与切割可用\n3. 向量化可用\n`,
    'utf8',
  )
  let DID = 0     // W1 主文档
  let BID = 0     // W1 删除/重传用文档
  let JID = 0     // W1 .json 文档（内容提取行为随环境而定的探针）
  let CHUNK_COUNT = 0  // W1 主文档切片数（WX-05d 后回填，WX-10 元数据条数断言基准）

  try {
    if (!AT1 || !AT2) throw new Error(`应用 token 换取失败: ${tok1.status}/${tok2.status}`)

    // ===== WX-01 准备与隔离 =====
    {
      check('WX-01a accessAppKey 换应用 token 200 tokenType=app', tok1.status === 200 && tok1.json?.tokenType === 'app' && typeof AT1 === 'string', `${tok1.status} ${tok1.text.slice(0, 120)}`)

      const l1 = await api('POST', '/api/external/wiki/list', { token: AT1, body: {} })
      const items1 = l1.json?.items ?? []
      check('WX-01b T1 列表 200 只含 T1 知识库', l1.status === 200 && items1.some((x) => Number(x.wikiId) === W1) && !items1.some((x) => Number(x.wikiId) === W2) && items1.every((x) => Number(x.teamId) === T1), `${l1.status} ${JSON.stringify(items1).slice(0, 200)}`)

      const l2 = await api('POST', '/api/external/wiki/list', { token: AT2, body: {} })
      const items2 = l2.json?.items ?? []
      check('WX-01c T2 列表仅含 T2 知识库', l2.status === 200 && items2.some((x) => Number(x.wikiId) === W2) && !items2.some((x) => Number(x.wikiId) === W1), JSON.stringify(items2).slice(0, 200))

      check('WX-01d T2 token 读 T1 库详情 404', (await api('GET', xw(W1), { token: AT2 })).status === 404)
      const ghost = 987654321098
      check('WX-01e 不存在 wikiId 详情 404', (await api('GET', xw(ghost), { token: AT1 })).status === 404)
      check('WX-01f 不存在 wikiId 文档列表 404', (await api('POST', xw(ghost, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 10 } })).status === 404)
    }

    // ===== WX-02 embedding-config =====
    {
      check('WX-02a 维度 0 → 400', (await api('PUT', xw(W1, '/embedding-config'), { token: AT1, body: { embeddingModelId: STUB_MODEL_ID, embeddingDimensions: 0 } })).status === 400)
      check('WX-02b 维度 2001 → 400', (await api('PUT', xw(W1, '/embedding-config'), { token: AT1, body: { embeddingModelId: STUB_MODEL_ID, embeddingDimensions: 2001 } })).status === 400)
      const bind = await api('PUT', xw(W1, '/embedding-config'), { token: AT1, body: { embeddingModelId: STUB_MODEL_ID, embeddingDimensions: EMBEDDING_DIMENSIONS } })
      check('WX-02c 绑定向量模型+维度 200', bind.status === 200, `${bind.status} ${bind.text.slice(0, 160)}`)
      const det = await api('GET', xw(W1), { token: AT1 })
      check('WX-02d 详情回显模型与维度', det.status === 200 && String(det.json?.embeddingModelId ?? '').toLowerCase() === String(STUB_MODEL_ID).toLowerCase() && Number(det.json?.embeddingDimensions) === EMBEDDING_DIMENSIONS, `${det.status} ${JSON.stringify({ m: det.json?.embeddingModelId, d: det.json?.embeddingDimensions })}`)
      if (unauthorizedModel) {
        const forbidden = await api('PUT', xw(W1, '/embedding-config'), { token: AT1, body: { embeddingModelId: unauthorizedModel.id, embeddingDimensions: EMBEDDING_DIMENSIONS } })
        check('WX-02e 未授权给团队的模型 403', forbidden.status === 403, `${forbidden.status} ${forbidden.text.slice(0, 160)}`)
      }
    }

    // ===== WX-03 上传三段式 =====
    {
      const pre = await api('POST', xw(W1, '/documents/preupload'), {
        token: AT1,
        body: { fileName: mdName, contentType: 'text/markdown', fileSize: mdContent.length, sha256: sha256Hex(mdContent) },
      })
      check('WX-03a preupload 200 返回 fileId/uploadUrl 且 isExist=false', pre.status === 200 && Number(pre.json?.fileId) > 0 && !!pre.json?.uploadUrl && pre.json?.isExist === false, `${pre.status} ${pre.text.slice(0, 200)}`)
      const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/markdown' }, body: mdContent })
      check('WX-03b PUT 预签名 URL 直传 200', put.status === 200, `${put.status}`)
      const comp = await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: pre.json.fileId, fileName: mdName } })
      check('WX-03c complete 200', comp.status === 200, `${comp.status} ${comp.text.slice(0, 160)}`)

      const list = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50 } })
      const item = (list.json?.items ?? []).find((x) => Number(x.fileId) === Number(pre.json.fileId))
      DID = item ? Number(item.documentId) : 0
      check('WX-03d documents/list 出现该文档', list.status === 200 && DID > 0 && item?.fileName === mdName, `${list.status} total=${list.json?.total}`)

      check('WX-03e 非法格式 .exe preupload 400', (await api('POST', xw(W1, '/documents/preupload'), { token: AT1, body: { fileName: `bad-${TS}.exe`, contentType: 'application/octet-stream', fileSize: 10, sha256: sha256Hex(`exe-${TS}`) } })).status === 400)

      // 跨 wiki 归属断言（C2 修复）：T2 在自己库真实上传拿 fileId，T1 拿它到 W1 complete → 404
      const otherContent = Buffer.from(`# 他团队文件 ${TS}`, 'utf8')
      const pre2 = await api('POST', xw(W2, '/documents/preupload'), { token: AT2, body: { fileName: `wx-other-${TS}.md`, contentType: 'text/markdown', fileSize: otherContent.length, sha256: sha256Hex(otherContent) } })
      let stolen = { status: 0, text: '' }
      if (pre2.status === 200) {
        await fetch(pre2.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/markdown' }, body: otherContent })
        stolen = await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: pre2.json.fileId, fileName: `wx-other-${TS}.md` } })
      }
      check('WX-03f 他团队 fileId 跨 wiki complete 404', stolen.status === 404, `pre2=${pre2.status} complete=${stolen.status} ${String(stolen.text).slice(0, 120)}`)

      // 删除 + 同 SHA 重传：外部删除连带存储文件（isExist 秒传分支在 wiki 域不可达），同 SHA 可重新上传入库
      const bName = `wx-dup-${TS}.md`
      const bContent = Buffer.from(`# 删除重传测试 ${TS}\n\n` + '第二个文档的内容，用于删除后重传验证。'.repeat(5), 'utf8')
      const preB = await api('POST', xw(W1, '/documents/preupload'), { token: AT1, body: { fileName: bName, contentType: 'text/markdown', fileSize: bContent.length, sha256: sha256Hex(bContent) } })
      if (preB.status === 200) {
        await fetch(preB.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/markdown' }, body: bContent })
        await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: preB.json.fileId, fileName: bName } })
        const listB = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50, query: bName } })
        const itemB = (listB.json?.items ?? []).find((x) => Number(x.fileId) === Number(preB.json.fileId))
        const delB = itemB ? await api('DELETE', xw(W1, '/documents'), { token: AT1, body: { documentIds: [Number(itemB.documentId)] } }) : { status: 0 }
        BID = itemB ? Number(itemB.documentId) : 0
        const listAfterDel = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50, query: bName } })
        const preB2 = await api('POST', xw(W1, '/documents/preupload'), { token: AT1, body: { fileName: bName, contentType: 'text/markdown', fileSize: bContent.length, sha256: sha256Hex(bContent) } })
        let reuploaded = false
        if (preB2.status === 200 && preB2.json?.isExist === false) {
          await fetch(preB2.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/markdown' }, body: bContent })
          const compB2 = await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: preB2.json.fileId, fileName: bName } })
          const listB2 = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50, query: bName } })
          reuploaded = compB2.status === 200 && (listB2.json?.items ?? []).some((x) => Number(x.fileId) === Number(preB2.json.fileId) && Number(x.documentId) !== BID)
        }
        check('WX-03g 删除文档 200 且同 SHA 可重新上传入库', delB.status === 200 && !(listAfterDel.json?.items ?? []).some((x) => Number(x.documentId) === BID) && preB2.status === 200 && typeof preB2.json?.isExist === 'boolean' && reuploaded, `del=${delB.status} pre2=${preB2.status} ${preB2.text.slice(0, 120)}`)
      } else {
        check('WX-03g 删除文档 200 且同 SHA 可重新上传入库', false, `第二个文档 preupload ${preB.status} ${preB.text.slice(0, 120)}`)
      }
    }

    // ===== WX-04 文档管理 =====
    {
      const newName = `wx-renamed-${TS}.md`
      const ren = await api('PUT', xw(W1, `/documents/${DID}/rename`), { token: AT1, body: { fileName: newName } })
      const list = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50 } })
      check('WX-04a rename 200 且列表回显新名', ren.status === 200 && (list.json?.items ?? []).some((x) => Number(x.documentId) === DID && x.fileName === newName), `ren=${ren.status} ${ren.text.slice(0, 120)}`)

      // 未提取内容 → 404（「文档内容尚未提取」/「文档不存在」任一消息，断言 404 即可）。
      // 主文档上传后自动提取；用 .json 文件制造"上传成功但提取失败"的未提取态，若环境支持 json 提取
      // 则回退断言不存在文档 content 404。两个分支互斥，只发一条断言。
      let checked = false
      const jName = `wx-raw-${TS}.json`
      const jContent = Buffer.from(JSON.stringify({ tip: '纯数据文件，markdown 抽取多半不支持', n: TS }), 'utf8')
      const preJ = await api('POST', xw(W1, '/documents/preupload'), { token: AT1, body: { fileName: jName, contentType: 'application/json', fileSize: jContent.length, sha256: sha256Hex(jContent) } })
      if (preJ.status === 200) {
        await fetch(preJ.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: jContent })
        await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: preJ.json.fileId, fileName: jName } })
        const listJ = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50, query: jName } })
        const itemJ = (listJ.json?.items ?? []).find((x) => Number(x.fileId) === Number(preJ.json.fileId))
        if (itemJ) {
          JID = Number(itemJ.documentId)
          const c = await api('GET', xw(W1, `/documents/${JID}/content`), { token: AT1 })
          if (c.status === 404) {
            checked = true
            check('WX-04b 未提取文档 content 404（文档内容尚未提取/文档不存在）', true)
          }
        }
      }
      if (!checked) {
        const c = await api('GET', xw(W1, '/documents/999999999/content'), { token: AT1 })
        check('WX-04b 未提取文档 content 404（文档内容尚未提取/文档不存在）', c.status === 404, `json 提取成功走回退：ghost=${c.status}`)
      }
      const ext = await api('POST', xw(W1, `/documents/${DID}/extract`), { token: AT1 })
      check('WX-04c extract 触发 200', ext.status === 200, `${ext.status} ${ext.text.slice(0, 120)}`)
    }

    // ===== WX-05 切割与向量化 =====
    {
      // 参数抄 wiki-embedding-e2e（recursive + token + cl100k_base）
      const part = await api('POST', xw(W1, `/documents/${DID}/partition`), {
        token: AT1,
        body: { splitMode: 'recursive', chunkSize: 256, chunkOverlap: 1, overlapUnit: 'sentence', sizeUnit: 'token', tokenEncodingOrModel: 'cl100k_base' },
      })
      check('WX-05a 普通切割 200（recursive + token）', part.status === 200, `${part.status} ${part.text.slice(0, 160)}`)

      const emb = await api('POST', xw(W1, `/documents/${DID}/embedding`), { token: AT1, body: { isEmbedSourceText: true, isEmbedMetadata: false } })
      const taskId = emb.json?.taskId
      check('WX-05b 触发向量化 200 返回 taskId', emb.status === 200 && typeof taskId === 'string' && taskId.length > 0, `${emb.status} ${emb.text.slice(0, 160)}`)

      // 进行中重复触发 → 409（桩服务每次响应延迟 3s，任务必然在途，无竞态）
      const dup = await api('POST', xw(W1, `/documents/${DID}/embedding`), { token: AT1, body: { isEmbedSourceText: true, isEmbedMetadata: false } })
      check('WX-05c 进行中重复触发 409', dup.status === 409, `${dup.status} ${dup.text.slice(0, 120)}`)

      // 轮询直到 embeddingCount > 0（先 3s 快轮询 60s，之后 30s 间隔，上限约 5 分钟）
      let done = false
      let last = null
      if (taskId) {
        const start = Date.now()
        while (Date.now() - start < 5 * 60 * 1000) {
          await new Promise((resolve) => setTimeout(resolve, Date.now() - start < 60000 ? 3000 : 30000))
          last = await api('GET', xw(W1, `/documents/${DID}/embedding`), { token: AT1 })
          if (last.status === 200 && Number(last.json?.embeddingCount ?? 0) > 0) { done = true; break }
        }
      }
      check('WX-05d 轮询向量化完成 embeddingCount>0', done, last ? `${last.status} embeddingCount=${last.json?.embeddingCount}` : 'no taskId')
      if (done) {
        const items = last.json?.items ?? []
        CHUNK_COUNT = items.length
        const sliceOrders = items.map((x) => Number(x.sliceOrder ?? 0))
        // 切片序号当前为 0-based（WikiDocumentProcessingService 从 0 递增），断言连续自增且含原文片段
        check('WX-05e 切片顺序连续自增且含原文片段', items.length >= 1 && sliceOrders[0] === 0 && sliceOrders.every((o, i) => i === 0 || o === sliceOrders[i - 1] + 1) && items.some((x) => typeof x.sliceContent === 'string' && x.sliceContent.includes('知识库外部接口测试')), `items=${items.length} orders=${sliceOrders.join(',')}`)
      }
    }

    // ===== WX-08 原文件下载 =====
    {
      const dl = await api('GET', xw(W1, `/documents/${DID}/download`), { token: AT1 })
      let bytesOk = false
      if (dl.status === 200 && typeof dl.json?.value === 'string' && dl.json.value.startsWith('http')) {
        const file = await fetch(dl.json.value).catch(() => null)
        if (file && file.status === 200) {
          const buf = Buffer.from(await file.arrayBuffer())
          bytesOk = buf.equals(mdContent)
        }
      }
      check('WX-08a download 200 返回预签名 URL 且原文件字节一致', bytesOk, `${dl.status} ${String(dl.json?.value ?? dl.text).slice(0, 120)}`)
      check('WX-08b 不存在文档 download 404', (await api('GET', xw(W1, '/documents/999999999/download'), { token: AT1 })).status === 404)
    }

    // ===== WX-09 向量召回 =====
    {
      const rec = await api('POST', xw(W1, '/recall/list'), { token: AT1, body: { query: '外部接口上传与向量化', top: 5 } })
      const items = rec.json?.items ?? []
      check('WX-09a recall 200 命中已向量化文档切片且得分为有效余弦值', rec.status === 200 && items.length > 0 && items.every((x) => String(Number(x.documentId)) === String(DID)) && items.every((x) => typeof x.score === 'number' && x.score >= -1 && x.score <= 1), `${rec.status} items=${items.length} ${rec.text.slice(0, 160)}`)
      const top1 = await api('POST', xw(W1, '/recall/list'), { token: AT1, body: { query: '外部接口上传与向量化', top: 1 } })
      check('WX-09b top=1 最多返回 1 条', top1.status === 200 && (top1.json?.items ?? []).length <= 1, `${top1.status} ${(top1.json?.items ?? []).length}`)
      const ghostFilter = await api('POST', xw(W1, '/recall/list'), { token: AT1, body: { query: '外部接口上传与向量化', top: 5, documentIds: [987654321] } })
      check('WX-09c 文档范围过滤生效（ghost 文档无命中）', ghostFilter.status === 200 && (ghostFilter.json?.items ?? []).length === 0, `${ghostFilter.status} ${(ghostFilter.json?.items ?? []).length}`)
      const badTop = await api('POST', xw(W1, '/recall/list'), { token: AT1, body: { query: '外部接口', top: 0 } })
      const badQuery = await api('POST', xw(W1, '/recall/list'), { token: AT1, body: { query: '', top: 5 } })
      check('WX-09d 非法参数 400（top=0 / 空 query）', badTop.status === 400 && badQuery.status === 400, `top=${badTop.status} query=${badQuery.status}`)
      // 读档分档：recall/list 走 GET/*/list=读口径，只读 key 放行、只写 key 403（若分档退化为写档会在此暴露）
      const roRecall = await api('POST', xw(W1, '/recall/list'), { token: AT1_RO, body: { query: '外部接口', top: 5 } })
      const woRecall = await api('POST', xw(W1, '/recall/list'), { token: AT1_WO, body: { query: '外部接口', top: 5 } })
      check('WX-09e 读档分档：只读 key 可召回、只写 key 403', roRecall.status === 200 && woRecall.status === 403, `ro=${roRecall.status} wo=${woRecall.status}`)
    }

    // ===== WX-10 切片元数据生成 =====
    {
      const genPath = `/documents/${DID}/chunks/metadata/generate`
      check('WX-10a 不存在文档元数据生成 404', (await api('POST', xw(W1, '/documents/999999999/chunks/metadata/generate'), { token: AT1, body: { metadataModelId: CONV_MODEL_ID } })).status === 404)
      check('WX-10b embedding 模型用于元数据生成 400', (await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: STUB_MODEL_ID } })).status === 400)
      check('WX-10c 不存在元数据模型 404', (await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: '11111111-1111-1111-1111-111111111111' } })).status === 404)
      // 未切割文档 → 400（.json 探针文档未切割；未创建成功则跳过该断言）
      if (JID > 0) {
        check('WX-10d 未切割文档元数据生成 400', (await api('POST', xw(W1, `/documents/${JID}/chunks/metadata/generate`), { token: AT1, body: { metadataModelId: CONV_MODEL_ID } })).status === 400)
      } else {
        console.warn('WARN | WX-10d 跳过：.json 探针文档未创建')
      }

      // 全量生成：桩返回 outline×1+questions×2+keywords×2+summary×1+aggregated×1 → 每切片 7 条
      const gen = await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: CONV_MODEL_ID } })
      check('WX-10e 全量生成条数 = 切片数×7', gen.status === 200 && Number(gen.json?.value) === CHUNK_COUNT * 7, `${gen.status} ${gen.text.slice(0, 120)} chunks=${CHUNK_COUNT}`)
      const det = await api('GET', xw(W1, `/documents/${DID}/embedding`), { token: AT1 })
      const detItems = det.json?.items ?? []
      const types = new Set(detItems.flatMap((x) => (x.metadatas ?? []).map((m) => Number(m.metadataType))))
      check('WX-10f 详情回读每切片 7 条且类型齐全（1大纲/2问题/3关键词/4摘要/5聚合）', detItems.length === CHUNK_COUNT && detItems.every((x) => Number(x.metadataCount ?? 0) === 7) && [1, 2, 3, 4, 5].every((t) => types.has(t)), `items=${detItems.length} counts=${detItems.map((x) => x.metadataCount).join(',')} types=${[...types].join(',')}`)

      // 指定策略覆盖生成（appendExisting=false 清空后仅大纲）→ 每切片 1 条
      const gen2 = await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: CONV_MODEL_ID, strategyTypes: ['outlineGeneration'] } })
      const det2 = await api('GET', xw(W1, `/documents/${DID}/embedding`), { token: AT1 })
      const det2Items = det2.json?.items ?? []
      check('WX-10g 策略覆盖生成 = 切片数×1 且详情每切片 1 条大纲', gen2.status === 200 && Number(gen2.json?.value) === CHUNK_COUNT && det2Items.length === CHUNK_COUNT && det2Items.every((x) => Number(x.metadataCount ?? 0) === 1 && (x.metadatas ?? []).every((m) => Number(m.metadataType) === 1)), `gen=${gen2.status}/${gen2.json?.value} counts=${det2Items.map((x) => x.metadataCount).join(',')}`)

      // 追加模式（appendExisting=true 保留已有再追加）→ 每切片 2 条
      const gen3 = await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: CONV_MODEL_ID, strategyTypes: ['outlineGeneration'], appendExisting: true } })
      const det3 = await api('GET', xw(W1, `/documents/${DID}/embedding`), { token: AT1 })
      const det3Items = det3.json?.items ?? []
      check('WX-10h 追加生成 = 切片数×1 且详情每切片累计 2 条', gen3.status === 200 && Number(gen3.json?.value) === CHUNK_COUNT && det3Items.every((x) => Number(x.metadataCount ?? 0) === 2), `gen=${gen3.status}/${gen3.json?.value} counts=${det3Items.map((x) => x.metadataCount).join(',')}`)

      // 指定切片：只处理第一个切片 → 1 条
      const firstChunkId = det3Items[0]?.chunkId
      const gen4 = await api('POST', xw(W1, genPath), { token: AT1, body: { metadataModelId: CONV_MODEL_ID, chunkIds: [firstChunkId], strategyTypes: ['outlineGeneration'] } })
      check('WX-10i 指定切片生成条数 = 1', gen4.status === 200 && Number(gen4.json?.value) === 1, `${gen4.status} ${gen4.text.slice(0, 120)}`)
    }

    // ===== WX-06 越权与认证 =====
    {
      check('WX-06a 无 token 调外部接口 401', (await api('POST', '/api/external/wiki/list', { body: {} })).status === 401)
      check('WX-06b 伪造 token 401', (await api('POST', '/api/external/wiki/list', { token: 'wx-invalid-token', body: {} })).status === 401)

      // T2 应用 token 访问 T1 库任意端点 → 404（授权器先行，写端点也不会产生变更）
      const cross = []
      cross.push(['GET 详情', await api('GET', xw(W1), { token: AT2 })])
      cross.push(['POST documents/list', await api('POST', xw(W1, '/documents/list'), { token: AT2, body: { pageNo: 1, pageSize: 10 } })])
      cross.push(['PUT embedding-config', await api('PUT', xw(W1, '/embedding-config'), { token: AT2, body: { embeddingModelId: STUB_MODEL_ID, embeddingDimensions: EMBEDDING_DIMENSIONS } })])
      cross.push(['POST preupload', await api('POST', xw(W1, '/documents/preupload'), { token: AT2, body: { fileName: `cross-${TS}.md`, contentType: 'text/markdown', fileSize: 8, sha256: sha256Hex(`cross-${TS}`) } })])
      cross.push(['GET document embedding', await api('GET', xw(W1, `/documents/${DID}/embedding`), { token: AT2 })])
      cross.push(['DELETE documents', await api('DELETE', xw(W1, '/documents'), { token: AT2, body: { documentIds: [DID] } })])
      cross.push(['GET document download', await api('GET', xw(W1, `/documents/${DID}/download`), { token: AT2 })])
      cross.push(['POST recall/list', await api('POST', xw(W1, '/recall/list'), { token: AT2, body: { query: '外部接口', top: 5 } })])
      cross.push(['POST chunks/metadata/generate', await api('POST', xw(W1, `/documents/${DID}/chunks/metadata/generate`), { token: AT2, body: { metadataModelId: CONV_MODEL_ID } })])
      check('WX-06c T2 token 访问 T1 库各端点一律 404', cross.every(([, r]) => r.status === 404), cross.map(([n, r]) => `${n}=${r.status}`).join(' '))

      const internal = await api('POST', '/api/external/wiki/list', { token, body: {} })
      check('WX-06d 内部用户 JWT 调外部接口 401/403', internal.status === 401 || internal.status === 403, `${internal.status}`)
    }
  } finally {
    // ===== WX-11 允许上传文件类型设置（WIKI_ALLOWED_FILE_TYPES，root 写，网页端与外部接口共用）=====
    {
      const DEFAULT_TYPES = '.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json'
      const putTypes = async (value) => api('PUT', '/api/settings', { token, body: { key: 'WIKI_ALLOWED_FILE_TYPES', value } })
      const sha256Hex = (s) => crypto.createHash('sha256').update(s).digest('hex')
      const xPreup = (fileName, fileSize, sha, contentType) => api('POST', xw(W1, '/documents/preupload'), { token: AT1, body: { fileName, contentType, fileSize, sha256: sha } })

      // a) 恢复默认后设置项与 upload-limit 一致回显
      await putTypes(DEFAULT_TYPES)
      const dft = await api('GET', '/api/wiki/upload-limit', { token })
      check('WX-11a 默认允许类型回读（网页端 upload-limit 与设置一致）', dft.status === 200 && Array.isArray(dft.json?.allowedFileTypes) && dft.json.allowedFileTypes.join(',') === DEFAULT_TYPES, JSON.stringify(dft.json?.allowedFileTypes))

      // b) 保存格式校验：无点号 / 大小写归一后重复 / 空串均 400
      const badNoDot = await putTypes('.pdf,pdf2,bad ext')
      const badDup = await putTypes('.pdf,.PDF')
      const badEmpty = await putTypes('  ')
      check('WX-11b 非法类型串保存 400（无点号/重复/空串）', badNoDot.status === 400 && badDup.status === 400 && badEmpty.status === 400, `nodot=${badNoDot.status} dup=${badDup.status} empty=${badEmpty.status}`)

      // c) 收窄为 .txt,.sh：外部接口 preupload 同步受限，.pdf 400 且提示允许清单
      const narrow = await putTypes('.txt,.sh')
      const lim = await api('GET', '/api/wiki/upload-limit', { token })
      const noPdf = await xPreup(`wx11-${TS}.pdf`, 5, sha256Hex('pdf' + TS), 'application/pdf')
      check('WX-11c 收窄后设置回显且非允许类型 preupload 400', narrow.status === 200 && lim.json?.allowedFileTypes?.join(',') === '.txt,.sh' && noPdf.status === 400 && noPdf.text.includes('.txt'), `narrow=${narrow.status} lim=${JSON.stringify(lim.json?.allowedFileTypes)} pdf=${noPdf.status} ${noPdf.text.slice(0, 120)}`)

      // d) 放开的 .sh（未知文本类型）放行上传，complete 后自动提取走纯文本兜底
      const shName = `wx11-${TS}.sh`
      const shContent = Buffer.from(`#!/bin/bash\necho "wx11-${TS}"\n`, 'utf8')
      const shPre = await xPreup(shName, shContent.length, sha256Hex(shContent), 'text/x-sh')
      check('WX-11d 放开的 .sh preupload 200', shPre.status === 200, `${shPre.status} ${shPre.text.slice(0, 120)}`)
      if (shPre.status === 200) {
        await fetch(shPre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/x-sh' }, body: shContent })
        const comp = await api('POST', xw(W1, '/documents/complete'), { token: AT1, body: { isSuccess: true, fileId: shPre.json.fileId, fileName: shName } })
        let docId = 0
        for (let i = 0; i < 10 && !docId; i++) {
          const list = await api('POST', xw(W1, '/documents/list'), { token: AT1, body: { pageNo: 1, pageSize: 50, query: shName } })
          const item = (list.json?.items ?? []).find((x) => Number(x.fileId) === Number(shPre.json.fileId))
          if (item) docId = Number(item.documentId)
          else await new Promise((r) => setTimeout(r, 1000))
        }
        // complete 内同步等待自动提取，返回后内容应已入库（未知类型按纯文本读取）
        const c = docId ? await api('GET', xw(W1, `/documents/${docId}/content`), { token: AT1 }) : { status: 0, json: null }
        check('WX-11e .sh 按纯文本兜底提取入库', c.status === 200 && String(c.json?.value ?? '').includes(`wx11-${TS}`), `doc=${docId} status=${c.status} ${(c.text ?? '').slice(0, 120)}`)
        if (docId) await api('DELETE', xw(W1, '/documents'), { token: AT1, body: { documentIds: [docId] } })
      }

      // f) 恢复默认后 .pdf 放行
      await putTypes(DEFAULT_TYPES)
      const backPdf = await xPreup(`wx11-back-${TS}.pdf`, 5, sha256Hex('back' + TS), 'application/pdf')
      check('WX-11f 恢复默认后 .pdf preupload 200', backPdf.status === 200, `${backPdf.status} ${backPdf.text.slice(0, 120)}`)
    }

    // ===== 清理：删文档、知识库、桩模型/桩渠道、接入点、团队，关闭桩服务（可重复执行）=====
    const docIds = [DID, BID, JID].filter((id) => Number(id) > 0)
    if (docIds.length > 0 && AT1) {
      const del = await api('DELETE', xw(W1, '/documents'), { token: AT1, body: { documentIds: docIds } }).catch(() => null)
      if (!del || del.status !== 200) console.warn('WARN | 清理文档未成功（知识库删除会连带）')
    }
    await api('DELETE', `/api/wiki/${W1}`, { token })
    await api('DELETE', `/api/wiki/${W2}`, { token })
    if (STUB_MODEL_ID || CONV_MODEL_ID) {
      const dm = await api('POST', '/api/ai/model/batch-delete', { token, body: { modelIds: [STUB_MODEL_ID, CONV_MODEL_ID].filter(Boolean) } })
      if (dm.status !== 200) console.warn(`WARN | 桩模型删除失败: ${dm.status}`)
    }
    if (STUB_CHANNEL_ID) {
      const dc = await api('DELETE', `/api/ai/channel/${STUB_CHANNEL_ID}`, { token })
      if (dc.status !== 200) console.warn(`WARN | 桩渠道删除失败: ${dc.status}`)
    }
    if (ACC1) await api('DELETE', `/api/access-app/${ACC1}`, { token })
    if (ACC2) await api('DELETE', `/api/access-app/${ACC2}`, { token })
    if (ACC_RO) await api('DELETE', `/api/access-app/${ACC_RO}`, { token })
    if (ACC_WO) await api('DELETE', `/api/access-app/${ACC_WO}`, { token })
    // 团队不可解散：清理改为管理员禁用归档
    await api('PUT', `/api/admin/team/${T1}/disable`, { token, body: { isDisable: true } })
    await api('PUT', `/api/admin/team/${T2}/disable`, { token, body: { isDisable: true } })
    if (stubServer) stubServer.close()
  }

  console.log(`\n===== 知识库外部接口 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
