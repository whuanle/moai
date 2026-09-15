// 知识图谱模块 E2E（真实 HTTP，复用 wiki/team-e2e 同款登录/断言；后端 127.0.0.1:5210）
// 场景编号与 docs/knowledgegraph/bdd.md 对应（@KG-S1..S16）
// 需后端 + Memgraph/图数据库 + KG_ENABLED=true；未开启/连不上时打印 SKIP 并退出码 0（CI 无图数据库不失败）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱 E2E 未执行: ${reason}`)
  console.warn('      需要后端 + 图数据库可达 且 KG_ENABLED=true（设置页开启并配置 KG_URI）。')
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

async function main() {
  // 能力开关的本地快速短路；真正以服务端 list.enabled 为准
  const envFlag = process.env.KG_ENABLED
  if (envFlag !== undefined && !['true', '1'].includes(String(envFlag).toLowerCase())) {
    skip(`环境变量 KG_ENABLED=${envFlag}`)
  }

  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) {
    throw new Error(`root 登录失败: ${login.status} ${login.text.slice(0, 120)}`)
  }
  const token = login.json.accessToken

  // 准备团队：root 创建即 Owner，满足 adminOnly
  const team = await api('POST', '/api/team', { token, body: { name: 'kg-team-' + TS } })
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
  const probe = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-probe-' + TS, mode: 'connected', database: 'neo4j' } })
  if (probe.status !== 200) {
    skip(`图数据库探活失败 (${probe.status}): ${probe.text.slice(0, 160)}`)
  }
  await api('DELETE', kg(Number(probe.json.value)), { token })

  // ===== KG-S1 创建图谱（空白模板）=====
  const blankName = 'kg-blank-' + TS
  const c1 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: blankName, templateKey: 'blank' } })
  check('KG-S1a 创建空白图谱 200 且返回 id', c1.status === 200 && Number(c1.json?.value) > 0, `${c1.status} ${c1.text.slice(0, 120)}`)
  const G1 = Number(c1.json?.value)
  if (!Number.isFinite(G1)) throw new Error('KG-S1 建图失败，后续场景无法继续')
  {
    const s = await api('GET', `${kg(G1)}/schema`, { token })
    check('KG-S1b 空白图谱 schema 无实体/关系类型且 mode=managed', s.status === 200 && (s.json?.entityTypes ?? []).length === 0 && (s.json?.relationTypes ?? []).length === 0 && s.json?.mode === 'managed', JSON.stringify(s.json).slice(0, 150))
  }

  // ===== KG-S2 创建模板图谱（ops）=====
  const opsName = 'kg-ops-' + TS
  const c2 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: opsName, templateKey: 'ops' } })
  check('KG-S2a 创建 ops 模板图谱 200', c2.status === 200 && Number(c2.json?.value) > 0, `${c2.status} ${c2.text.slice(0, 120)}`)
  const G2 = Number(c2.json?.value)
  let svcTypeId = null
  let pplTypeId = null
  {
    const s = await api('GET', `${kg(G2)}/schema`, { token })
    const et = s.json?.entityTypes ?? []
    const rt = s.json?.relationTypes ?? []
    const names = et.map(x => x.name)
    const byName = (arr, n) => arr.find(x => x.name === n)
    const svc = byName(et, '服务')
    const ppl = byName(et, '人员')
    const prj = byName(et, '项目')
    const maint = byName(rt, '维护')
    const dep = byName(rt, '依赖')
    svcTypeId = svc?.entityTypeId ?? null
    pplTypeId = ppl?.entityTypeId ?? null
    check('KG-S2b 模板实体类型含 服务/人员/项目', s.status === 200 && ['服务', '人员', '项目'].every(n => names.includes(n)), JSON.stringify(names))
    check('KG-S2c 模板关系类型含 维护/依赖', ['维护', '依赖'].every(n => byName(rt, n)), JSON.stringify(rt.map(x => x.name)))
    check('KG-S2d 维护 起止约束=人员→服务', maint?.sourceTypeId === ppl?.entityTypeId && maint?.targetTypeId === svc?.entityTypeId, JSON.stringify(maint))
    check('KG-S2e 依赖 起止约束=项目→服务', dep?.sourceTypeId === prj?.entityTypeId && dep?.targetTypeId === svc?.entityTypeId, JSON.stringify(dep))
  }

  // 在 ops 图写入节点+边，供 KG-S11 内省看到 KgNode / KG_REL
  {
    const seedSvc = await api('POST', `${kg(G2)}/nodes`, { token, body: { entityTypeId: svcTypeId, name: 'seed-svc-' + TS } })
    const seedPpl = await api('POST', `${kg(G2)}/nodes`, { token, body: { entityTypeId: pplTypeId, name: 'seed-ppl-' + TS } })
    check('KG-S2f 模板图谱可新增节点 200', seedSvc.status === 200 && seedPpl.status === 200 && !!seedSvc.json?.value && !!seedPpl.json?.value, `${seedSvc.status}/${seedPpl.status} ${seedSvc.text.slice(0, 100)}`)
    const maintRel = (await api('GET', `${kg(G2)}/schema`, { token })).json?.relationTypes?.find(x => x.name === '维护')
    const seedEdge = await api('POST', `${kg(G2)}/edges`, { token, body: { relationTypeId: maintRel?.relationTypeId, sourceNodeId: seedPpl.json?.value, targetNodeId: seedSvc.json?.value } })
    check('KG-S2g 模板图谱可新增边 200', seedEdge.status === 200 && !!seedEdge.json?.value, `${seedEdge.status} ${seedEdge.text.slice(0, 100)}`)
  }

  // ===== KG-S3 同团队重名 409 =====
  check('KG-S3 同团队重名建图 409', (await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: blankName, templateKey: 'blank' } })).status === 409)

  // ===== KG-S4 新增实体类型、关系类型（带起止约束）=====
  const mkType = (name) => api('POST', `${kg(G1)}/entity-types`, { token, body: { name } })
  const te1 = await mkType('服务')
  const te2 = await mkType('人员')
  const te3 = await mkType('项目')
  check('KG-S4a 新增实体类型 服务/人员/项目 200', [te1, te2, te3].every(r => r.status === 200 && Number(r.json?.value) > 0), `${te1.status}/${te2.status}/${te3.status}`)
  const SVC = Number(te1.json?.value)
  const PPL = Number(te2.json?.value)
  const PRJ = Number(te3.json?.value)
  const tr1 = await api('POST', `${kg(G1)}/relation-types`, { token, body: { name: '维护', sourceTypeId: PPL, targetTypeId: SVC } })
  const tr2 = await api('POST', `${kg(G1)}/relation-types`, { token, body: { name: '依赖', sourceTypeId: PRJ, targetTypeId: SVC } })
  check('KG-S4b 新增关系类型带起止约束 200', tr1.status === 200 && tr2.status === 200 && Number(tr1.json?.value) > 0, `${tr1.status}/${tr2.status}`)
  const MAINT = Number(tr1.json?.value)
  {
    const s = await api('GET', `${kg(G1)}/schema`, { token })
    const rel = (s.json?.relationTypes ?? []).find(x => x.name === '维护')
    check('KG-S4c schema 回显关系类型约束', s.status === 200 && Number(rel?.sourceTypeId) === PPL && Number(rel?.targetTypeId) === SVC, JSON.stringify(rel))
  }

  // ===== KG-S5 新增节点（类型不符/不存在 400）=====
  const bad = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: 999999, name: 'ghost' } })
  check('KG-S5a 不存在实体类型加节点 400', bad.status === 400, `${bad.status} ${bad.text.slice(0, 120)}`)
  const nPpl = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PPL, name: '张三-' + TS } })
  const nSvc = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: SVC, name: '订单服务-' + TS } })
  const nPrj = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PRJ, name: '项目A-' + TS } })
  check('KG-S5b 合法节点 200', [nPpl, nSvc, nPrj].every(r => r.status === 200 && !!r.json?.value), `${nPpl.status}/${nSvc.status}/${nPrj.status}`)
  const N = { PPL: nPpl.json?.value, SVC: nSvc.json?.value, PRJ: nPrj.json?.value }

  // ===== KG-S6 新增边（违反起止约束 400）=====
  {
    const badSource = await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: MAINT, sourceNodeId: N.PRJ, targetNodeId: N.SVC } })
    check('KG-S6a 起点类型违反约束 400', badSource.status === 400, `${badSource.status} ${badSource.text.slice(0, 120)}`)
    const badTarget = await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: MAINT, sourceNodeId: N.PPL, targetNodeId: N.PRJ } })
    check('KG-S6b 终点类型违反约束 400', badTarget.status === 400, `${badTarget.status} ${badTarget.text.slice(0, 120)}`)
    const ok = await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: MAINT, sourceNodeId: N.PPL, targetNodeId: N.SVC } })
    check('KG-S6c 合法边 200', ok.status === 200 && !!ok.json?.value, `${ok.status} ${ok.text.slice(0, 120)}`)
  }

  // ===== KG-S7 节点/边分页 =====
  {
    const n2 = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PPL, name: '李四-' + TS } })
    const n3 = await api('POST', `${kg(G1)}/nodes`, { token, body: { entityTypeId: PPL, name: '王五-' + TS } })
    await api('POST', `${kg(G1)}/edges`, { token, body: { relationTypeId: MAINT, sourceNodeId: n2.json?.value, targetNodeId: N.SVC } })

    const p1 = await api('POST', `${kg(G1)}/nodes/list`, { token, body: { pageNo: 1, pageSize: 2 } })
    check('KG-S7a 节点分页 pageSize=2 截断且 total=5', p1.status === 200 && (p1.json?.items ?? []).length === 2 && Number(p1.json?.total) === 5, JSON.stringify({ n: (p1.json?.items ?? []).length, total: p1.json?.total }))
    const p3 = await api('POST', `${kg(G1)}/nodes/list`, { token, body: { pageNo: 3, pageSize: 2 } })
    check('KG-S7b 节点分页 pageNo=3 剩 1 条', p3.status === 200 && (p3.json?.items ?? []).length === 1 && Number(p3.json?.total) === 5, JSON.stringify({ n: (p3.json?.items ?? []).length, total: p3.json?.total }))

    const e1 = await api('POST', `${kg(G1)}/edges/list`, { token, body: { pageNo: 1, pageSize: 1 } })
    check('KG-S7c 边分页 pageSize=1 且 total=2', e1.status === 200 && (e1.json?.items ?? []).length === 1 && Number(e1.json?.total) === 2, JSON.stringify({ n: (e1.json?.items ?? []).length, total: e1.json?.total }))
    const e2 = await api('POST', `${kg(G1)}/edges/list`, { token, body: { relationTypeId: MAINT, pageNo: 1, pageSize: 20 } })
    check('KG-S7d 边按关系类型筛选 total=2', e2.status === 200 && Number(e2.json?.total) === 2, JSON.stringify({ total: e2.json?.total }))
  }

  // ===== KG-S8 删除仍有节点的实体类型 409 =====
  check('KG-S8 删除仍有节点的实体类型 409', (await api('DELETE', `${kg(G1)}/entity-types/${PPL}`, { token })).status === 409)

  // ===== KG-S9 删除图谱后节点/边清空 =====
  {
    const del = await api('DELETE', kg(G1), { token })
    check('KG-S9a 删除图谱 200', del.status === 200, `${del.status} ${del.text.slice(0, 120)}`)
    check('KG-S9b 删除后详情 404', (await api('GET', kg(G1), { token })).status === 404)
    const after = await api('GET', `/api/knowledge-graph/list?teamId=${TID}`, { token })
    check('KG-S9c 删除后列表不含该图谱且仍含 G2', after.status === 200 && !(after.json?.items ?? []).some(i => Number(i.kgId) === G1) && (after.json?.items ?? []).some(i => Number(i.kgId) === G2), JSON.stringify({ ids: (after.json?.items ?? []).map(i => i.kgId) }))
  }

  // ===== KG-S10 接入未知库名（方言语义：neo4j 多库→400 拒绝；memgraph 单库→200 仅登记名，随后清理）=====
  {
    const s10 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-conn-miss-' + TS, mode: 'connected', database: 'kg_missing_db_' + TS } })
    check('KG-S10 接入未知库名按方言处理（neo4j=400 / memgraph=200）', s10.status === 400 || s10.status === 200, `${s10.status}`)
    if (s10.status === 200 && Number(s10.json?.value)) await api('DELETE', kg(Number(s10.json.value)), { token })
  }

  // ===== KG-S11 接入真实数据库 neo4j（内省 schema）=====
  const c11 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-conn-' + TS, mode: 'connected', database: 'neo4j' } })
  check('KG-S11a 接入真实库 neo4j 200 且返回 id', c11.status === 200 && Number(c11.json?.value) > 0, `${c11.status} ${c11.text.slice(0, 120)}`)
  const CONN = Number(c11.json?.value)
  {
    const s = await api('GET', `${kg(CONN)}/schema`, { token })
    const d = s.json ?? {}
    const labels = (d.entityTypes ?? []).map(x => x.name)
    const rels = (d.relationTypes ?? []).map(x => x.name)
    check('KG-S11b schema mode=connected / readOnly=true / database=neo4j', s.status === 200 && d.mode === 'connected' && d.readOnly === true && d.database === 'neo4j', JSON.stringify({ mode: d.mode, readOnly: d.readOnly, database: d.database }))
    check('KG-S11c 内省标签含 KgNode', labels.includes('KgNode'), JSON.stringify(labels))
    check('KG-S11d 内省关系类型含 KG_REL', rels.includes('KG_REL'), JSON.stringify(rels))
    check('KG-S11e 内省 propertyKeys 含 kgId', Array.isArray(d.propertyKeys) && d.propertyKeys.includes('kgId'), JSON.stringify(d.propertyKeys))
    const det = await api('GET', kg(CONN), { token })
    check('KG-S11f 详情 readOnly=true / mode=connected', det.json?.readOnly === true && det.json?.mode === 'connected', JSON.stringify({ mode: det.json?.mode, readOnly: det.json?.readOnly }))
  }

  // ===== KG-S12 connected 图谱写操作 409 只读 =====
  check('KG-S12a connected 图谱新增节点 409 只读', (await api('POST', `${kg(CONN)}/nodes`, { token, body: { entityTypeId: 1, name: 'readonly' } })).status === 409)
  check('KG-S12b connected 图谱新增实体类型 409 只读', (await api('POST', `${kg(CONN)}/entity-types`, { token, body: { name: 'x' } })).status === 409)

  // ===== KG-S13 画布有界子图（托管图 G2 已有 2 节点 1 边）=====
  {
    const cv = await api('POST', `${kg(G2)}/canvas`, { token, body: { limit: 200 } })
    check('KG-S13a 画布返回节点与节点集内部的边', cv.status === 200 && (cv.json?.nodes ?? []).length >= 2 && (cv.json?.edges ?? []).length >= 1, JSON.stringify({ n: cv.json?.nodes?.length, e: cv.json?.edges?.length }))
    const cvf = await api('POST', `${kg(G2)}/canvas`, { token, body: { keyword: 'seed-svc-' + TS } })
    check('KG-S13b 关键字过滤仅命中 1 节点', cvf.status === 200 && (cvf.json?.nodes ?? []).length === 1, JSON.stringify(cvf.json?.nodes))

    // ===== KG-S14 一跳邻接展开 =====
    const listR = await api('POST', `${kg(G2)}/nodes/list`, { token, body: { keyword: 'seed-ppl-' + TS, pageNo: 1, pageSize: 5 } })
    const pplNode = (listR.json?.items ?? [])[0]
    const nb = await api('GET', `${kg(G2)}/nodes/${pplNode?.nodeId}/neighbors?limit=50`, { token })
    check('KG-S14a 邻接含 seed-svc 且带维护边', nb.status === 200 && (nb.json?.nodes ?? []).some(x => x.name === 'seed-svc-' + TS) && (nb.json?.edges ?? []).length >= 1, JSON.stringify(nb.json).slice(0, 200))
    const nbGhost = await api('GET', `${kg(G2)}/nodes/ghost/neighbors`, { token })
    check('KG-S14b 不存在节点邻接 404', nbGhost.status === 404, `${nbGhost.status}`)
  }

  // ===== KG-S17 接入图画布（外部图库全量查询，elementId 定位 + label 展示）=====
  {
    const cv = await api('POST', `${kg(CONN)}/canvas`, { token, body: { keyword: 'seed-svc-' + TS, limit: 50 } })
    const nodes = cv.json?.nodes ?? []
    const node = nodes[0]
    check('KG-S17a 接入图画布返回带 entityLabel 的节点', cv.status === 200 && nodes.length >= 1 && typeof node?.entityLabel === 'string' && node.entityLabel.length > 0, JSON.stringify(nodes).slice(0, 200))
    const cvLabel = await api('POST', `${kg(CONN)}/canvas`, { token, body: { label: 'KgNode', limit: 50 } })
    check('KG-S17b 接入图按标签过滤命中 KgNode', cvLabel.status === 200 && (cvLabel.json?.nodes ?? []).some(x => x.entityLabel === 'KgNode'), JSON.stringify({ n: cvLabel.json?.nodes?.length }))
    const cvEdges = cvLabel.json?.edges ?? []
    check('KG-S17c 接入图画布边带 relationName', cvEdges.length === 0 || typeof cvEdges[0]?.relationName === 'string', JSON.stringify(cvEdges).slice(0, 200))

    // ===== KG-S18 接入图一跳邻接（elementId）=====
    if (node?.nodeId) {
      const nb = await api('GET', `${kg(CONN)}/nodes/${encodeURIComponent(node.nodeId)}/neighbors?limit=50`, { token })
      check('KG-S18a 接入图邻接 200', nb.status === 200, `${nb.status} ${nb.text.slice(0, 120)}`)
    } else {
      check('KG-S18a 接入图邻接 200', false, '缺少可展开节点')
    }
    const nbGhost = await api('GET', `${kg(CONN)}/nodes/ghost/neighbors`, { token })
    check('KG-S18b 接入图不存在节点邻接 404', nbGhost.status === 404, `${nbGhost.status}`)
  }

  // ===== KG-S19 内省缓存与强制刷新 =====
  {
    const cached = await api('GET', `${kg(CONN)}/schema`, { token })
    check('KG-S19a 二次查询命中缓存 fromCache=true', cached.status === 200 && cached.json?.fromCache === true, JSON.stringify({ fromCache: cached.json?.fromCache }))
    const refreshed = await api('GET', `${kg(CONN)}/schema?refresh=true`, { token })
    check('KG-S19b refresh=true 跳过缓存 fromCache=false', refreshed.status === 200 && refreshed.json?.fromCache === false, JSON.stringify({ fromCache: refreshed.json?.fromCache }))
  }

  // ===== KG-S20 图谱头像（真实上传管线 + 设置 + 回显 + 未登记拦截）=====
  {
    // 1x1 PNG
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64')
    const sha256 = crypto.createHash('sha256').update(png).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token, body: { fileName: `kg-avatar-${TS}.png`, contentType: 'image/png', fileSize: png.length, SHA256: sha256 } })
    let objectKey = pre.json?.objectKey
    if (pre.status === 200 && pre.json?.isExist === false && pre.json?.uploadUrl) {
      const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: png })
      const complete = await api('POST', '/api/storage/complate_url', { token, body: { isSuccess: true, fileId: pre.json.fileId } })
      check('KG-S20a 图片预上传+直传+完成 200', put.status === 200 && complete.status === 200, JSON.stringify({ put: put.status, complete: complete.status }))
      objectKey = objectKey ?? complete.json?.objectKey
    } else {
      check('KG-S20a 图片预上传+直传+完成 200', pre.status === 200 && pre.json?.isExist === true, JSON.stringify(pre).slice(0, 160))
    }

    if (objectKey) {
      const set = await api('POST', `${kg(G2)}/avatar`, { token, body: { objectKey } })
      check('KG-S20b 设置图谱头像 200', set.status === 200, `${set.status} ${set.text.slice(0, 120)}`)
      const det = await api('GET', kg(G2), { token })
      check('KG-S20c 详情回显 avatarPath', det.json?.avatarPath === objectKey, JSON.stringify({ avatarPath: det.json?.avatarPath, objectKey }))
      const list = await api('GET', `/api/knowledge-graph/list?teamId=${TID}`, { token })
      const item = (list.json?.items ?? []).find(i => Number(i.kgId) === G2)
      check('KG-S20d 列表回显 avatarPath', item?.avatarPath === objectKey, JSON.stringify({ avatarPath: item?.avatarPath }))
    } else {
      check('KG-S20b 设置图谱头像 200', false, '未取得 objectKey')
      check('KG-S20c 详情回显 avatarPath', false, '未取得 objectKey')
      check('KG-S20d 列表回显 avatarPath', false, '未取得 objectKey')
    }
    const ghost = await api('POST', `${kg(G2)}/avatar`, { token, body: { objectKey: 'ghost/kg-avatar.png' } })
    check('KG-S20e 未登记 objectKey 设置头像 404', ghost.status === 404, `${ghost.status}`)
  }


  // ===== KG-S21 模型属性设置（实体类型属性定义 → 实例属性值存储 → 回显）=====
  {
    const props = [
      { name: '年龄', type: 'number', required: true, description: '周岁' },
      { name: '入职日期', type: 'date', required: false, description: '' },
      { name: '在职', type: 'boolean', required: false, description: '' },
      { name: '备注', type: 'string', required: false, description: '' },
    ]
    const ct = await api('POST', `${kg(G2)}/entity-types`, { token, body: { name: '属性人员-' + TS, properties: props } })
    const propsTypeId = Number(ct.json?.value)
    check('KG-S21a 创建带属性的实体类型 200', ct.status === 200 && propsTypeId > 0, `${ct.status} ${ct.text.slice(0, 120)}`)
    const schema = await api('GET', `${kg(G2)}/schema`, { token })
    const createdType = (schema.json?.entityTypes ?? []).find(x => x.name === '属性人员-' + TS)
    check('KG-S21b schema 回显属性定义', createdType && (createdType.properties ?? []).length === 4 && createdType.properties[0].name === '年龄' && createdType.properties[0].type === 'number' && createdType.properties[0].required === true, JSON.stringify(createdType?.properties).slice(0, 200))
    const dup = await api('POST', `${kg(G2)}/entity-types`, { token, body: { name: '重复属性-' + TS, properties: [{ name: 'a', type: 'string' }, { name: 'a', type: 'string' }] } })
    check('KG-S21c 属性名重复 400', dup.status === 400, `${dup.status}`)
    const badType = await api('POST', `${kg(G2)}/entity-types`, { token, body: { name: '坏类型-' + TS, properties: [{ name: 'a', type: 'float' }] } })
    check('KG-S21d 非法属性类型 400', badType.status === 400, `${badType.status}`)
    const node = await api('POST', `${kg(G2)}/nodes`, { token, body: { entityTypeId: propsTypeId, name: '属性实例-' + TS, properties: { '年龄': '30', '入职日期': '2026-01-01T00:00:00Z', '在职': 'true', '备注': '核心' } } })
    const propsNodeId = node.json?.value
    check('KG-S21e 创建带属性值的实例 200', node.status === 200 && propsNodeId, `${node.status} ${node.text.slice(0, 120)}`)
    const list = await api('POST', `${kg(G2)}/nodes/list`, { token, body: { keyword: '属性实例-' + TS, pageNo: 1, pageSize: 5 } })
    const item = (list.json?.items ?? [])[0]
    check('KG-S21f 实例列表回显属性值', item && item.properties && item.properties['年龄'] === '30' && item.properties['在职'] === 'true' && item.properties['备注'] === '核心', JSON.stringify(item?.properties))
    const upd = await api('PUT', `${kg(G2)}/nodes/${propsNodeId}`, { token, body: { entityTypeId: propsTypeId, name: '属性实例-' + TS, description: '', properties: { '年龄': '31', '在职': 'false' } } })
    const list2 = await api('POST', `${kg(G2)}/nodes/list`, { token, body: { keyword: '属性实例-' + TS, pageNo: 1, pageSize: 5 } })
    const item2 = (list2.json?.items ?? [])[0]
    check('KG-S21g 更新实例属性生效', upd.status === 200 && item2?.properties?.['年龄'] === '31' && item2?.properties?.['在职'] === 'false', JSON.stringify(item2?.properties))
  }

  // ===== KG-S16 图谱名称全局唯一（跨团队重名 409）=====
  {
    const team2 = await api('POST', '/api/team', { token, body: { name: 'kg-team2-' + TS } })
    const TID2 = Number(team2.json?.value)
    check('KG-S16a 跨团队同名建图 409', Number.isFinite(TID2) && (await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID2, name: opsName, templateKey: 'blank' } })).status === 409)
    await api('DELETE', `/api/team/${TID2}`, { token })
  }

  // 清理
  await api('DELETE', kg(G2), { token })
  await api('DELETE', kg(CONN), { token })
  await api('DELETE', `/api/team/${TID}`, { token })

  console.log(`\n===== 知识图谱 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
