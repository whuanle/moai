// 知识库召回测试 E2E（场景 @WK-S37 ~ @WK-S55；后端 127.0.0.1:5210）
// 覆盖：参数校验（空查询/越界 topK/越界阈值/AI 优化缺模型/非法文档 id）→ 团队门禁（非成员 404）
//       → 向量召回（得分降序）→ 文档范围过滤 → 相似度阈值 → AI 优化问题 → AI 生成回答
//       → 重排序接入 @WK-S42（rerankScore 降序、顺序重排）→ 重排序渠道故障降级 → 相邻片段上下文与去重。
// 模型策略：优先使用本地 OpenAI 兼容桩（/v1/embeddings 确定性哈希向量 + /v1/chat/completions 固定文案
//       + /v1/rerank 反向打分），无需真实模型渠道；桩创建失败时回退到平台既有模型
//       （admin 自举授权 + 探针挑选），仍不可用则依赖模型的场景标记 SKIP（不计入 FAIL）。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
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

/** rerank 桩行为：normal 正常反向打分 / error 返回 500（模拟渠道故障降级） */
let rerankMode = 'normal'

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function uploadWikiDoc(token, wikiId, fileName, content, contentType) {
  const sha256 = crypto.createHash('sha256').update(content).digest('hex')
  const pre = await api('POST', `/api/wiki/${wikiId}/documents/preupload`, {
    token,
    body: { wikiId, fileName, contentType, fileSize: content.length, sha256 },
  })
  if (pre.status !== 200) return { ok: false, step: 'preupload', res: pre }
  const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: content })
  if (put.status !== 200) return { ok: false, step: 'put', res: { status: put.status, text: '' } }
  const complete = await api('POST', `/api/wiki/${wikiId}/documents/complete`, {
    token,
    body: { wikiId, isSuccess: true, fileId: pre.json.fileId, fileName },
  })
  if (complete.status !== 200) return { ok: false, step: 'complete', res: complete }
  const list = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const item = (list.json?.items ?? []).find((i) => Number(i.fileId) === Number(pre.json.fileId))
  return { ok: true, fileId: pre.json.fileId, documentId: item ? Number(item.documentId) : null }
}

const makeMd = (title, keywords, words = 8) => Buffer.from(
  `# ${title}\n\n## 说明\n\n` +
  `本文用于验证知识库召回测试。${keywords}。`.repeat(words) +
  `\n\n## 要点\n\n1. ${keywords}是本文核心主题\n2. 如需了解更多请联系管理员\n`,
  'utf8',
)

async function listDocs(token, wikiId) {
  const r = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const map = new Map()
  for (const item of r.json?.items ?? []) map.set(Number(item.documentId), item)
  return map
}

async function waitUntil(token, wikiId, predicate, timeoutMs = 90000) {
  const start = Date.now()
  let docs = null
  while (Date.now() - start < timeoutMs) {
    docs = await listDocs(token, wikiId)
    if (predicate(docs)) return { ok: true, docs }
    await new Promise((r) => setTimeout(r, 1500))
  }
  return { ok: false, docs }
}

/** 召回测试调用封装 */
async function recall(token, wikiId, body) {
  return api('POST', `/api/wiki/${wikiId}/recall-test`, { token, body: { wikiId, top: 5, ...body } })
}

// ===== 本地 OpenAI 兼容桩：embedding（确定性哈希向量）+ chat（固定文案） =====

/** 字符级 hash 分布向量：共享词多的文本对相似度更高，便于断言排序 */
// embedding 桩请求计数（@WK-S53 复用快速路径断言：命中聚类后再次召回不再调用 embedding）
let embedCallCount = 0

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
        embedCallCount++
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

      // rerank：反向打分（输入越靠后得分越高）→ 重排后顺序应为输入序的倒序，便于断言顺序确实被重排
      if (String(req.url).includes('/rerank')) {
        if (rerankMode === 'error') {
          res.writeHead(500, { 'Content-Type': 'application/json' })
          res.end(JSON.stringify({ code: 500, message: 'stub rerank failure' }))
          return
        }

        const docs = Array.isArray(payload.documents) ? payload.documents : []
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          model: payload.model ?? 'stub-rerank',
          results: docs.map((_, index) => ({ index, relevance_score: docs.length <= 1 ? 0.9 : index / docs.length })),
        }))
        return
      }

      // chat completions：优化问题桩返回提取到的关键词；回答桩返回固定结论
      const userMsgs = (payload.messages ?? []).filter((m) => m.role === 'user').map((m) => String(m.content ?? ''))
      const last = userMsgs[userMsgs.length - 1] ?? ''
      const hasFacts = userMsgs.some((m) => m.includes('======')) || (payload.messages ?? []).length > 2
      let reply = '退货政策 运费'
      if (hasFacts) {
        reply = '根据知识库内容，商品签收后 7 天内可无理由退货，退货运费由买家承担。'
      } else {
        const kw = last.replace(/[^\u4e00-\u9fa5A-Za-z0-9 ]/g, ' ').split(/\s+/).filter((w) => w.length >= 2)
        reply = kw.slice(0, 5).join(' ') || last.slice(0, 20)
      }

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
        id: 'chatcmpl-stub',
        object: 'chat.completion',
        created: Math.floor(Date.now() / 1000),
        model: payload.model ?? 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: reply }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

