// 知识图谱外部接口 E2E（真实 HTTP；复用 kg-e2e / external-app-e2e 同款登录/断言 helper；后端 127.0.0.1:5210）
// 场景编号 KX-01..KX-08（新编号，不复用 KG-S*）：/api/external/knowledge-graph 仅接受应用 token（团队级授权）
// 用法：node local-dev/kg-external-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5210）
// 前置：后端 + 图数据库可达 且 KG_ENABLED=true（未开启时打印 SKIP 并退出码 0）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱外部接口 E2E 未执行: ${reason}`)
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
const xkg = (id) => `/api/external/knowledge-graph/${id}`
let kx07Skipped = false

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) {
    throw new Error(`root 登录失败: ${login.status} ${login.text.slice(0, 120)}`)
  }
  const token = login.json.accessToken

  // ===== 准备：两个团队，各建一个 managed 图谱；团队1 另建 connected 图谱（探活失败则跳过 KX-07）=====
  const T1 = Number((await api('POST', '/api/team', { token, body: { name: 'kx-team1-' + TS } })).json?.value)
  const T2 = Number((await api('POST', '/api/team', { token, body: { name: 'kx-team2-' + TS } })).json?.value)
  if (!Number.isFinite(T1) || !Number.isFinite(T2)) throw new Error('创建团队失败')

  const cap = await api('GET', `/api/knowledge-graph/list?teamId=${T1}`, { token })
  if (cap.status !== 200 || cap.json?.enabled !== true) skip(`list.enabled=${cap.json?.enabled}`)

  const g1 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: T1, name: 'kx-managed1-' + TS, templateKey: 'blank' } })
  const g2 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: T2, name: 'kx-managed2-' + TS, templateKey: 'blank' } })
  if (g1.status !== 200 || g2.status !== 200) throw new Error(`建 managed 图失败: ${g1.status}/${g2.status}`)
  const G1 = Number(g1.json?.value)
  const G2 = Number(g2.json?.value)

  // connected 探活：成功即保留供 KX-07 只读断言；失败则标记不可用
  let CONN = 0
  {
    const probe = await api('POST', '/api/knowledge-graph', { token, body: { teamId: T1, name: 'kx-conn-' + TS, mode: 'connected', database: 'neo4j' } })
    if (probe.status === 200 && Number(probe.json?.value) > 0) CONN = Number(probe.json.value)
    else console.warn(`WARN | connected 探活失败 (${probe.status})，KX-07 将跳过: ${probe.text.slice(0, 120)}`)
  }

  // 接入点：团队1 / 团队2 各一个
  const acc1 = await api('POST', '/api/access-app', { token, body: { teamId: T1, name: 'kx接入1-' + TS, description: 'kg external e2e' } })
  const acc2 = await api('POST', '/api/access-app', { token, body: { teamId: T2, name: 'kx接入2-' + TS, description: 'kg external e2e' } })
  if (acc1.status !== 200 || acc2.status !== 200) throw new Error(`创建接入点失败: ${acc1.status}/${acc2.status}`)
  const KEY1 = acc1.json?.key
  const KEY2 = acc2.json?.key
  const ACC1 = acc1.json?.accessAppId
  const ACC2 = acc2.json?.accessAppId

  try {
    // ===== KX-01 接入点换应用 token；列表仅含本团队 managed 图谱 =====
    const tok1 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY1 } })
    check('KX-01a accessAppKey 换应用 token 200 tokenType=app', tok1.status === 200 && tok1.json?.tokenType === 'app' && typeof tok1.json?.accessToken === 'string', `${tok1.status} ${tok1.text.slice(0, 120)}`)
    const AT1 = tok1.json?.accessToken
    const tok2 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY2 } })
    const AT2 = tok2.json?.accessToken

    const list1 = await api('POST', '/api/external/knowledge-graph/list', { token: AT1, body: {} })
    const ids1 = (list1.json?.items ?? []).map((x) => Number(x.id))
    const item1 = (list1.json?.items ?? []).find((x) => Number(x.id) === G1)
    check('KX-01b 团队1 列表 200 含本团队 managed 图谱且 mode=managed', list1.status === 200 && ids1.includes(G1) && item1?.mode === 'managed', `${list1.status} ${list1.text.slice(0, 160)}`)
    check('KX-01c 团队1 列表不含他团队图谱与 connected 图谱', !ids1.includes(G2) && (CONN === 0 || !ids1.includes(CONN)), JSON.stringify(ids1))

    const list2 = await api('POST', '/api/external/knowledge-graph/list', { token: AT2, body: {} })
    const ids2 = (list2.json?.items ?? []).map((x) => Number(x.id))
    check('KX-01d 团队2 token 列表仅含团队2 图谱', list2.status === 200 && ids2.includes(G2) && !ids2.includes(G1), JSON.stringify(ids2))

    // ===== KX-02 跨团队/不存在 → 404 =====
    const cross = await api('POST', xkg(G2) + '/nodes', { token: AT1, body: { entityTypeId: 1, name: 'kx-ghost-' + TS } })
    check('KX-02a 他团队图谱写节点 404', cross.status === 404, `${cross.status} ${cross.text.slice(0, 120)}`)
    const ghostId = 987654321
    check('KX-02b 不存在图谱 schema 404', (await api('GET', xkg(ghostId) + '/schema', { token: AT1 })).status === 404)
    check('KX-02c 不存在图谱 nodes/list 404', (await api('POST', xkg(ghostId) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })).status === 404)

    // ===== KX-03 类型 CRUD（实体/关系）=====
    const etAName = 'KX实体A-' + TS
    const teA = await api('POST', xkg(G1) + '/entity-types', { token: AT1, body: { name: etAName, color: '#3b82f6', description: '初始描述' } })
    check('KX-03a 建实体类型 200 返回 id', teA.status === 200 && Number(teA.json?.value) > 0, `${teA.status} ${teA.text.slice(0, 120)}`)
    const ET_A = Number(teA.json?.value)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      // long 全局以字符串序列化（LongStringConverter），id/count 一律 Number() 归一后比较
      const found = (s.json?.entityTypes ?? []).find((x) => Number(x.entityTypeId) === ET_A)
      check('KX-03b schema 出现新类型且 count=0', s.status === 200 && found && found.name === etAName && Number(found.count) === 0, JSON.stringify(found))
    }
    const etARenamed = 'KX实体甲-' + TS
    check('KX-03c 改名实体类型 200', (await api('PUT', `${xkg(G1)}/entity-types/${ET_A}`, { token: AT1, body: { name: etARenamed, color: '#3b82f6', description: '改名后' } })).status === 200)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      check('KX-03d schema 回显改名', (s.json?.entityTypes ?? []).some((x) => Number(x.entityTypeId) === ET_A && x.name === etARenamed), JSON.stringify(s.json?.entityTypes))
    }
    const teB = await api('POST', xkg(G1) + '/entity-types', { token: AT1, body: { name: 'KX实体B-' + TS } })
    check('KX-03e 建第二个实体类型 200', teB.status === 200 && Number(teB.json?.value) > 0, `${teB.status}`)
    const ET_B = Number(teB.json?.value)

    const rtName = 'KX关联-' + TS
    const tr = await api('POST', xkg(G1) + '/relation-types', { token: AT1, body: { name: rtName, color: '#ef4444', sourceTypeId: ET_A, targetTypeId: ET_B } })
    check('KX-03f 建关系类型（绑定起止）200', tr.status === 200 && Number(tr.json?.value) > 0, `${tr.status} ${tr.text.slice(0, 120)}`)
    const RT = Number(tr.json?.value)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      const rel = (s.json?.relationTypes ?? []).find((x) => Number(x.relationTypeId) === RT)
      check('KX-03g schema 回显关系约束 ET_A→ET_B', rel && Number(rel.sourceTypeId) === ET_A && Number(rel.targetTypeId) === ET_B, JSON.stringify(rel))
    }
    const rtRenamed = 'KX关联改-' + TS
    // 注意：更新接口对缺省的 sourceTypeId/targetTypeId 按 null（任意）覆盖，须显式回传以保留约束
    check('KX-03h 更新关系类型 200（回传约束）', (await api('PUT', `${xkg(G1)}/relation-types/${RT}`, { token: AT1, body: { name: rtRenamed, description: '更新过', sourceTypeId: ET_A, targetTypeId: ET_B } })).status === 200)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      const rel = (s.json?.relationTypes ?? []).find((x) => Number(x.relationTypeId) === RT)
      check('KX-03i 更新后 schema 回显新名且约束保持', rel && rel.name === rtRenamed && Number(rel.sourceTypeId) === ET_A && Number(rel.targetTypeId) === ET_B, JSON.stringify(rel))
    }
    const tr2 = await api('POST', xkg(G1) + '/relation-types', { token: AT1, body: { name: 'KX临时关系-' + TS } })
    const RT2 = Number(tr2.json?.value)
    check('KX-03j 删除无引用关系类型 200', tr2.status === 200 && (await api('DELETE', `${xkg(G1)}/relation-types/${RT2}`, { token: AT1 })).status === 200)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      check('KX-03k schema 不再含已删关系类型', !(s.json?.relationTypes ?? []).some((x) => Number(x.relationTypeId) === RT2), JSON.stringify(s.json?.relationTypes?.map((x) => x.relationTypeId)))
    }

    // 删除守卫与按序清理（专用临时类型/节点，不干扰下游场景的计数与分页断言）
    const teC = await api('POST', xkg(G1) + '/entity-types', { token: AT1, body: { name: 'KX临时类型-' + TS } })
    const ET_C = Number(teC.json?.value)
    const trC = await api('POST', xkg(G1) + '/relation-types', { token: AT1, body: { name: 'KX引用关系-' + TS, sourceTypeId: ET_A, targetTypeId: ET_C } })
    const RT_C = Number(trC.json?.value)
    const nc = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_C, name: 'KX临时节点-' + TS } })
    check('KX-03l 仍有节点的实体类型删除 409', teC.status === 200 && trC.status === 200 && nc.status === 200 && (await api('DELETE', `${xkg(G1)}/entity-types/${ET_C}`, { token: AT1 })).status === 409, `${teC.status}/${trC.status}/${nc.status}`)
    check('KX-03m 删除临时节点 200（解除节点守卫）', (await api('DELETE', `${xkg(G1)}/nodes/${encodeURIComponent(nc.json?.value)}`, { token: AT1 })).status === 200)
    check('KX-03n 被关系类型引用的实体类型删除 409', (await api('DELETE', `${xkg(G1)}/entity-types/${ET_C}`, { token: AT1 })).status === 409)
    check('KX-03o 按序清理：先删引用它的关系类型 200', (await api('DELETE', `${xkg(G1)}/relation-types/${RT_C}`, { token: AT1 })).status === 200)
    {
      const delC = await api('DELETE', `${xkg(G1)}/entity-types/${ET_C}`, { token: AT1 })
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      check('KX-03p 再删实体类型 200 且 schema 消失', delC.status === 200 && !((s.json?.entityTypes ?? []).some((x) => Number(x.entityTypeId) === ET_C)), `${delC.status}`)
    }

    // ===== KX-04 节点 CRUD / 分页 / 邻接 =====
    const nAName = 'KX节点A-' + TS
    const na = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_A, name: nAName, description: '甲节点' } })
    const nb = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_B, name: 'KX节点B-' + TS } })
    check('KX-04a 建两个不同类型节点 200 返回 nodeId', na.status === 200 && nb.status === 200 && typeof na.json?.value === 'string' && na.json.value.length > 0, `${na.status}/${nb.status} ${na.text.slice(0, 120)}`)
    const NA = na.json?.value
    const NB = nb.json?.value

    const p1 = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 1 } })
    check('KX-04b 节点分页 pageNo=1/pageSize=1 截断且 total=2', p1.status === 200 && (p1.json?.items ?? []).length === 1 && Number(p1.json?.total) === 2, JSON.stringify({ n: p1.json?.items?.length, total: p1.json?.total }))
    const p2 = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 2, pageSize: 1 } })
    check('KX-04c 第 2 页取到剩余 1 条', p2.status === 200 && (p2.json?.items ?? []).length === 1 && Number(p2.json?.total) === 2, JSON.stringify({ n: p2.json?.items?.length, total: p2.json?.total }))

    const det = await api('GET', `${xkg(G1)}/nodes/${encodeURIComponent(NA)}`, { token: AT1 })
    check('KX-04d 节点详情回显名称与类型', det.status === 200 && det.json?.nodeId === NA && det.json?.name === nAName && Number(det.json?.entityTypeId) === ET_A, `${det.status} ${det.text.slice(0, 160)}`)

    const nb0 = await api('GET', `${xkg(G1)}/nodes/${encodeURIComponent(NA)}/neighbors?limit=50`, { token: AT1 })
    check('KX-04e 空邻接 200（0 节点 0 边）', nb0.status === 200 && (nb0.json?.nodes ?? []).length === 0 && (nb0.json?.edges ?? []).length === 0, `${nb0.status} ${nb0.text.slice(0, 120)}`)

    const nARenamed = 'KX节点甲-' + TS
    check('KX-04f 更新节点名称 200', (await api('PUT', `${xkg(G1)}/nodes/${encodeURIComponent(NA)}`, { token: AT1, body: { entityTypeId: ET_A, name: nARenamed } })).status === 200)
    {
      const det2 = await api('GET', `${xkg(G1)}/nodes/${encodeURIComponent(NA)}`, { token: AT1 })
      check('KX-04g 详情回显新名称', det2.status === 200 && det2.json?.name === nARenamed, JSON.stringify(det2.json?.name))
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      const cntA = Number((s.json?.entityTypes ?? []).find((x) => Number(x.entityTypeId) === ET_A)?.count)
      const cntB = Number((s.json?.entityTypes ?? []).find((x) => Number(x.entityTypeId) === ET_B)?.count)
      check('KX-04h schema 计数随写入变化（ET_A=1, ET_B=1）', cntA === 1 && cntB === 1, JSON.stringify({ cntA, cntB }))
    }

    // 删除节点 + DETACH DELETE 级联（临时节点与边，删完恢复基线，不影响 KX-05/06 的计数断言）
    // ND 用 ET_B：RT 约束为 ET_A→ET_B，NA→ND 才是合法边
    const nd = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_B, name: 'KX节点D-' + TS } })
    const ND = nd.json?.value
    const edD = await api('POST', xkg(G1) + '/edges', { token: AT1, body: { relationTypeId: RT, sourceNodeId: NA, targetNodeId: ND } })
    check('KX-04i 准备第 3 节点与一条边 200', nd.status === 200 && typeof ND === 'string' && edD.status === 200, `${nd.status}/${edD.status} ${edD.text.slice(0, 120)}`)
    const delN = await api('DELETE', `${xkg(G1)}/nodes/${encodeURIComponent(ND)}`, { token: AT1 })
    check('KX-04j 删除节点 200', delN.status === 200, `${delN.status} ${delN.text.slice(0, 120)}`)
    check('KX-04k 删除后节点 GET 404', (await api('GET', `${xkg(G1)}/nodes/${encodeURIComponent(ND)}`, { token: AT1 })).status === 404)
    {
      const s = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      const cntB = Number((s.json?.entityTypes ?? []).find((x) => Number(x.entityTypeId) === ET_B)?.count)
      check('KX-04l schema 计数回退（ET_B=1）', cntB === 1, JSON.stringify({ cntB }))
      const el0 = await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
      check('KX-04m DETACH 级联：节点删除后其边连带消失（edges total=0）', Number(el0.json?.total) === 0, JSON.stringify(el0.json?.total))
      const pl0 = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
      check('KX-04n 节点 total 回到 2', Number(pl0.json?.total) === 2, JSON.stringify(pl0.json?.total))
    }

    // ===== KX-05 边 CRUD（含约束校验）=====
    const e1 = await api('POST', xkg(G1) + '/edges', { token: AT1, body: { relationTypeId: RT, sourceNodeId: NA, targetNodeId: NB } })
    check('KX-05a 合法边 200 返回 edgeId', e1.status === 200 && typeof e1.json?.value === 'string' && e1.json.value.length > 0, `${e1.status} ${e1.text.slice(0, 120)}`)
    const E1 = e1.json?.value
    const bad = await api('POST', xkg(G1) + '/edges', { token: AT1, body: { relationTypeId: RT, sourceNodeId: NB, targetNodeId: NA } })
    check('KX-05b 起止类型违反关系约束 400', bad.status === 400, `${bad.status} ${bad.text.slice(0, 120)}`)

    const el = await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
    check('KX-05c 边分页 total=1', el.status === 200 && Number(el.json?.total) === 1 && (el.json?.items ?? [])[0]?.edgeId === E1, JSON.stringify(el.json).slice(0, 160))
    const ed = await api('GET', `${xkg(G1)}/edges/${encodeURIComponent(E1)}`, { token: AT1 })
    check('KX-05d 边详情回显类型与端点', ed.status === 200 && Number(ed.json?.relationTypeId) === RT && ed.json?.sourceNodeId === NA && ed.json?.targetNodeId === NB, `${ed.status} ${ed.text.slice(0, 160)}`)

    // 自由关系（无约束）用于换绑
    const tr3 = await api('POST', xkg(G1) + '/relation-types', { token: AT1, body: { name: 'KX自由关系-' + TS } })
    const RT3 = Number(tr3.json?.value)
    check('KX-05e 更新边换绑关系类型 200', tr3.status === 200 && (await api('PUT', `${xkg(G1)}/edges/${encodeURIComponent(E1)}`, { token: AT1, body: { relationTypeId: RT3 } })).status === 200)
    {
      const ed2 = await api('GET', `${xkg(G1)}/edges/${encodeURIComponent(E1)}`, { token: AT1 })
      check('KX-05f 换绑后边详情 relationTypeId 生效', ed2.status === 200 && Number(ed2.json?.relationTypeId) === RT3, JSON.stringify(ed2.json))
    }
    check('KX-05g 删除边 200 且列表清零', (await api('DELETE', `${xkg(G1)}/edges/${encodeURIComponent(E1)}`, { token: AT1 })).status === 200 && Number((await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })).json?.total) === 0)
    check('KX-05h 删除无引用关系类型（换绑用）200', (await api('DELETE', `${xkg(G1)}/relation-types/${RT3}`, { token: AT1 })).status === 200)

    // ===== KX-06 批量写入 =====
    const batchNodes = [
      { entityTypeId: ET_A, name: 'KX批量A1-' + TS },
      { entityTypeId: ET_A, name: 'KX批量A2-' + TS },
      { entityTypeId: ET_B, name: 'KX批量B1-' + TS },
    ]
    const bn = await api('POST', xkg(G1) + '/nodes/batch', { token: AT1, body: { items: batchNodes } })
    check('KX-06a 节点批量 3 条成功且逐条回 id', bn.status === 200 && bn.json?.successCount === 3 && bn.json?.failedCount === 0 && (bn.json?.results ?? []).length === 3 && bn.json.results.every((r) => r.ok === true && !!r.id), `${bn.status} ${bn.text.slice(0, 200)}`)
    const B = (bn.json?.results ?? []).map((r) => r.id)
    const totalAfter = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 1 } })
    check('KX-06b 批量后节点 total=5', Number(totalAfter.json?.total) === 5, JSON.stringify(totalAfter.json?.total))

    const bnBad = await api('POST', xkg(G1) + '/nodes/batch', { token: AT1, body: { items: [{ entityTypeId: ET_A, name: 'KX好-' + TS }, { entityTypeId: 999999, name: 'KX坏-' + TS }] } })
    const totalAfterBad = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 1 } })
    check('KX-06c 含非法 entityTypeId 整批 400 且计数不变', bnBad.status === 400 && Number(totalAfterBad.json?.total) === 5, `${bnBad.status} total=${totalAfterBad.json?.total}`)

    const bn201 = await api('POST', xkg(G1) + '/nodes/batch', { token: AT1, body: { items: Array.from({ length: 201 }, (_, i) => ({ entityTypeId: ET_A, name: `KX超限-${TS}-${i}` })) } })
    check('KX-06d 201 条超批上限 400（校验层）', bn201.status === 400, `${bn201.status} ${bn201.text.slice(0, 120)}`)

    const be = await api('POST', xkg(G1) + '/edges/batch', { token: AT1, body: { items: [
      { relationTypeId: RT, sourceNodeId: B[0], targetNodeId: B[2] },
      { relationTypeId: RT, sourceNodeId: B[1], targetNodeId: NB },
    ] } })
    check('KX-06e 边批量 2 条成功', be.status === 200 && be.json?.successCount === 2 && (be.json?.results ?? []).length === 2 && be.json.results.every((r) => !!r.id), `${be.status} ${be.text.slice(0, 200)}`)
    const el2 = await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
    check('KX-06f 批量后边 total=2', Number(el2.json?.total) === 2, JSON.stringify(el2.json?.total))

    const beBad = await api('POST', xkg(G1) + '/edges/batch', { token: AT1, body: { items: [
      { relationTypeId: RT, sourceNodeId: B[0], targetNodeId: B[2] },
      { relationTypeId: RT, sourceNodeId: 'ghost-node-' + TS, targetNodeId: B[2] },
    ] } })
    const el3 = await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
    check('KX-06g 含不存在端点整批 400 且计数不变', beBad.status === 400 && Number(el3.json?.total) === 2, `${beBad.status} total=${el3.json?.total}`)

    {
      const nbh = await api('GET', `${xkg(G1)}/nodes/${encodeURIComponent(NB)}/neighbors?limit=50`, { token: AT1 })
      check('KX-06h 邻接展开含批量节点与边', nbh.status === 200 && (nbh.json?.nodes ?? []).some((x) => x.nodeId === B[1]) && (nbh.json?.edges ?? []).length === 1, JSON.stringify(nbh.json).slice(0, 200))
    }

    // ===== KX-07 connected 图谱写操作 409 只读 =====
    if (CONN > 0) {
      check('KX-07a connected 图谱外部写节点 409', (await api('POST', xkg(CONN) + '/nodes', { token: AT1, body: { entityTypeId: ET_A, name: 'kx-readonly-' + TS } })).status === 409)
      check('KX-07b connected 图谱外部建实体类型 409', (await api('POST', xkg(CONN) + '/entity-types', { token: AT1, body: { name: 'kx-readonly-type-' + TS } })).status === 409)
    } else {
      kx07Skipped = true
      console.warn('WARN | KX-07 跳过：connected 图谱不可用（图数据库探活失败）')
    }

    // ===== KX-08 鉴权边界 =====
    check('KX-08a 无 token 调外部接口 401', (await api('POST', '/api/external/knowledge-graph/list', { body: {} })).status === 401)
    check('KX-08b 伪造 token 401', (await api('POST', '/api/external/knowledge-graph/list', { token: 'kx-invalid-token', body: {} })).status === 401)
    {
      const internal = await api('POST', '/api/external/knowledge-graph/list', { token, body: {} })
      check('KX-08c 内部用户 JWT 调外部接口 401/403', internal.status === 401 || internal.status === 403, `${internal.status}`)
    }
  } finally {
    // ===== 清理：删图谱（连带类型/节点/边）、接入点、团队；可重复执行 =====
    await api('DELETE', kg(G1), { token })
    await api('DELETE', kg(G2), { token })
    if (CONN > 0) await api('DELETE', kg(CONN), { token })
    if (ACC1) await api('DELETE', `/api/access-app/${ACC1}`, { token })
    if (ACC2) await api('DELETE', `/api/access-app/${ACC2}`, { token })
    await api('DELETE', `/api/team/${T1}`, { token })
    await api('DELETE', `/api/team/${T2}`, { token })
  }

  const skipNote = kx07Skipped ? ' | KX-07 skipped (connected 探活失败)' : ''
  console.log(`\n===== 知识图谱外部接口 E2E 汇总: PASS=${PASS} FAIL=${FAIL}${skipNote} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
