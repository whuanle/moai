// 知识库向量化模型迁移 E2E（场景 @WK-S50；后端 127.0.0.1:5210，可用 argv[2] 覆盖）
// 链路：桩 embedding（按请求 dimensions 出确定性哈希向量，slow 档故意放慢撑开迁移窗口）→
// 建库绑 1024 维模型并向量召回基线 → 迁移发起校验（403/400/409）→ 迁移窗口内写阻断（切割/向量化 409）
// → 进度轮询至成功 → 配置切换与新维度召回 → 死渠道迁移失败自动回滚（原向量不受影响仍可召回）。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'

const check = (name, cond, detail = '') => {
  if (cond) { console.log(`PASS | ${name}`); PASS++ } else { console.log(`FAIL | ${name} — ${detail}`); FAIL++ }
}
const skip = (name, reason = '') => { console.log(`SKIP | ${name} ${reason}`); SKIP++ }
let PASS = 0
let FAIL = 0
let SKIP = 0

let RSA_KEY = ''

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

const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function adminLogin() {
  try {
    const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    return l.status === 200 ? l.json.accessToken : null
  } catch {
    return null
  }
}

// ===== 桩模型：embedding 按请求 dimensions 出确定性哈希向量；模型名含 slow 时每请求延迟 1.2s 撑开迁移窗口 =====
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
    req.on('end', async () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      if (String(req.url).includes('/embeddings')) {
        const model = String(payload.model ?? '')
        if (model.includes('slow')) await new Promise((r) => setTimeout(r, 1200))
        const inputs = Array.isArray(payload.input) ? payload.input : [String(payload.input ?? '')]
        const dims = Math.max(1, Math.min(Number(payload.dimensions) || 1024, 2000))
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({
          object: 'list',
          model,
          data: inputs.map((text, index) => ({ object: 'embedding', index, embedding: stubEmbed(text, dims) })),
          usage: { prompt_tokens: 10, total_tokens: 10 },
        }))
        return
      }

      res.writeHead(404, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ error: 'stub: not found' }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

const makeMd = (title, keywords, words = 8) => Buffer.from(
  `# ${title}\n\n## 说明\n\n` +
  `本文用于验证知识库向量化模型迁移。${keywords}。`.repeat(words) +
  `\n\n## 要点\n\n1. ${keywords}是本文核心主题\n2. 如需了解更多请联系管理员\n`,
  'utf8',
)

async function uploadWikiDoc(token, wikiId, fileName, content, contentType) {
  const sha256 = crypto.createHash('sha256').update(content).digest('hex')
  const pre = await api('POST', `/api/wiki/${wikiId}/documents/preupload`, { token, body: { wikiId, fileName, contentType, fileSize: content.length, sha256 } })
  if (pre.status !== 200) return { ok: false, res: pre }
  const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: content })
  if (put.status !== 200) return { ok: false, res: { status: put.status, text: '' } }
  const complete = await api('POST', `/api/wiki/${wikiId}/documents/complete`, { token, body: { wikiId, isSuccess: true, fileId: pre.json.fileId, fileName } })
  if (complete.status !== 200) return { ok: false, res: complete }
  const list = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const item = (list.json?.items ?? []).find((i) => Number(i.fileId) === Number(pre.json.fileId))
  return { ok: true, documentId: item ? Number(item.documentId) : null }
}

async function listDocs(token, wikiId) {
  const r = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const map = new Map()
  for (const item of r.json?.items ?? []) map.set(Number(item.documentId), item)
  return { ok: r.status === 200, map }
}

async function waitUntil(token, wikiId, predicate, timeoutMs = 120000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const { ok, map } = await listDocs(token, wikiId)
    if (ok && predicate(map)) return { ok: true }
    await new Promise((r) => setTimeout(r, 2000))
  }
  return { ok: false }
}

const migrationStatus = (token, wikiId) => api('GET', `/api/wiki/${wikiId}/model-migration`, { token })

async function pollMigration(token, wikiId, predicate, timeoutMs = 120000) {
  const deadline = Date.now() + timeoutMs
  let last = null
  while (Date.now() < deadline) {
    last = await migrationStatus(token, wikiId)
    if (last.status === 200 && predicate(last.json ?? {})) return { ok: true, last }
    await new Promise((r) => setTimeout(r, 1000))
  }
  return { ok: false, last }
}

