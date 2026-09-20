// 知识库默认工作流 + 批量处理 E2E（场景 @WK-S27 ~ @WK-S34；后端 127.0.0.1:5210）
// 覆盖：默认工作流配置（权限/保存/回读/校验）→ 批量全流程（切割+元数据+向量化）
//       → 单步执行（只切割 / 只生成元数据 / 只向量化）→ 错误隔离与参数校验。
// 说明：元数据生成/向量化为异步任务（WorkerTask + MQ）。若团队当前无可用模型，脚本会用
//       种子管理员（admin）通过模型授权 API 将团队加入已启用模型（合并授权，不做全量覆盖）；
//       若自举失败，依赖模型的场景标记为 SKIP（不计入 FAIL）。
// 注意：若存在其他旧版本后端实例竞争同一 MQ 队列，带元数据的任务可能被旧实例误处理，失败时可重跑验证。
import crypto from 'node:crypto'

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
    return { ok: false, step: 'put', res: { status: put.status, text: '' } }
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

const makeMd = (title, words) => Buffer.from(
  `# ${title}\n\n## 概述\n\n` +
  `这是用于验证知识库批量工作流的中文文本，主题为 ${title}。`.repeat(words) +
  `\n\n## 步骤\n\n1. 上传\n2. 切割\n3. 元数据\n4. 向量化\n`,
  'utf8',
)

async function listDocs(token, wikiId) {
  const r = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { wikiId, pageNo: 1, pageSize: 50 } })
  const map = new Map()
  for (const item of r.json?.items ?? []) map.set(Number(item.documentId), item)
  return map
}

async function waitUntil(token, wikiId, predicate, timeoutMs = 60000) {
  const start = Date.now()
  let docs = null
  while (Date.now() - start < timeoutMs) {
    docs = await listDocs(token, wikiId)
    if (predicate(docs)) return { ok: true, docs }
    await new Promise((r) => setTimeout(r, 1500))
  }
  return { ok: false, docs }
}

/** 团队无可用模型时，用种子管理员把团队加入已启用的 embedding/对话模型授权（合并，不覆盖既有授权） */
async function bootstrapTeamModels(TID) {
  let adminToken = null
  try {
    const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    if (l.status !== 200) return { ok: false, reason: `admin 登录失败 ${l.status}` }
    adminToken = l.json.accessToken
  } catch (e) {
    return { ok: false, reason: `admin 登录异常 ${e.message}` }
  }

  const list = await api('GET', '/api/ai/model', { token: adminToken })
  if (list.status !== 200) return { ok: false, reason: `模型列表 ${list.status}` }
  const items = list.json?.items ?? []
  const enabledOf = (kind) => items.filter((x) => x.enabled === true && x.modelKind === kind)
  // 每类授权多个候选（部分模型不支持 dimensions / 不返回 JSON，由探针挑选可用项）
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

/** 探针：从候选 embedding 模型中挑出真正可用的（部分模型不支持 dimensions 参数） */
async function pickWorkingEmbeddingModel(owner, WID, probeDocId, candidates) {
  for (const model of candidates) {
    const bind = await api('PUT', `/api/wiki/${WID}/embedding-config`, {
      token: owner.token,
      body: { wikiId: WID, embeddingModelId: model.id, embeddingDimensions: 1024 },
    })
    if (bind.status !== 200) { console.log(`INFO | embedding 探针跳过（绑定失败）${model.name}: ${bind.status}`); continue }
    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: owner.token,
      body: { wikiId: WID, documentIds: [probeDocId], isEmbedding: true, embedSourceText: true, embedMetadata: false },
    })
    if (batch.status !== 200) { console.log(`INFO | embedding 探针跳过（批量失败）${model.name}: ${batch.status}`); continue }
    const item = (batch.json?.items ?? [])[0]
    if (item?.success !== true) { console.log(`INFO | embedding 探针跳过（前置不满足）${model.name}: ${item?.message}`); continue }
    const done = await waitUntil(owner.token, WID, (docs) => docs.get(probeDocId)?.isEmbedding === true, 60000)
    if (done.ok) return model
    console.log(`INFO | embedding 探针失败 ${model.name}: 60s 内未完成向量化`)
  }
  return null
}