/** admin 登录（失败返回 null） */
async function adminLogin() {
  try {
    const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    return l.status === 200 ? l.json.accessToken : null
  } catch {
    return null
  }
}

/** 用桩创建渠道 + embedding/对话模型并授权给团队，返回 {channelId, embeddingModelId, conversationModelId} 或 null */
async function createStubModels(adminToken, TID, stubPort) {
  const chName = `recall-stub渠道-${TS}`
  const crc = await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'recall-stub-key', enabled: true, description: 'wiki-recall E2E 桩渠道' },
  })
  if (crc.status !== 200) { console.log(`INFO | 桩渠道创建失败：${crc.status} ${crc.text.slice(0, 120)}`); return null }
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) { console.log('INFO | 桩渠道未找到'); return null }

  const mkModel = async (modelName, kind) => {
    const mrc = await api('POST', '/api/ai/model', {
      token: adminToken,
      body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: kind, description: 'wiki-recall E2E 桩模型' }, enabled: true, isPublic: false },
    })
    if (mrc.status !== 200) { console.log(`INFO | 桩模型 ${kind} 创建失败：${mrc.status} ${mrc.text.slice(0, 120)}`); return '' }
    const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
    return String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  }

  const embeddingModelId = await mkModel(`recall-stub-embed-${TS}`, 'embedding')
  const conversationModelId = await mkModel(`recall-stub-chat-${TS}`, 'conversation')
  const rerankModelId = await mkModel(`recall-stub-rerank-${TS}`, 'rerank')
  if (!embeddingModelId || !conversationModelId || !rerankModelId) return null

  for (const modelId of [embeddingModelId, conversationModelId, rerankModelId]) {
    const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    const put = await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
    if (put.status !== 200) { console.log(`INFO | 桩模型授权失败：${put.status}`); return null }
  }

  return { channelId: CH_ID, embeddingModelId, conversationModelId, rerankModelId }
}

/** 清理桩渠道（级联删模型） */
async function cleanupStubModels(adminToken, channelId) {
  if (!adminToken || !channelId) return
  await api('DELETE', `/api/ai/channel/${channelId}`, { token: adminToken })
}

