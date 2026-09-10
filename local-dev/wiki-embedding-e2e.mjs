// 知识库文档向量化 E2E（场景 @WK-S15 / @WK-S16 / @WK-S19 / @WK-S21；依赖 wiki + 存储上传链路；后端 127.0.0.1:5210）
// 覆盖：上传即自动提取内容入库 → 普通切割 → 向量化 全流程
// （内容在 CompleteWikiDocument 后自动入库 wiki_document_content；切片入库 wiki_document_chunk_content）。
import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5210'
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

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

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
  if (pre.status !== 200) {
    return { ok: false, step: 'preupload', res: pre }
  }
  const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: content })
  if (put.status !== 200) {
    return { ok: false, step: 'put', res: { status: put.status } }
  }
  const complete = await api('POST', `/api/wiki/${wikiId}/documents/complete`, {
    token,
    body: { wikiId, isSuccess: true, fileId: pre.json.fileId, fileName },
  })
  if (complete.status !== 200) {
    return { ok: false, step: 'complete', res: complete }
  }
  const list = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const item = (list.json?.items ?? []).find((i) => Number(i.fileId) === Number(pre.json.fileId))
  return { ok: true, fileId: pre.json.fileId, documentId: item ? Number(item.documentId) : null }
}

async function bindFirstEmbeddingModel(token, teamId, wikiId) {
  const opt = await api('GET', `/api/wiki/model-options?teamId=${teamId}`, { token })
  const first = (opt.json?.embeddingModels ?? [])[0]
  if (!first) return { ok: false, reason: 'no embedding model option' }
  const r = await api('PUT', `/api/wiki/${wikiId}/embedding-config`, {
    token,
    body: { wikiId, embeddingModelId: first.id, embeddingDimensions: 1024 },
  })
  if (r.status !== 200) return { ok: false, res: r }
  return { ok: true, modelId: first.id, modelName: first.name }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('eo')
  const member = await mkuser('em')
  const outsider = await mkuser('ex')

  // 准备团队 + 知识库
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'emb-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 2 } })
  const WID = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'emb-wiki-' + TS, description: 'embedding e2e' } })).json.value)

  const bind = await bindFirstEmbeddingModel(owner.token, TID, WID)
  check('WK-S15/WK-S16 前置：绑定向量模型 200', bind.ok, bind.reason || bind.res?.text?.slice(0, 120))

  // 上传一个 .md 文件：上传完成即自动提取内容入库（不再需要手动提取）
  const mdName = `demo${TS}${seq}.md`
  const mdContent = Buffer.from(
    `# 知识库向量化测试\n\n## 概述\n\n` +
      '这是一段用于验证 Maomi.ToMarkdown 抽取与入库的中文文本。'.repeat(40) +
      `\n\n## 步骤\n\n1. 上传文件\n2. 自动提取内容入库\n3. 普通切割\n4. 向量化\n5. 验证内容与切片已入库\n`,
    'utf8',
  )
  const up = await uploadWikiDoc(member.token, WID, mdName, mdContent, 'text/markdown')
  check('WK-S19 前置：上传 .md 文档 200', up.ok && up.documentId > 0, up.ok ? '' : `${up.step} ${up.res?.status ?? ''}`)
  const DID = up.documentId
  if (!DID) {
    console.log('\n===== 向量化 E2E 中止：未获取到 documentId =====')
    process.exit(FAIL > 0 ? 1 : 0)
  }

  // ===== WK-S19：上传后内容已自动提取入库（无需手动提取） =====
  {
    const r = await api('GET', `/api/wiki/${WID}/documents/${DID}/embedding`, { token: member.token })
    check('WK-S19a 上传后 isContentExtracted=true（自动提取入库）', r.status === 200 && r.json?.isContentExtracted === true, `isContentExtracted=${r.json?.isContentExtracted} contentLength=${r.json?.contentLength}`)
  }

  // ===== WK-S21：已提取但未切割直接向量化 409 =====
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/embedding`, {
      token: member.token,
      body: { wikiId: WID, documentId: DID, metadataModelId: '00000000-0000-0000-0000-000000000000', isEmbedSourceText: true, isEmbedMetadata: true },
    })
    check('WK-S21a 未切割向量化 409', r.status === 409, `${r.status} ${r.text.slice(0, 120)}`)
  }

  // ===== WK-S15：普通切割缺切片参数 400 =====
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/partition`, {
      token: member.token,
      body: { wikiId: WID, documentId: DID, chunkOverlap: 64 },
    })
    check('WK-S15a 缺 chunkSize 400', r.status === 400, `${r.status} ${r.text.slice(0, 120)}`)
  }
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/partition`, {
      token: member.token,
      body: { wikiId: WID, documentId: DID, chunkSize: 512, chunkOverlap: 9999 },
    })
    check('WK-S15b chunkOverlap >= chunkSize 400', r.status === 400, `${r.status} ${r.text.slice(0, 120)}`)
  }

  // ===== WK-S16：合法切割 + 向量化 200 =====
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/partition`, {
      token: member.token,
      body: { wikiId: WID, documentId: DID, splitMode: 'recursive', chunkSize: 256, chunkOverlap: 1, overlapUnit: 'sentence', sizeUnit: 'token', tokenEncodingOrModel: 'cl100k_base' },
    })
    check('WK-S16a 普通切割 200（recursive + token）', r.status === 200, `${r.status} ${r.text.slice(0, 120)}`)
  }

  let metaModelId = '00000000-0000-0000-0000-000000000000'
  const opt = await api('GET', `/api/wiki/model-options?teamId=${TID}`, { token: owner.token })
  const conv = (opt.json?.conversationModels ?? [])[0]
  if (conv?.id) metaModelId = conv.id
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/embedding`, {
      token: member.token,
      body: { wikiId: WID, documentId: DID, metadataModelId: metaModelId, isEmbedSourceText: true, isEmbedMetadata: true },
    })
    check('WK-S16b 触发向量化 200（不再携带切片参数）', r.status === 200, `${r.status} ${r.text.slice(0, 120)}`)
  }

  // ===== WK-S19：异步等待并验证切片 + 向量内容 =====
  let embedded = false
  let detailRes = null
  const start = Date.now()
  while (Date.now() - start < 30000) {
    detailRes = await api('GET', `/api/wiki/${WID}/documents/${DID}/embedding`, { token: member.token })
    if (detailRes.status === 200 && (detailRes.json?.embeddingCount ?? 0) > 0) {
      embedded = true
      break
    }
    await new Promise((r) => setTimeout(r, 1000))
  }
  check('WK-S19c 30s 内 embeddingCount > 0', embedded, embedded ? `embeddingCount=${detailRes.json?.embeddingCount}` : detailRes?.text?.slice(0, 120))

  if (detailRes?.status === 200) {
    const items = detailRes.json?.items ?? []
    const sliceOrders = items.map((x) => Number(x.sliceOrder ?? 0))
    check('WK-S19d 切片顺序 1..N 自增', items.length >= 1 && sliceOrders[0] === 1 && sliceOrders.every((o, i) => i === 0 || o === sliceOrders[i - 1] + 1), `items=${items.length} orders=${sliceOrders.join(',')}`)
    check('WK-S19e 切片内容含原 markdown 片段', items.some((x) => typeof x.sliceContent === 'string' && x.sliceContent.includes('知识库向量化测试')), '未匹配到原标题片段')
    check('WK-S19f wiki embedding 模型未变更', detailRes.json?.embeddingModelId === bind.modelId, `before=${bind.modelId} after=${detailRes.json?.embeddingModelId}`)
  }

  // ===== WK-S15c：非成员触发 404 =====
  {
    const r = await api('POST', `/api/wiki/${WID}/documents/${DID}/embedding`, {
      token: outsider.token,
      body: { wikiId: WID, documentId: DID, metadataModelId: metaModelId, isEmbedSourceText: true, isEmbedMetadata: true },
    })
    check('WK-S15c 非成员触发 404', r.status === 404, `${r.status} ${r.text.slice(0, 120)}`)
  }

  console.log(`\n===== 向量化 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