/** 探针：从候选对话模型中挑出生成元数据可用的（用同步元数据接口验证，绕开 MQ 与实例竞争） */
async function pickWorkingConversationModel(member, WID, probeDocId, candidates) {
  for (const model of candidates) {
    const r = await api('POST', `/api/wiki/${WID}/documents/${probeDocId}/chunks/metadata/generate`, {
      token: member.token,
      body: { wikiId: WID, documentId: probeDocId, metadataModelId: model.id, chunkIds: [], appendExisting: false },
    })
    if (r.status === 200 && Number(r.json?.value ?? 0) > 0) return model
    console.log(`INFO | conversation 探针失败 ${model.name}: ${r.status} ${r.text.slice(0, 120)}`)
  }
  return null
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('wo')
  const member = await mkuser('wm')
  const outsider = await mkuser('wx')

  const teamRes = await api('POST', '/api/team', { token: owner.token, body: { name: 'wf-team-' + TS } })
  check('WK-S28 前置：创建团队 200', teamRes.status === 200 && Number(teamRes.json?.value) > 0, `${teamRes.status} ${teamRes.text.slice(0, 120)}`)
  const TID = Number(teamRes.json?.value)
  const addMember = await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  check('WK-S28 前置：添加成员 200', addMember.status === 200, `${addMember.status} ${addMember.text.slice(0, 120)}`)
  const WID = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'wf-wiki-' + TS, description: 'workflow e2e' } })).json.value)

  // 模型可用性：优先读取现有公开/已授权模型，缺失时自举授权；再用探针挑出真正可用的模型
  let opt = await api('GET', `/api/wiki/model-options?teamId=${TID}`, { token: owner.token })
  let embCandidates = opt.json?.embeddingModels ?? []
  let convCandidates = opt.json?.conversationModels ?? []
  if (embCandidates.length === 0 || convCandidates.length === 0) {
    const boot = await bootstrapTeamModels(TID)
    if (!boot.ok) console.log(`INFO | 模型自举失败：${boot.reason}`)
    opt = await api('GET', `/api/wiki/model-options?teamId=${TID}`, { token: owner.token })
    embCandidates = opt.json?.embeddingModels ?? []
    convCandidates = opt.json?.conversationModels ?? []
  }

  let embModel = null, convModel = null
  let embeddingBound = false
  if (embCandidates.length > 0 && convCandidates.length > 0) {
    const probe = await uploadWikiDoc(owner.token, WID, `wf-probe-${TS}.md`, makeMd('探针文档', 10), 'text/markdown')
    console.log(`INFO | 探针文档上传：${probe.ok ? 'ok documentId=' + probe.documentId : probe.step + ':' + probe.res?.status}`)
    if (probe.documentId) {
      const part = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
        token: owner.token,
        body: { wikiId: WID, documentIds: [probe.documentId], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' },
      })
      const pItem = (part.json?.items ?? [])[0]
      console.log(`INFO | 探针文档切割：${part.status} ${pItem?.success === true ? 'ok' : JSON.stringify(pItem)}`)
      embModel = await pickWorkingEmbeddingModel(owner, WID, probe.documentId, embCandidates.slice(0, 4))
      convModel = await pickWorkingConversationModel(member, WID, probe.documentId, convCandidates.slice(0, 4))
    }
  } else {
    console.log(`INFO | 模型候选为空：embedding=${embCandidates.length}, conversation=${convCandidates.length}`)
  }
  const hasModels = Boolean(embModel?.id) && Boolean(convModel?.id)
  if (!hasModels) console.log('INFO | 未找到可用的 embedding/对话模型组合，依赖模型的场景将跳过')

  if (hasModels) {
    const bind = await api('PUT', `/api/wiki/${WID}/embedding-config`, {
      token: owner.token,
      body: { wikiId: WID, embeddingModelId: embModel.id, embeddingDimensions: 1024 },
    })
    embeddingBound = bind.status === 200
    check('WK-S28 前置：绑定向量模型 200', embeddingBound, `${bind.status} ${bind.text.slice(0, 120)}`)
  } else {
    skip('WK-S28 前置：绑定向量模型（环境无可用模型）')
  }

  // ===== WK-S27：默认工作流配置 =====
  {
    // 配置保存仅校验模型存在且已授权（不调用 LLM），任一已授权对话模型即可
    const anyConvId = convCandidates[0]?.id ?? '00000000-0000-0000-0000-000000000000'
    const forbidden = await api('PUT', `/api/wiki/${WID}/workflow-config`, {
      token: member.token,
      body: { wikiId: WID, partition: { splitMode: 'recursive', chunkSize: 400, chunkOverlap: 20, overlapUnit: 'character', sizeUnit: 'character' } },
    })
    check('WK-S27a Member 保存默认工作流 403', forbidden.status === 403, `${forbidden.status} ${forbidden.text.slice(0, 120)}`)

    const cfg = {
      partition: { mode: 'normal', splitMode: 'recursive', chunkSize: 400, chunkOverlap: 20, overlapUnit: 'character', sizeUnit: 'character' },
      metadata: { metadataModelId: anyConvId, strategyTypes: ['outlineGeneration', 'questionGeneration'] },
      embedding: { embedSourceText: true, embedMetadata: true },
    }
    const saved = await api('PUT', `/api/wiki/${WID}/workflow-config`, { token: owner.token, body: { ...cfg, wikiId: WID } })
    check('WK-S27b Admin 保存默认工作流 200', saved.status === 200, `${saved.status} ${saved.text.slice(0, 120)}`)

    const detail = await api('GET', `/api/wiki/${WID}`, { token: owner.token })
    const wf = detail.json?.workflowConfig
    check('WK-S27c 详情回读 workflowConfig（切割参数）', wf?.partition?.chunkSize === 400 && wf?.partition?.splitMode === 'recursive' && wf?.partition?.mode === 'normal', JSON.stringify(wf?.partition))
    check('WK-S27d 详情回读 workflowConfig（元数据/向量化预设）', wf?.metadata?.metadataModelId === anyConvId && wf?.embedding?.embedSourceText === true, JSON.stringify(wf?.metadata))
    check('WK-S27g 详情回读多选策略', JSON.stringify(wf?.metadata?.strategyTypes) === JSON.stringify(['outlineGeneration', 'questionGeneration']), JSON.stringify(wf?.metadata?.strategyTypes))

    const badModel = await api('PUT', `/api/wiki/${WID}/workflow-config`, {
      token: owner.token,
      body: { wikiId: WID, metadata: { metadataModelId: '11111111-1111-1111-1111-111111111111' } },
    })
    check('WK-S27e 元数据模型不存在 404', badModel.status === 404, `${badModel.status}`)

    const badEmbedding = await api('PUT', `/api/wiki/${WID}/workflow-config`, {
      token: owner.token,
      body: { wikiId: WID, embedding: { embedSourceText: false, embedMetadata: false } },
    })
    check('WK-S27f 向量化两项全关 400', badEmbedding.status === 400, `${badEmbedding.status}`)
  }

  // ===== WK-S28：批量全流程（切割 + 生成元数据 + 向量化） =====
  let d1 = null, d2 = null
  {
    const up1 = await uploadWikiDoc(member.token, WID, `wf-a-${TS}.md`, makeMd('批量工作流文档A', 30), 'text/markdown')
    const up2 = await uploadWikiDoc(member.token, WID, `wf-b-${TS}.md`, makeMd('批量工作流文档B', 30), 'text/markdown')
    check('WK-S28a 前置：上传两个文档 200', up1.ok && up2.ok && up1.documentId > 0 && up2.documentId > 0,
      `${up1.ok ? '' : `${up1.step}:${up1.res?.status}:${up1.res?.text?.slice(0, 100)}`} ${up2.ok ? '' : `${up2.step}:${up2.res?.status}:${up2.res?.text?.slice(0, 100)}`}`)
    d1 = up1.documentId
    d2 = up2.documentId
  }

  // ===== WK-S28b~d：批量全流程（依赖模型） =====
  if (!d1 || !d2) {
    check('WK-S28b 批量全流程 200 且逐文档成功', false, '未获取到 documentId')
  } else if (!hasModels) {
    skip('WK-S28b~d 批量全流程（环境无可用模型）')
  } else {
    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: {
        wikiId: WID,
        documentIds: [d1, d2],
        isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character',
        isGenerateMetadata: true, metadataModelId: convModel.id, strategyTypes: ['outlineGeneration'],
        isEmbedding: true, embedSourceText: true, embedMetadata: true,
      },
    })
    const items = batch.json?.items ?? []
    check('WK-S28b 批量全流程 200 且逐文档成功', batch.status === 200 && items.length === 2 && items.every((i) => i.success === true), `${batch.status} ${batch.text.slice(0, 160)}`)
    check('WK-S28c 全流程任务返回 taskId', items.every((i) => typeof i.taskId === 'string' && i.taskId.length > 0), JSON.stringify(items))

    const done = await waitUntil(member.token, WID, (docs) => {
      const a = docs.get(d1), b = docs.get(d2)
      return a?.isEmbedding === true && b?.isEmbedding === true && (a?.metadataCount ?? 0) > 0 && (b?.metadataCount ?? 0) > 0
    })
    const a = done.docs?.get(d1)
    check('WK-S28d 60s 内两文档向量化完成且元数据>0', done.ok, `isEmbedding=${a?.isEmbedding}, metadataCount=${a?.metadataCount}, chunkCount=${a?.chunkCount}`)
  }

  // ===== WK-S29：批量单步·只切割 =====
  let d3 = null
  {
    const up = await uploadWikiDoc(member.token, WID, `wf-c-${TS}.md`, makeMd('单步切割文档C', 30), 'text/markdown')
    check('WK-S29a 前置：上传文档C 200', up.ok && up.documentId > 0, up.ok ? '' : `${up.step}:${up.res?.status}:${up.res?.text?.slice(0, 100)}`)
    d3 = up.documentId
    if (!d3) {
      console.log(`\n===== 批量工作流 E2E 汇总: PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
      process.exit(FAIL > 0 ? 1 : 0)
    }

    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: {
        wikiId: WID,
        documentIds: [d3],
        isPartition: true, splitMode: 'recursive', chunkSize: 256, chunkOverlap: 20, overlapUnit: 'character', sizeUnit: 'character',
      },
    })
    const item = (batch.json?.items ?? [])[0]
    check('WK-S29b 只切割 200 且无异步任务', batch.status === 200 && item?.success === true && (item?.taskId ?? null) === null, `${batch.status} ${batch.text.slice(0, 160)}`)

    const docs = await listDocs(member.token, WID)
    const c = docs.get(d3)
    check('WK-S29c 只切割后：有切片/无元数据/未向量化', (c?.chunkCount ?? 0) > 0 && (c?.metadataCount ?? 0) === 0 && c?.isEmbedding === false, `chunks=${c?.chunkCount}, meta=${c?.metadataCount}, emb=${c?.isEmbedding}`)
  }

  // ===== WK-S30：批量单步·只生成元数据 =====
  if (!hasModels) {
    skip('WK-S30 只生成元数据（环境无可用模型）')
  } else {
    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isGenerateMetadata: true, metadataModelId: convModel.id, strategyTypes: ['outlineGeneration', 'questionGeneration'] },
    })
    const item = (batch.json?.items ?? [])[0]
    check('WK-S30a 只生成元数据 200 且返回 taskId', batch.status === 200 && item?.success === true && typeof item?.taskId === 'string', `${batch.status} ${batch.text.slice(0, 160)}`)

    const done = await waitUntil(member.token, WID, (docs) => (docs.get(d3)?.metadataCount ?? 0) > 0)
    const c = done.docs?.get(d3)
    check('WK-S30b 60s 内元数据>0 且仍未向量化', done.ok && c?.isEmbedding === false, `meta=${c?.metadataCount}, emb=${c?.isEmbedding}`)
  }

  // ===== WK-S31：批量单步·只向量化 =====
  if (!hasModels) {
    skip('WK-S31 只向量化（环境无可用模型）')
  } else {
    // 若 S30 的元数据任务被环境干扰未生成元数据，则退化为仅原文向量化，保持本场景聚焦"单步向量化"
    const pre = await listDocs(member.token, WID)
    const hasMeta = (pre.get(d3)?.metadataCount ?? 0) > 0
    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isEmbedding: true, embedSourceText: true, embedMetadata: hasMeta },
    })
    const item = (batch.json?.items ?? [])[0]
    check('WK-S31a 只向量化 200', batch.status === 200 && item?.success === true, `${batch.status} ${batch.text.slice(0, 160)}`)

    const done = await waitUntil(member.token, WID, (docs) => docs.get(d3)?.isEmbedding === true)
    check('WK-S31b 60s 内向量化完成', done.ok, `emb=${done.docs?.get(d3)?.isEmbedding}`)
  }

  // ===== WK-S32：批量错误隔离 =====
  {
    const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3, 999999999], isPartition: true, splitMode: 'markdown', chunkSize: 200, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' },
    })
    const items = batch.json?.items ?? []
    const bad = items.find((i) => Number(i.documentId) === 999999999)
    check('WK-S32a 不存在的文档逐文档失败且不影响其他文档', batch.status === 200 && items.length === 2 && bad?.success === false && items.find((i) => Number(i.documentId) === d3)?.success === true, batch.text.slice(0, 200))
    check('WK-S32b 失败项携带原因', typeof bad?.message === 'string' && bad.message.includes('不存在'), bad?.message)
  }

  // ===== WK-S33：批量参数校验 =====
  {
    const empty = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [], isPartition: true, chunkSize: 100, chunkOverlap: 10 },
    })
    check('WK-S33a 空文档列表 400', empty.status === 400, `${empty.status}`)

    const noStep = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3] },
    })
    check('WK-S33b 三步全不勾 400', noStep.status === 400, `${noStep.status}`)

    const noModel = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isGenerateMetadata: true },
    })
    check('WK-S33c 元数据缺模型 400', noModel.status === 400, `${noModel.status}`)

    const badChunk = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isPartition: true, chunkSize: 0, chunkOverlap: 0 },
    })
    check('WK-S33d 切割缺 chunkSize 400', badChunk.status === 400, `${badChunk.status}`)

    const tooMany = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: Array.from({ length: 51 }, (_, i) => i + 1), isPartition: true, chunkSize: 100, chunkOverlap: 10 },
    })
    check('WK-S33e 超过 50 个文档 400', tooMany.status === 400, `${tooMany.status}`)
  }

  // ===== WK-S34：批量前置校验 =====
  {
    const uncut = await uploadWikiDoc(member.token, WID, `wf-d-${TS}.md`, makeMd('未切割文档D', 20), 'text/markdown')
    check('WK-S34a 前置：上传文档D 200', uncut.ok && uncut.documentId > 0, uncut.ok ? '' : `${uncut.step}:${uncut.res?.status}:${uncut.res?.text?.slice(0, 100)}`)
    if (uncut.documentId && embeddingBound) {
      // 上传即自动提取内容；跳过切割直接向量化 → 逐文档失败并给出原因
      const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
        token: member.token,
        body: { wikiId: WID, documentIds: [uncut.documentId], isEmbedding: true, embedSourceText: true, embedMetadata: false },
      })
      const item = (batch.json?.items ?? [])[0]
      check('WK-S34b 未切割直接向量化：逐文档失败', batch.status === 200 && item?.success === false && (item?.message ?? '').includes('切割'), batch.text.slice(0, 160))
    } else if (!embeddingBound) {
      skip('WK-S34b 未切割直接向量化（未绑定向量模型）')
    }

    // 未配置向量模型的新知识库：批量向量化整体 409
    const WID2 = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'wf-wiki2-' + TS, description: 'no embedding' } })).json.value)
    const up = await uploadWikiDoc(member.token, WID2, `wf-e-${TS}.md`, makeMd('无向量模型文档E', 10), 'text/markdown')
    if (up.documentId) {
      const batch = await api('POST', `/api/wiki/${WID2}/documents/batch-workflow`, {
        token: member.token,
        body: { wikiId: WID2, documentIds: [up.documentId], isEmbedding: true, embedSourceText: true, embedMetadata: true },
      })
      check('WK-S34c 未配置向量模型批量向量化 409', batch.status === 409, `${batch.status}`)
    }

    const outsiderBatch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: outsider.token,
      body: { wikiId: WID, documentIds: [d3], isPartition: true, chunkSize: 100, chunkOverlap: 10 },
    })
    check('WK-S34d 非成员批量处理 404', outsiderBatch.status === 404, `${outsiderBatch.status}`)
  }

  // ===== WK-S35：批量 AI 切割 =====
  {
    const noModel = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isPartition: true, isAiPartition: true },
    })
    check('WK-S35a AI 切割缺模型 400', noModel.status === 400, `${noModel.status}`)

    const noStep = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
      token: member.token,
      body: { wikiId: WID, documentIds: [d3], isAiPartition: true, aiModelId: '11111111-1111-1111-1111-111111111111' },
    })
    check('WK-S35b 未勾选切割步骤仅选 AI 切割 400', noStep.status === 400, `${noStep.status}`)

    const aiModelId = convCandidates[0]?.id
    if (!aiModelId) {
      skip('WK-S35c AI 切割批量提交（环境无对话模型候选）')
    } else {
      // 提交语义：AI 切割 + 单策略元数据生成一次提交，AI 切割无需切片参数
      const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
        token: member.token,
        body: {
          wikiId: WID,
          documentIds: [d3],
          isPartition: true, isAiPartition: true, aiModelId,
          isGenerateMetadata: true, metadataModelId: aiModelId, strategyTypes: ['semanticAggregation'],
        },
      })
      const item = (batch.json?.items ?? [])[0]
      check('WK-S35c AI 切割批量提交 200 且返回 taskId（无需切片参数）', batch.status === 200 && item?.success === true && typeof item?.taskId === 'string', `${batch.status} ${batch.text.slice(0, 160)}`)
    }
  }

  // ===== WK-S36：多选生成策略提交 =====
  {
    if (!convCandidates[0]?.id) {
      skip('WK-S36 多选策略批量提交（环境无对话模型候选）')
    } else {
      // 独立文档，避免与 S35c 的任务撞同文档并发防护
      const up = await uploadWikiDoc(member.token, WID, `wf-f-${TS}.md`, makeMd('多选策略文档F', 12), 'text/markdown')
      check('WK-S36a 前置：上传文档F 200', up.ok && up.documentId > 0, up.ok ? '' : `${up.step}:${up.res?.status}`)
      if (up.documentId) {
        await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
          token: member.token,
          body: { wikiId: WID, documentIds: [up.documentId], isPartition: true, splitMode: 'markdown', chunkSize: 300, chunkOverlap: 10, overlapUnit: 'character', sizeUnit: 'character' },
        })
        const batch = await api('POST', `/api/wiki/${WID}/documents/batch-workflow`, {
          token: member.token,
          body: {
            wikiId: WID,
            documentIds: [up.documentId],
            isGenerateMetadata: true,
            metadataModelId: convCandidates[0].id,
            strategyTypes: ['outlineGeneration', 'keywordSummaryFusion', 'semanticAggregation'],
          },
        })
        const item = (batch.json?.items ?? [])[0]
        check('WK-S36 多选生成策略批量提交 200', batch.status === 200 && item?.success === true && typeof item?.taskId === 'string', `${batch.status} ${batch.text.slice(0, 160)}`)
      }
    }
  }

  console.log(`\n===== 批量工作流 E2E 汇总: PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