/** 创建渠道 + 一个 embedding 模型并授权给团队，返回模型 id 或 '' */
async function createEmbedModel(adminToken, TID, channelName, baseUrl, modelName, delayNote = '') {
  const crc = await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: channelName, protocolFamily: 'openaiChatCompletions', baseUrl, apiKey: 'migration-stub-key', enabled: true, description: `wiki 迁移 E2E 桩渠道${delayNote}` },
  })
  if (crc.status !== 200) { console.log(`INFO | 渠道 ${channelName} 创建失败：${crc.status} ${crc.text.slice(0, 120)}`); return { channelId: '', modelId: '' } }
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const channelId = String((channels.json?.items ?? []).find((c) => c.name === channelName)?.id ?? '')
  if (!channelId) return { channelId: '', modelId: '' }

  const mrc = await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId, meta: { modelId: modelName, name: modelName, modelKind: 'embedding', description: 'wiki 迁移 E2E 桩模型' }, enabled: true, isPublic: false },
  })
  if (mrc.status !== 200) { console.log(`INFO | 模型 ${modelName} 创建失败：${mrc.status}`); return { channelId, modelId: '' } }
  const models = await api('GET', `/api/ai/model?channelId=${channelId}`, { token: adminToken })
  const modelId = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  if (!modelId) return { channelId, modelId: '' }

  const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  return { channelId, modelId }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('mo')
  const member = await mkuser('mm')

  const teamRes = await api('POST', '/api/team', { token: owner.token, body: { name: 'migration-team-' + TS } })
  check('WK-S50 前置：创建团队 200', teamRes.status === 200 && Number(teamRes.json?.value) > 0, `${teamRes.status}`)
  const TID = Number(teamRes.json?.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const wikiRes = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'mig_wiki_' + TS, description: 'migration e2e' } })
  const WID = Number(wikiRes.json?.value)
  check('WK-S50 前置：创建知识库 200', wikiRes.status === 200 && WID > 0, `${wikiRes.status} ${wikiRes.text.slice(0, 160)}`)

  const adminToken = await adminLogin()
  if (!adminToken) {
    skip('WK-S50 全场景（种子管理员不可用）')
    process.exit(FAIL > 0 ? 1 : 0)
    return
  }

  const stub = await startStubServer()
  const stubPort = stub.address().port

  // 快档（1024）/慢档（256，撑开迁移窗口）/死渠道（迁移失败回滚用）
  const fast = await createEmbedModel(adminToken, TID, `mig-fast渠道-${TS}`, `http://127.0.0.1:${stubPort}/v1`, `mig-embed-fast-${TS}`)
  const slow = await createEmbedModel(adminToken, TID, `mig-slow渠道-${TS}`, `http://127.0.0.1:${stubPort}/v1`, `mig-embed-slow-${TS}`, '（slow 档）')
  const dead = await createEmbedModel(adminToken, TID, `mig-dead渠道-${TS}`, 'http://127.0.0.1:1/v1', `mig-embed-dead-${TS}`)
  const okModels = fast.modelId && slow.modelId && dead.modelId
  if (!okModels) {
    skip('WK-S50 全场景（桩模型创建失败）')
    await cleanup(adminToken, owner, WID, [fast.channelId, slow.channelId, dead.channelId], stub)
    process.exit(FAIL > 0 ? 1 : 0)
    return
  }

  const bind = await api('PUT', `/api/wiki/${WID}/embedding-config`, { token: owner.token, body: { wikiId: WID, embeddingModelId: fast.modelId, embeddingDimensions: 1024 } })
  check('WK-S50 前置：绑定向量模型 200', bind.status === 200, `${bind.status} ${bind.text.slice(0, 120)}`)

  const d1 = await uploadWikiDoc(owner.token, WID, `migration-refund-${TS}.md`, makeMd('退货政策说明', '商品签收后 7 天内可无理由退货，运费由买家承担'), 'text/markdown')
  const d2 = await uploadWikiDoc(owner.token, WID, `migration-invoice-${TS}.md`, makeMd('发票开具流程', '发票在订单完成后 30 天内开具'), 'text/markdown')
  check('WK-S50 前置：两文档上传 200', d1.ok && d2.ok && d1.documentId > 0 && d2.documentId > 0, `${d1.ok}/${d1.documentId} ${d2.ok}/${d2.documentId}`)

  let baselineVectors = 0
  if (d1.ok && d2.ok) {
    const part = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [d1.documentId, d2.documentId], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
    const embed = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, { token: owner.token, body: { wikiId: WID, documentIds: [d1.documentId, d2.documentId], isEmbedding: true, embedSourceText: true, embedMetadata: false } })
    check('WK-S50 前置：切割+向量化提交 200', part.status === 200 && embed.status === 200, `${part.status}/${embed.status}`)
    const done = await waitUntil(owner.token, WID, (docs) => docs.get(d1.documentId)?.isEmbedding === true && docs.get(d2.documentId)?.isEmbedding === true, 240000)
    check('WK-S50 前置：向量化完成', done.ok, '240s 内未完成')

    const baseline = await api('POST', `/api/wiki/${WID}/recall-test`, { token: owner.token, body: { wikiId: WID, query: '退货政策 运费', top: 10 } })
    check('WK-S50 前置：迁移前召回有命中', baseline.status === 200 && (baseline.json?.items ?? []).length > 0, `${baseline.status} items=${(baseline.json?.items ?? []).length}`)

    const before = await migrationStatus(owner.token, WID)
    baselineVectors = Number(before.json?.totalVectorCount ?? 0)
    check('WK-S50a 迁移状态回显当前模型/维度/向量数', before.status === 200
      && before.json?.currentModelId === fast.modelId
      && Number(before.json?.currentDimensions) === 1024
      && baselineVectors > 0
      && before.json?.activeMigration == null, `model=${before.json?.currentModelId} dims=${before.json?.currentDimensions} vectors=${baselineVectors}`)

    // ===== @WK-S50：发起校验（403 / 400 / 409） =====
    const memberStart = await api('POST', `/api/wiki/${WID}/model-migration`, { token: member.token, body: { wikiId: WID, modelId: slow.modelId, dimensions: 256 } })
    check('WK-S50b Member 发起迁移 403', memberStart.status === 403, `${memberStart.status}`)
    const sameStart = await api('POST', `/api/wiki/${WID}/model-migration`, { token: owner.token, body: { wikiId: WID, modelId: fast.modelId, dimensions: 1024 } })
    check('WK-S50c 与当前模型/维度一致 400', sameStart.status === 400, `${sameStart.status}`)
    const ghostStart = await api('POST', `/api/wiki/${WID}/model-migration`, { token: owner.token, body: { wikiId: WID, modelId: crypto.randomUUID(), dimensions: 512 } })
    check('WK-S50d 目标模型不可用 400', ghostStart.status === 400, `${ghostStart.status}`)

    // ===== @WK-S50：迁移全链路（慢档撑开窗口） =====
    const start = await api('POST', `/api/wiki/${WID}/model-migration`, { token: owner.token, body: { wikiId: WID, modelId: slow.modelId, dimensions: 256 } })
    check('WK-S50e 发起迁移 200 并返回任务 id', start.status === 200 && !!start.json?.taskId, `${start.status} ${start.text.slice(0, 120)}`)

    if (start.status === 200) {
      const dup = await api('POST', `/api/wiki/${WID}/model-migration`, { token: owner.token, body: { wikiId: WID, modelId: slow.modelId, dimensions: 256 } })
      check('WK-S50f 并发二次发起 409', dup.status === 409, `${dup.status}`)

      // 迁移窗口内写阻断：切割与向量化均 409
      const blockedPart = await api('POST', `/api/wiki/${WID}/documents/${d1.documentId}/partition`, { token: owner.token, body: { wikiId: WID, documentId: d1.documentId, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' } })
      check('WK-S50g 迁移期间切割 409', blockedPart.status === 409, `${blockedPart.status} ${blockedPart.text.slice(0, 120)}`)
      const blockedEmbed = await api('POST', `/api/wiki/${WID}/documents/${d1.documentId}/embedding`, { token: owner.token, body: { wikiId: WID, documentId: d1.documentId, isEmbedSourceText: true, isEmbedMetadata: false } })
      check('WK-S50g 迁移期间向量化 409', blockedEmbed.status === 409, `${blockedEmbed.status} ${blockedEmbed.text.slice(0, 120)}`)

      const processing = await migrationStatus(owner.token, WID)
      check('WK-S50h 迁移中状态可见（等待/处理中）', processing.status === 200
        && [1, 2].includes(Number(processing.json?.activeMigration?.state)), `state=${processing.json?.activeMigration?.state}`)

      const success = await pollMigration(owner.token, WID, (s) => Number(s.activeMigration?.state) === 4, 120000)
      check('WK-S50i 迁移任务成功终态', success.ok, `state=${success.last?.json?.activeMigration?.state} msg=${success.last?.json?.activeMigration?.message}`)
      const task = success.last?.json?.activeMigration
      check('WK-S50j 进度回显：重建条数与向量总数一致', success.ok && Number(task?.totalCount) === baselineVectors && Number(task?.migratedCount) === baselineVectors, `total=${task?.totalCount} migrated=${task?.migratedCount} baseline=${baselineVectors}`)

      const after = await migrationStatus(owner.token, WID)
      check('WK-S50k 迁移后当前配置切到目标模型/维度', after.status === 200
        && after.json?.currentModelId === slow.modelId
        && Number(after.json?.currentDimensions) === 256, `model=${after.json?.currentModelId} dims=${after.json?.currentDimensions}`)

      const detail = await api('GET', `/api/wiki/${WID}`, { token: owner.token })
      check('WK-S50l 知识库详情配置已切换且仍锁定', detail.status === 200
        && detail.json?.embeddingModelId === slow.modelId
        && Number(detail.json?.embeddingDimensions) === 256
        && detail.json?.isLock === true, `model=${detail.json?.embeddingModelId} dims=${detail.json?.embeddingDimensions} lock=${detail.json?.isLock}`)

      const locked = await api('PUT', `/api/wiki/${WID}/embedding-config`, { token: owner.token, body: { wikiId: WID, embeddingModelId: slow.modelId, embeddingDimensions: 256 } })
      check('WK-S50m 锁定态保存配置 409（提示走迁移端点）', locked.status === 409, `${locked.status} ${locked.text.slice(0, 120)}`)

      const migrated = await api('POST', `/api/wiki/${WID}/recall-test`, { token: owner.token, body: { wikiId: WID, query: '退货政策 运费', top: 10 } })
      check('WK-S50n 迁移后按新维度召回有命中', migrated.status === 200 && (migrated.json?.items ?? []).length > 0, `${migrated.status} items=${(migrated.json?.items ?? []).length}`)

      // ===== @WK-S50：死渠道迁移失败自动回滚 =====
      const failStart = await api('POST', `/api/wiki/${WID}/model-migration`, { token: owner.token, body: { wikiId: WID, modelId: dead.modelId, dimensions: 512 } })
      check('WK-S50o 死渠道迁移可发起 200', failStart.status === 200, `${failStart.status}`)
      if (failStart.status === 200) {
        const failed = await pollMigration(owner.token, WID, (s) => Number(s.activeMigration?.state) === 5, 60000)
        check('WK-S50p 死渠道迁移失败终态且提示已回滚', failed.ok && String(failed.last?.json?.activeMigration?.message ?? '').includes('回滚'), `state=${failed.last?.json?.activeMigration?.state} msg=${failed.last?.json?.activeMigration?.message}`)

        const rollbackStatus = await migrationStatus(owner.token, WID)
        check('WK-S50q 失败后配置保持不变', rollbackStatus.status === 200
          && rollbackStatus.json?.currentModelId === slow.modelId
          && Number(rollbackStatus.json?.currentDimensions) === 256, `model=${rollbackStatus.json?.currentModelId} dims=${rollbackStatus.json?.currentDimensions}`)

        const still = await api('POST', `/api/wiki/${WID}/recall-test`, { token: owner.token, body: { wikiId: WID, query: '退货政策 运费', top: 10 } })
        check('WK-S50r 回滚后原向量仍可召回', still.status === 200 && (still.json?.items ?? []).length > 0, `${still.status} items=${(still.json?.items ?? []).length}`)
      }
    }
  } else {
    skip('WK-S50 迁移全链路（文档上传失败）')
  }

  await cleanup(adminToken, owner, WID, [fast.channelId, slow.channelId, dead.channelId], stub)

  console.log(`\n==== Wiki 模型迁移 E2E：PASS ${PASS} / FAIL ${FAIL} / SKIP ${SKIP} ====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

async function cleanup(adminToken, owner, wikiId, channelIds, stub) {
  if (wikiId > 0) await api('DELETE', `/api/wiki/${wikiId}`, { token: owner.token })
  for (const channelId of channelIds) {
    if (channelId && adminToken) await api('DELETE', `/api/ai/channel/${channelId}`, { token: adminToken })
  }
  if (stub) await new Promise((r) => stub.close(r))
}

main().catch((e) => {
  console.error('E2E 异常退出:', e)
  process.exit(1)
})