/** 团队无可用模型时，用种子管理员把团队加入已启用的 embedding/对话模型授权（合并，不覆盖既有授权） */
async function bootstrapTeamModels(adminToken, TID) {
  const list = await api('GET', '/api/ai/model', { token: adminToken })
  if (list.status !== 200) return { ok: false, reason: `模型列表 ${list.status}` }
  const items = list.json?.items ?? []
  const enabledOf = (kind) => items.filter((x) => x.enabled === true && x.modelKind === kind)
  const targets = [...enabledOf('embedding').slice(0, 3), ...enabledOf('conversation').slice(0, 6)]
  if (targets.length === 0) return { ok: false, reason: '无已启用的 embedding/对话模型' }

  for (const model of targets) {
    const cur = await api('GET', `/api/ai/model/${model.id}/authorization`, { token: adminToken })
    if (cur.status !== 200) return { ok: false, reason: `授权查询 ${cur.status}` }
    if (cur.json?.isPublic === true) continue
    const teamIds = (cur.json?.items ?? []).map((x) => Number(x.teamId))
    if (teamIds.includes(TID)) continue
    const put = await api('PUT', `/api/ai/model/${model.id}/authorization`, { token: adminToken, body: { modelId: model.id, teamIds: [...teamIds, TID] } })
    if (put.status !== 200) return { ok: false, reason: `授权更新 ${put.status} ${put.text.slice(0, 120)}` }
  }
  return { ok: true, reason: '' }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ro')
  const member = await mkuser('rm')
  const outsider = await mkuser('rx')

  const teamRes = await api('POST', '/api/team', { token: owner.token, body: { name: 'recall-team-' + TS } })
  check('WK-S37 前置：创建团队 200', teamRes.status === 200 && Number(teamRes.json?.value) > 0, `${teamRes.status} ${teamRes.text.slice(0, 120)}`)
  const TID = Number(teamRes.json?.value)
  const addMember = await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  check('WK-S37 前置：添加成员 200', addMember.status === 200, `${addMember.status} ${addMember.text.slice(0, 120)}`)
  const WID = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'recall_wiki_' + TS, description: 'recall e2e' } })).json.value)

  // ===== 模型准备：本地桩优先，失败时回退平台既有模型（admin 自举 + 探针） =====
  const adminToken = await adminLogin()
  const stub = adminToken ? await startStubServer() : null
  const stubPort = stub ? stub.address().port : 0
  let stubModels = stub ? await createStubModels(adminToken, TID, stubPort) : null

  let embModelId = '', convModelId = '', channelId = ''
  if (stubModels) {
    embModelId = stubModels.embeddingModelId
    convModelId = stubModels.conversationModelId
    channelId = stubModels.channelId
    console.log('INFO | 使用本地桩模型（embedding + conversation）')
  } else {
    console.log('INFO | 桩模型不可用，回退平台既有模型')
    let opt = await api('GET', `/api/wiki/model-options?teamId=${TID}`, { token: owner.token })
    let embCandidates = opt.json?.embeddingModels ?? []
    let convCandidates = opt.json?.conversationModels ?? []
    if (embCandidates.length === 0 || convCandidates.length === 0) {
      if (adminToken) {
        const boot = await bootstrapTeamModels(adminToken, TID)
        if (!boot.ok) console.log(`INFO | 模型自举失败：${boot.reason}`)
        opt = await api('GET', `/api/wiki/model-options?teamId=${TID}`, { token: owner.token })
        embCandidates = opt.json?.embeddingModels ?? []
        convCandidates = opt.json?.conversationModels ?? []
      }
    }
    embModelId = String(embCandidates[0]?.id ?? '')
    convModelId = String(convCandidates[0]?.id ?? '')
  }
  const hasEmb = embModelId !== ''
  const hasConv = convModelId !== ''
  if (!hasEmb) console.log('INFO | 无可用 embedding 模型，依赖召回的场景将跳过')
  if (!hasConv) console.log('INFO | 无可用对话模型，依赖 AI 优化/回答的场景将跳过')

  // ===== @WK-S37：参数校验与门禁（不依赖模型） =====
  const emptyQuery = await recall(member.token, WID, { query: '' })
  check('WK-S37a 空查询 400', emptyQuery.status === 400, `${emptyQuery.status}`)

  const blankQuery = await recall(member.token, WID, { query: '   ' })
  check('WK-S37a 空白查询 400', blankQuery.status === 400, `${blankQuery.status}`)

  const topZero = await recall(member.token, WID, { query: '测试', top: 0 })
  check('WK-S37a top=0 400', topZero.status === 400, `${topZero.status}`)

  const topOver = await recall(member.token, WID, { query: '测试', top: 51 })
  check('WK-S37a top=51 400', topOver.status === 400, `${topOver.status}`)

  const badScore = await recall(member.token, WID, { query: '测试', minScore: 1.5 })
  check('WK-S37a minScore=1.5 400', badScore.status === 400, `${badScore.status}`)

  const noModel = await recall(member.token, WID, { query: '测试', isOptimizeQuery: true })
  check('WK-S37a AI 优化缺模型 400', noModel.status === 400, `${noModel.status}`)

  const outsiderRes = await recall(outsider.token, WID, { query: '测试' })
  check('WK-S37b 非成员召回 404', outsiderRes.status === 404, `${outsiderRes.status}`)

  const badDoc = await recall(member.token, WID, { query: '测试', documentIds: [0] })
  check('WK-S37a 非法文档 id 400', badDoc.status === 400, `${badDoc.status}`)

  // ===== @WK-S38：向量召回 / 文档范围 / 阈值（依赖 embedding 模型） =====
  let doc1Id = null, doc2Id = null, doc3Id = null
  if (hasEmb) {
    const bind = await api('PUT', `/api/wiki/${WID}/embedding-config`, {
      token: owner.token,
      body: { wikiId: WID, embeddingModelId: embModelId, embeddingDimensions: 1024 },
    })
    check('WK-S38 前置：绑定向量模型 200', bind.status === 200, `${bind.status} ${bind.text.slice(0, 120)}`)

    const d1 = await uploadWikiDoc(owner.token, WID, `recall-return-${TS}.md`, makeMd('退货政策说明', '商品签收后 7 天内可无理由退货，退货时需保证商品完好，运费由买家承担'), 'text/markdown')
    const d2 = await uploadWikiDoc(owner.token, WID, `recall-invoice-${TS}.md`, makeMd('发票开具流程', '发票在订单完成后 30 天内开具，支持电子发票和纸质发票，抬头信息需准确'), 'text/markdown')
    doc1Id = d1.documentId
    doc2Id = d2.documentId
    const uploadOk = Boolean(doc1Id) && Boolean(doc2Id)
    check('WK-S38 前置：两文档上传 200', uploadOk, `doc1=${d1.ok}/${doc1Id} doc2=${d2.ok}/${doc2Id}`)

    if (uploadOk) {
      const part = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
        token: owner.token,
        body: { wikiId: WID, documentIds: [doc1Id, doc2Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' },
      })
      check('WK-S38 前置：批量切割 200', part.status === 200, `${part.status} ${part.text.slice(0, 120)}`)

      const embed = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
        token: owner.token,
        body: { wikiId: WID, documentIds: [doc1Id, doc2Id], isEmbedding: true, embedSourceText: true, embedMetadata: false },
      })
      const embedItems = embed.json?.items ?? []
      check('WK-S38 前置：批量向量化提交 200', embed.status === 200 && embedItems.every((x) => x.success === true), `${embed.status} ${JSON.stringify(embedItems).slice(0, 160)}`)

      const done = await waitUntil(owner.token, WID, (docs) => docs.get(doc1Id)?.isEmbedding === true && docs.get(doc2Id)?.isEmbedding === true, 120000)
      check('WK-S38 前置：两文档向量化完成', done.ok, '120s 内未全部完成')

      if (done.ok) {
        const all = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
        const items = all.json?.items ?? []
        check('WK-S38a 全库召回 200 且有命中', all.status === 200 && items.length > 0, `${all.status} items=${items.length}`)
        const scores = items.map((x) => Number(x.score ?? 0))
        const desc = scores.every((s, i) => i === 0 || scores[i - 1] >= s)
        check('WK-S38a 得分降序排列', desc, JSON.stringify(scores))
        check('WK-S38a 命中项带文档名', items.every((x) => String(x.documentName ?? '') !== ''), JSON.stringify(items[0]))
        check('WK-S38a 命中项带切片 id', items.every((x) => Number(x.chunkId ?? 0) > 0), '')

        const scoped = await recall(member.token, WID, { query: '退货政策 运费', documentIds: [doc2Id], top: 10 })
        const scopedItems = scoped.json?.items ?? []
        check('WK-S38b 范围过滤：命中仅来自指定文档', scoped.status === 200 && scopedItems.every((x) => Number(x.documentId) === doc2Id), JSON.stringify(scopedItems.map((x) => x.documentId)))

        const scopedSelf = await recall(member.token, WID, { query: '发票 开具', documentIds: [doc2Id], top: 10 })
        const scopedSelfItems = scopedSelf.json?.items ?? []
        check('WK-S38b 范围过滤：指定文档可召回自身内容', scopedSelf.status === 200 && scopedSelfItems.length > 0 && scopedSelfItems.every((x) => Number(x.documentId) === doc2Id), `items=${scopedSelfItems.length}`)

        const emptyScope = await recall(member.token, WID, { query: '退货政策 运费', documentIds: [99999999], top: 10 })
        check('WK-S38b 范围过滤：不存在文档 0 命中', emptyScope.status === 200 && (emptyScope.json?.items ?? []).length === 0, `${emptyScope.status}`)

        const highBar = await recall(member.token, WID, { query: '退货政策 运费', minScore: 1, top: 10 })
        check('WK-S38c 阈值=1 时 0 命中', highBar.status === 200 && (highBar.json?.items ?? []).length === 0, `items=${(highBar.json?.items ?? []).length}`)

        const lowBar = await recall(member.token, WID, { query: '退货政策 运费', minScore: 0, top: 10 })
        check('WK-S38c 阈值=0 时不丢命中', lowBar.status === 200 && (lowBar.json?.items ?? []).length > 0, `${lowBar.status}`)

        const memberAgain = await recall(member.token, WID, { query: '退货政策 运费', top: 3 })
        check('WK-S38d 成员可正常召回（top=3）', memberAgain.status === 200 && (memberAgain.json?.items ?? []).length <= 3, `${memberAgain.status}`)
      }
    }
  } else {
    skip('WK-S38 向量召回/范围过滤/阈值（环境无可用 embedding 模型）')
  }

  // ===== @WK-S39：AI 优化问题与生成回答（依赖对话模型） =====
  if (hasEmb && hasConv && doc1Id) {
    const withOptimize = await recall(member.token, WID, {
      query: '你好，我想问一下你们家商品退货是怎么规定的呀？谢谢',
      top: 5,
      aiModelId: convModelId,
      isOptimizeQuery: true,
    })
    const optimized = String(withOptimize.json?.optimizedQuery ?? '')
    check('WK-S39a AI 优化问题 200 且返回优化文本', withOptimize.status === 200 && optimized !== '', `${withOptimize.status} optimized="${optimized.slice(0, 60)}"`)
    check('WK-S39a 优化后仍可召回', withOptimize.status === 200 && (withOptimize.json?.items ?? []).length > 0, `items=${(withOptimize.json?.items ?? []).length}`)
    check('WK-S39a 响应携带原始查询', String(withOptimize.json?.query ?? '') !== '', '')

    const withAnswer = await recall(member.token, WID, {
      query: '退货政策 运费',
      documentIds: [doc1Id],
      top: 5,
      aiModelId: convModelId,
      isAnswer: true,
    })
    const answer = String(withAnswer.json?.answer ?? '')
    check('WK-S39b AI 生成回答 200 且非空', withAnswer.status === 200 && answer !== '', `${withAnswer.status} answer="${answer.slice(0, 60)}"`)

    const bothOn = await recall(member.token, WID, {
      query: '问一下退货规定',
      top: 5,
      aiModelId: convModelId,
      isOptimizeQuery: true,
      isAnswer: true,
    })
    check('WK-S39c 优化+回答同时开启', bothOn.status === 200
      && String(bothOn.json?.optimizedQuery ?? '') !== ''
      && String(bothOn.json?.answer ?? '') !== '', `${bothOn.status}`)
  } else {
    skip('WK-S39 AI 优化问题/生成回答（环境无可用对话模型或 embedding 模型）')
  }

  // ===== @WK-S42：重排序接入与降级 + 相邻片段上下文（依赖 embedding + rerank 桩） =====
  const baseKey = (list) => (list ?? []).map((x) => `${x.documentId}#${x.chunkIndex}`)
  if (hasEmb && doc1Id && stubModels?.rerankModelId) {
    const baseline = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
    const baseItems = baseline.json?.items ?? []
    check('WK-S42 前置：基线召回 200 且 ≥2 命中', baseline.status === 200 && baseItems.length >= 2, `${baseline.status} items=${baseItems.length}`)
    check('WK-S42a 未绑定重排序时无 rerankScore', baseItems.every((x) => x.rerankScore == null), JSON.stringify(baseItems.map((x) => x.rerankScore)))

    const bindRerank = await api('PUT', `/api/wiki/${WID}/rerank-model`, {
      token: owner.token,
      body: { wikiId: WID, rerankModelId: stubModels.rerankModelId },
    })
    check('WK-S42 前置：绑定重排序模型 200', bindRerank.status === 200, `${bindRerank.status} ${bindRerank.text.slice(0, 120)}`)

    rerankMode = 'normal'
    const reranked = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
    const rrItems = reranked.json?.items ?? []
    check('WK-S42a 重排序召回 200 且带 rerankScore', reranked.status === 200 && rrItems.length > 0 && rrItems.every((x) => x.rerankScore != null), `${reranked.status} rerankScores=${JSON.stringify(rrItems.map((x) => x.rerankScore))}`)
    const rrScores = rrItems.map((x) => Number(x.rerankScore))
    check('WK-S42a 重排序得分降序', rrScores.every((s, i) => i === 0 || rrScores[i - 1] >= s), JSON.stringify(rrScores))
    check('WK-S42a 顺序与向量基线相反（桩反向打分）', baseKey(rrItems).join('|') === [...baseKey(baseItems)].reverse().join('|'), `base=${baseKey(baseItems).join(',')} reranked=${baseKey(rrItems).join(',')}`)

    // 上下文断言用小 top：top=10 时两文档全部切片都是主命中，邻居按去重设计不会重复出现
    const rerankedSmall = await recall(member.token, WID, { query: '退货政策 运费', top: 2 })
    const smItems = rerankedSmall.json?.items ?? []
    check('WK-S42b 小规模召回 200（top=2）', rerankedSmall.status === 200 && smItems.length === 2, `${rerankedSmall.status} items=${smItems.length}`)
    check('WK-S42b 命中项带切片序号', smItems.every((x) => Number.isInteger(x.chunkIndex) && x.chunkIndex >= 0), JSON.stringify(smItems.map((x) => x.chunkIndex)))
    check('WK-S42b 命中项带文档切片总数', smItems.every((x) => Number(x.documentChunkCount ?? 0) > 0), JSON.stringify(smItems.map((x) => x.documentChunkCount)))
    const withContext = smItems.filter((x) => (x.context ?? []).length > 0)
    check('WK-S42b 至少一个命中带相邻上下文', smItems.length > 0 && withContext.length > 0, `withContext=${withContext.length}/${smItems.length}`)
    const seenKeys = new Set()
    let dup = false
    for (const x of smItems) {
      const keys = [`${x.documentId}#${x.chunkIndex}`, ...(x.context ?? []).map((c) => `${x.documentId}#${c.chunkIndex}`)]
      for (const k of keys) {
        if (seenKeys.has(k)) dup = true
        seenKeys.add(k)
      }
    }
    check('WK-S42b 命中与上下文按片段去重', !dup, `unique=${seenKeys.size}`)
    check('WK-S42b 上下文为相邻片段（序号 ±1）', withContext.every((x) => (x.context ?? []).every((c) => Math.abs(c.chunkIndex - x.chunkIndex) === 1)), JSON.stringify(withContext[0]))
    check('WK-S42b 上下文片段非空', withContext.every((x) => (x.context ?? []).every((c) => String(c.content ?? '') !== '')), '')

    rerankMode = 'error'
    const degraded = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
    const dgItems = degraded.json?.items ?? []
    check('WK-S42c 重排序渠道 500 时召回仍 200', degraded.status === 200 && dgItems.length > 0, `${degraded.status}`)
    check('WK-S42c 降级后无 rerankScore', dgItems.every((x) => x.rerankScore == null), JSON.stringify(dgItems.map((x) => x.rerankScore)))
    check('WK-S42c 降级后保持向量召回顺序', baseKey(dgItems).join('|') === baseKey(baseItems).join('|'), `degraded=${baseKey(dgItems).join(',')}`)

    rerankMode = 'normal'
    const unbind = await api('PUT', `/api/wiki/${WID}/rerank-model`, { token: owner.token, body: { wikiId: WID, rerankModelId: null } })
    const unbound = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
    check('WK-S42d 解绑后无 rerankScore 且顺序恢复', unbind.status === 200
      && (unbound.json?.items ?? []).every((x) => x.rerankScore == null)
      && baseKey(unbound.json?.items ?? []).join('|') === baseKey(baseItems).join('|'), `${unbind.status}`)
  } else {
    skip('WK-S42 重排序接入/降级与相邻片段上下文（无桩 rerank 模型或未向量化）')
  }

  // ===== @WK-S49：同 chunk 多 MetadataType 变体按 ChunkId 去重（依赖 embedding + conversation 模型） =====
  if (hasEmb && hasConv && doc1Id && doc2Id) {
    const metaWf = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: owner.token,
      body: { wikiId: WID, documentIds: [doc1Id, doc2Id], isGenerateMetadata: true, metadataModelId: convModelId, isEmbedding: true, embedSourceText: true, embedMetadata: true },
    })
    check('WK-S49 前置：元数据生成+向量化提交 200', metaWf.status === 200, `${metaWf.status} ${metaWf.text.slice(0, 160)}`)
    const metaDone = await waitUntil(owner.token, WID, (docs) => docs.get(doc1Id)?.isEmbedding === true && docs.get(doc2Id)?.isEmbedding === true, 180000)
    check('WK-S49 前置：元数据向量化完成', metaDone.ok, '180s 内未完成')

    const dedupRecall = await recall(member.token, WID, { query: '退货政策 运费', top: 10, isKeywordSearch: false })
    const dedupItems = dedupRecall.json?.items ?? []
    const dedupIds = dedupItems.map((x) => String(x.chunkId))
    check('WK-S49a 同 chunk 多 MetadataType 变体只保留最高分（无重复切片）', dedupRecall.status === 200 && dedupIds.length > 0 && new Set(dedupIds).size === dedupIds.length, `items=${dedupIds.length} unique=${new Set(dedupIds).size}`)
  } else {
    skip('WK-S49 ChunkId 去重（无对话/embedding 模型）')
  }

  // ===== @WK-S51：关键词混合检索（原文 ILIKE 探针命中 + isKeywordHit 标记 + 阈值退化纯向量） =====
  if (hasEmb && doc1Id) {
    const d3 = await uploadWikiDoc(owner.token, WID, `recall-hybrid-${TS}.md`, makeMd('钛合金机甲骨架规格', '钛合金机甲骨架的屈服强度为 1280MPa，疲劳寿命 9000 小时，检修周期为 500 飞行小时'), 'text/markdown')
    doc3Id = d3.documentId
    check('WK-S51 前置：混合检索文档上传 200', Boolean(doc3Id), `${d3.ok}/${doc3Id}`)
    if (doc3Id) {
      const part3 = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc3Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
      const embed3 = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc3Id], isEmbedding: true, embedSourceText: true, embedMetadata: false } })
      check('WK-S51 前置：切割+向量化提交 200', part3.status === 200 && embed3.status === 200, `${part3.status}/${embed3.status}`)
      const done3 = await waitUntil(owner.token, WID, (docs) => docs.get(doc3Id)?.isEmbedding === true, 120000)
      check('WK-S51 前置：向量化完成', done3.ok, '120s 内未完成')

      if (done3.ok) {
        const hybrid = await recall(member.token, WID, { query: '机甲骨架 检修周期', top: 20 })
        const hybridItems = hybrid.json?.items ?? []
        const hybridHit = hybridItems.find((x) => String(x.content ?? '').includes('机甲骨架') && Number(x.documentId) === doc3Id)
        check('WK-S51a 关键词路径命中独特词内容', hybrid.status === 200 && hybridHit != null, `items=${hybridItems.length} hit=${hybridHit ? 'Y' : 'N'}`)
        check('WK-S51b 关键词命中带 isKeywordHit 标记', hybridHit != null && hybridHit.isKeywordHit === true, JSON.stringify(hybridHit?.isKeywordHit))

        const strict = await recall(member.token, WID, { query: '机甲骨架 检修周期', minScore: 1, top: 20 })
        check('WK-S51c 设置相似度阈值后退化为纯向量（阈值=1 时 0 命中）', strict.status === 200 && (strict.json?.items ?? []).length === 0, `items=${(strict.json?.items ?? []).length}`)
      }
    }
  } else {
    skip('WK-S51 关键词混合检索（无 embedding 模型）')
  }

  // ===== @WK-S52：过期索引检测（重切割未重嵌 → isStale=true；重新向量化后 → isStale=false） =====
  if (hasEmb && doc1Id) {
    const repart = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc1Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
    check('WK-S52 前置：重新切割提交 200', repart.status === 200, `${repart.status} ${repart.text.slice(0, 120)}`)
    const staleRecall = await recall(member.token, WID, { query: '退货政策 运费', documentIds: [doc1Id], top: 10 })
    const staleItems = staleRecall.json?.items ?? []
    check('WK-S52a 重切割后（向量已删）关键词兜底仍可命中', staleRecall.status === 200 && staleItems.length > 0, `items=${staleItems.length}`)
    check('WK-S52b 过期命中带 isStale=true', staleItems.every((x) => x.isStale === true), JSON.stringify(staleItems.map((x) => x.isStale)))

    const reembed = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc1Id], isEmbedding: true, embedSourceText: true, embedMetadata: false } })
    check('WK-S52 前置：重新向量化提交 200', reembed.status === 200, `${reembed.status}`)
    const reDone = await waitUntil(owner.token, WID, (docs) => docs.get(doc1Id)?.isEmbedding === true, 120000)
    check('WK-S52 前置：重新向量化完成', reDone.ok, '120s 内未完成')
    const freshRecall = await recall(member.token, WID, { query: '退货政策 运费', documentIds: [doc1Id], top: 10 })
    const freshItems = freshRecall.json?.items ?? []
    check('WK-S52c 重新向量化后 isStale=false', freshRecall.status === 200 && freshItems.length > 0 && freshItems.every((x) => x.isStale === false), JSON.stringify(freshItems.map((x) => x.isStale)))
  } else {
    skip('WK-S52 过期索引检测（无 embedding 模型）')
  }

  // ===== @WK-S53：检索经验沉淀与复用（同查询免 embedding 快速路径 + 来源变化自愈失效） =====
  if (hasEmb && doc1Id && doc2Id) {
    const queryD = '退货政策 运费说明'
    const c0 = embedCallCount
    const first = await recall(member.token, WID, { query: queryD, top: 10 })
    const firstItems = first.json?.items ?? []
    const firstKeys = firstItems.map((x) => `${x.documentId}#${x.chunkId}`)
    check('WK-S53a 首次检索走完整链路（embedding 被调用）', first.status === 200 && firstItems.length > 0 && embedCallCount > c0, `items=${firstItems.length} calls=${c0}->${embedCallCount}`)

    // 等待 MQ 消费者落库：重复检索直到计数稳定（连续两次零增长且命中一致即为复用快速路径）
    let reuseStable = false
    let reuseKeys = []
    let reuseCalls = embedCallCount
    for (let i = 0; i < 10 && !reuseStable; i++) {
      await new Promise((r) => setTimeout(r, 1500))
      const before = embedCallCount
      const again = await recall(member.token, WID, { query: queryD, top: 10 })
      const againItems = again.json?.items ?? []
      reuseKeys = againItems.map((x) => `${x.documentId}#${x.chunkId}`)
      reuseCalls = embedCallCount
      if (embedCallCount === before && reuseKeys.join('|') === firstKeys.join('|')) reuseStable = true
    }
    check('WK-S53b 复用快速路径：同查询免 embedding 且命中一致', reuseStable, `stable=${reuseStable} calls=${reuseCalls} keys=${reuseKeys.join(',').slice(0, 120)}`)
    const third = await recall(member.token, WID, { query: queryD, top: 10 })
    const thirdKeys = (third.json?.items ?? []).map((x) => `${x.documentId}#${x.chunkId}`)
    check('WK-S53c 复用稳定：再次召回仍零 embedding 且命中一致', embedCallCount === reuseCalls && thirdKeys.join('|') === firstKeys.join('|'), `calls=${reuseCalls}->${embedCallCount}`)

    // 新查询未沉淀过 → 完整检索（embedding +1）
    const cNew = embedCallCount
    const fresh = await recall(member.token, WID, { query: '退货政策 运费时效', top: 10 })
    check('WK-S53d 未沉淀的新查询走完整链路', fresh.status === 200 && (fresh.json?.items ?? []).length > 0 && embedCallCount > cNew, `calls=${cNew}->${embedCallCount}`)

    // 来源变化自愈：删除主要命中文档后，聚类存活数不足半数被删并回落完整检索
    if (doc1Id) {
      const delDoc1 = await api('DELETE', `/api/wiki/${WID}/documents`, { token: owner.token, body: { documentIds: [doc1Id] } })
      check('WK-S53 前置：删除主要命中文档 200', delDoc1.status === 200, `${delDoc1.status}`)
      await new Promise((r) => setTimeout(r, 500))
      const cHeal = embedCallCount
      const healed = await recall(member.token, WID, { query: queryD, top: 10 })
      const healedItems = healed.json?.items ?? []
      check('WK-S53e 来源变化后聚类失效回落完整检索（embedding +1）', healed.status === 200 && embedCallCount > cHeal, `calls=${cHeal}->${embedCallCount}`)
      check('WK-S53f 回落后命中不再包含已删文档', healedItems.every((x) => Number(x.documentId) !== doc1Id), `docs=${healedItems.map((x) => x.documentId).join(',')}`)
      doc1Id = null
    }
  } else {
    skip('WK-S53 检索经验沉淀与复用（无 embedding 模型）')
  }

  // ===== @WK-S54：内容级兜底检索（文档已提取未切割即可查；切割后由切片命中接管） =====
  if (hasEmb && doc2Id) {
    const d5 = await uploadWikiDoc(owner.token, WID, `recall-content-fallback-${TS}.md`, makeMd('深空探测器防热瓦规程', '深空探测器的防热瓦每 200 小时需进行无损探伤检查'), 'text/markdown')
    const doc5Id = d5.documentId
    check('WK-S54 前置：兜底检索文档上传 200', Boolean(doc5Id), `${d5.ok}/${doc5Id}`)
    if (doc5Id) {
      // 仅提取不切割：该文档无切片，切片路径与向量路径都搜不到
      const extracted = await api('POST', `/api/wiki/${WID}/documents/${doc5Id}/extract`, { token: owner.token, body: {} })
      check('WK-S54 前置：仅提取内容 200', extracted.status === 200, `${extracted.status} ${extracted.text.slice(0, 120)}`)

      const fallback = await recall(member.token, WID, { query: '防热瓦 无损探伤', top: 10 })
      const fallbackItems = fallback.json?.items ?? []
      const fallbackHit = fallbackItems.find((x) => Number(x.documentId) === doc5Id)
      check('WK-S54a 已提取未切割文档可被召回（内容兜底）', fallback.status === 200 && fallbackHit != null, `items=${fallbackItems.length} hit=${fallbackHit ? 'Y' : 'N'}`)
      check('WK-S54b 兜底命中带 isContentFallback 标记且无切片序号', fallbackHit != null
        && fallbackHit.isContentFallback === true
        && fallbackHit.isKeywordHit === true
        && fallbackHit.chunkIndex == null, JSON.stringify(fallbackHit?.isContentFallback))
      check('WK-S54c 兜底片段包含探针词', fallbackHit != null && String(fallbackHit.content ?? '').includes('防热瓦'), `content="${String(fallbackHit?.content ?? '').slice(0, 60)}"`)

      // 切割后切片候选非空，兜底不再触发，该文档由正常切片命中接管
      const part5 = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc5Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
      check('WK-S54 前置：切割提交 200', part5.status === 200, `${part5.status}`)
      const afterPart = await recall(member.token, WID, { query: '防热瓦 无损探伤', documentIds: [doc5Id], top: 10 })
      const afterItems = afterPart.json?.items ?? []
      check('WK-S54d 切割后由切片命中接管（兜底标记消失且有切片序号）', afterPart.status === 200
        && afterItems.length > 0
        && afterItems.every((x) => x.isContentFallback === false)
        && afterItems.every((x) => Number.isInteger(x.chunkIndex)), `items=${afterItems.length} fallback=${afterItems.filter((x) => x.isContentFallback).length}`)

      // 清理兜底检索文档
      await api('DELETE', `/api/wiki/${WID}/documents`, { token: owner.token, body: { documentIds: [doc5Id] } })
    }
  } else {
    skip('WK-S54 内容级兜底检索（无 embedding 模型）')
  }

  // ===== @WK-S55：切片标题锚点 + 自适应证据窗口 =====
  if (hasEmb && doc2Id) {
    const d6 = await uploadWikiDoc(owner.token, WID, `recall-anchor-${TS}.md`, makeMd('锚点验证文档', '锚点章节内容甲乙丙丁戊己庚辛壬癸', 30), 'text/markdown')
    const doc6Id = d6.documentId
    check('WK-S55 前置：锚点文档上传 200', Boolean(doc6Id), `${d6.ok}/${doc6Id}`)
    if (doc6Id) {
      const part6 = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc6Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
      const embed6 = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc6Id], isEmbedding: true, embedSourceText: true, embedMetadata: false } })
      check('WK-S55 前置：切割+向量化提交 200', part6.status === 200 && embed6.status === 200, `${part6.status}/${embed6.status}`)
      const done6 = await waitUntil(owner.token, WID, (docs) => docs.get(doc6Id)?.isEmbedding === true, 120000)
      check('WK-S55 前置：向量化完成', done6.ok, '120s 内未完成')

      if (done6.ok) {
        const anchorRecall = await recall(member.token, WID, { query: '锚点章节 内容甲乙', documentIds: [doc6Id], top: 10 })
        const anchorItems = anchorRecall.json?.items ?? []
        const docItems = anchorItems.filter((x) => Number(x.documentId) === doc6Id)
        check('WK-S55a 命中带非空标题锚点且含文档标题', anchorRecall.status === 200 && docItems.length > 0
          && docItems.every((x) => String(x.anchor ?? '') !== '')
          && docItems.every((x) => String(x.anchor).includes('锚点验证文档')), `anchors=${JSON.stringify(docItems.map((x) => x.anchor))}`)

        // 自适应证据窗口：contextWindow=2 时存在距离命中片段 ±2 的上下文（默认窗口 1 只有 ±1）
        const wide = await recall(member.token, WID, { query: '锚点章节 内容甲乙', documentIds: [doc6Id], top: 1, contextWindow: 2 })
        const wideHit = (wide.json?.items ?? [])[0]
        const wideSpan = (wideHit?.context ?? []).map((c) => Math.abs((c.chunkIndex ?? 0) - (wideHit?.chunkIndex ?? 0)))
        check('WK-S55b 上下文窗口=2 出现距离 ±2 的相邻片段', wide.status === 200 && wideHit != null && wideSpan.some((d) => d === 2), `span=${JSON.stringify(wideSpan)}`)
        check('WK-S55c 窗口=2 上下文不超过 ±2', wideHit != null && wideSpan.every((d) => d <= 2), `span=${JSON.stringify(wideSpan)}`)
      }

      // 清理锚点文档
      await api('DELETE', `/api/wiki/${WID}/documents`, { token: owner.token, body: { documentIds: [doc6Id] } })
    }
  } else {
    skip('WK-S55 切片标题锚点与自适应证据窗口（无 embedding 模型）')
  }

  // ===== @WK-S48：删除文档向量生命周期闭环 =====
  if (hasEmb && doc2Id) {
    const delDoc = await api('DELETE', `/api/wiki/${WID}/documents`, { token: owner.token, body: { documentIds: [doc2Id] } })
    check('WK-S48 前置：删除文档 200', delDoc.status === 200, `${delDoc.status} ${delDoc.text.slice(0, 120)}`)
    const afterDel = await recall(member.token, WID, { query: '发票 开具 电子发票', top: 10 })
    const afterDelItems = afterDel.json?.items ?? []
    check('WK-S48a 删除文档后召回不再命中（向量同步清理）', afterDel.status === 200 && afterDelItems.every((x) => Number(x.documentId) !== doc2Id), `items=${afterDelItems.map((x) => x.documentId).join(',')}`)

    // 删除后重建同内容文档：召回指向新文档的新切片
    const d4 = await uploadWikiDoc(owner.token, WID, `recall-invoice-rebuild-${TS}.md`, makeMd('发票开具流程', '发票在订单完成后 30 天内开具，支持电子发票和纸质发票，抬头信息需准确'), 'text/markdown')
    const doc4Id = d4.documentId
    if (doc4Id) {
      await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc4Id], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
      await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [doc4Id], isEmbedding: true, embedSourceText: true, embedMetadata: false } })
      const rebuilt = await waitUntil(owner.token, WID, (docs) => docs.get(doc4Id)?.isEmbedding === true, 120000)
      const rebuildRecall = await recall(member.token, WID, { query: '发票 开具 电子发票', top: 10 })
      const rebuildItems = rebuildRecall.json?.items ?? []
      check('WK-S48b 重建文档召回正常且指向新文档', rebuilt.ok && rebuildRecall.status === 200
        && rebuildItems.some((x) => Number(x.documentId) === doc4Id)
        && rebuildItems.every((x) => Number(x.documentId) !== doc2Id), `items=${rebuildItems.map((x) => `${x.documentId}#${x.chunkId}`).join(',')}`)
    }
  } else {
    skip('WK-S48 删除向量生命周期（无 embedding 模型）')
  }

  if (stub) {
    await cleanupStubModels(adminToken, channelId)
    stub.close()
  }

  console.log(`\nRESULT | PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常退出:', e)
  process.exit(1)
})
