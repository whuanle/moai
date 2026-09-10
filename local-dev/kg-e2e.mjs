// 知识图谱模块 E2E（真实 HTTP，复用 wiki/team-e2e 同款登录/断言；后端 127.0.0.1:5210）
// 场景编号与 docs/knowledgegraph/bdd.md 对应（@KG-S1..S12）
// 需后端 + Neo4j + OPEN_NEO4J=true；未开启/连不上时打印 SKIP 并退出码 0（CI 无 Neo4j 不失败）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱 E2E 未执行: ${reason}`)
  console.warn('      需要后端 + Neo4j 可达 且 OPEN_NEO4J=true（设置页开启并配置 NEO4J_URI）。')
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
  const envFlag = process.env.OPEN_NEO4J
  if (envFlag !== undefined && !['true', '1'].includes(String(envFlag).toLowerCase())) {
    skip(`环境变量 OPEN_NEO4J=${envFlag}`)
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

  // Neo4j 可达性探针：用默认库 neo4j 建一个 connected 图，成功即证明可达，随后清理
  const probe = await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-probe-' + TS, mode: 'connected', database: 'neo4j' } })
  if (probe.status !== 200) {
    skip(`Neo4j 探活失败 (${probe.status}): ${probe.text.slice(0, 160)}`)
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
    check('KG-S4c schema 回显关系类型约束', s.status === 200 && rel?.sourceTypeId === PPL && rel?.targetTypeId === SVC, JSON.stringify(rel))
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
    check('KG-S7a 节点分页 pageSize=2 截断且 total=5', p1.status === 200 && (p1.json?.items ?? []).length === 2 && p1.json?.total === 5, JSON.stringify({ n: (p1.json?.items ?? []).length, total: p1.json?.total }))
    const p3 = await api('POST', `${kg(G1)}/nodes/list`, { token, body: { pageNo: 3, pageSize: 2 } })
    check('KG-S7b 节点分页 pageNo=3 剩 1 条', p3.status === 200 && (p3.json?.items ?? []).length === 1 && p3.json?.total === 5, JSON.stringify({ n: (p3.json?.items ?? []).length, total: p3.json?.total }))

    const e1 = await api('POST', `${kg(G1)}/edges/list`, { token, body: { pageNo: 1, pageSize: 1 } })
    check('KG-S7c 边分页 pageSize=1 且 total=2', e1.status === 200 && (e1.json?.items ?? []).length === 1 && e1.json?.total === 2, JSON.stringify({ n: (e1.json?.items ?? []).length, total: e1.json?.total }))
    const e2 = await api('POST', `${kg(G1)}/edges/list`, { token, body: { relationTypeId: MAINT, pageNo: 1, pageSize: 20 } })
    check('KG-S7d 边按关系类型筛选 total=2', e2.status === 200 && e2.json?.total === 2, JSON.stringify({ total: e2.json?.total }))
  }

  // ===== KG-S8 删除仍有节点的实体类型 409 =====
  check('KG-S8 删除仍有节点的实体类型 409', (await api('DELETE', `${kg(G1)}/entity-types/${PPL}`, { token })).status === 409)

  // ===== KG-S9 删除图谱后节点/边清空 =====
  {
    const del = await api('DELETE', kg(G1), { token })
    check('KG-S9a 删除图谱 200', del.status === 200, `${del.status} ${del.text.slice(0, 120)}`)
    check('KG-S9b 删除后详情 404', (await api('GET', kg(G1), { token })).status === 404)
    const after = await api('GET', `/api/knowledge-graph/list?teamId=${TID}`, { token })
    check('KG-S9c 删除后列表不含该图谱', after.status === 200 && !(after.json?.items ?? []).some(i => Number(i.kgId) === G1))
  }

  // ===== KG-S10 接入不存在的数据库 400 =====
  check('KG-S10 接入不存在数据库 400', (await api('POST', '/api/knowledge-graph', { token, body: { teamId: TID, name: 'kg-conn-miss-' + TS, mode: 'connected', database: 'kg_missing_db_' + TS } })).status === 400)

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

  // 清理
  await api('DELETE', kg(G2), { token })
  await api('DELETE', kg(CONN), { token })
  await api('DELETE', `/api/team/${TID}`, { token })

  console.log(`\n===== 知识图谱 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
