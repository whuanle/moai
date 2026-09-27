// 知识图谱外部接口 E2E（真实 HTTP；复用 kg-e2e / external-app-e2e 同款登录/断言 helper；后端 127.0.0.1:5210）
// 场景编号 KX-01..KX-14（新编号，不复用 KG-S*）：/api/external/knowledge-graph 仅接受应用 token（团队级授权），并按 kg_read/kg_write 分档校验
// KX-10..KX-12：外部批量导入（/import 类型名引用+业务key幂等upsert+逐条结果）、按key批删/查询、connected 409（图库不可达时跳过）
// KX-13..KX-14：按 key 同步闭环（keys/list 枚举 + edges/batch-delete 按引用删边 + 单节点 key 收养）、导入预检 validateOnly
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
    // ===== KX-09 知识图谱读/写范围（kg_read/kg_write）=====
    {
      const accRO = await api('POST', '/api/access-app', { token, body: { teamId: T1, name: 'kx接入只读-' + TS, description: 'kg scope e2e', scopes: ['kg_read'] } })
      const KEY_RO = accRO.json?.key
      const ID_RO = accRO.json?.accessAppId
      check('KX-09a 创建 kg_read 只读接入 200', accRO.status === 200, `${accRO.status} ${accRO.text.slice(0, 120)}`)

      const tokRO = await api('POST', '/api/external/token', { body: { accessAppKey: KEY_RO } })
      const AT_RO = tokRO.json?.accessToken
      check('KX-09b 只读接入换应用 token 200', tokRO.status === 200, `${tokRO.status}`)

      const roSchema = await api('GET', xkg(G1) + '/schema', { token: AT_RO })
      check('KX-09c kg_read token 读 schema 200', roSchema.status === 200, `${roSchema.status} ${roSchema.text.slice(0, 120)}`)
      const roList = await api('POST', xkg(G1) + '/nodes/list', { token: AT_RO, body: { pageNo: 1, pageSize: 10 } })
      check('KX-09d kg_read token 节点列表 200', roList.status === 200, `${roList.status}`)
      const roWrite = await api('POST', xkg(G1) + '/entity-types', { token: AT_RO, body: { name: 'kx-写权限应拒-' + TS, color: '#ef4444' } })
      check('KX-09e kg_read token 建实体类型 403', roWrite.status === 403, `${roWrite.status} ${roWrite.text.slice(0, 120)}`)
      const roWiki = await api('POST', '/api/external/wiki/list', { token: AT_RO, body: {} })
      check('KX-09f kg_read token 无 wiki_read 跨资源组 403', roWiki.status === 403, `${roWiki.status}`)

      // 直连 key：kg_read 直连读放行、写 403
      const dSchema = await fetch(`${BASE}${xkg(G1)}/schema`, { headers: { Authorization: `Bearer ${KEY_RO}` } })
      check('KX-09g kg_read 直连读 schema 200', dSchema.status === 200, `${dSchema.status}`)
      const dWrite = await fetch(`${BASE}${xkg(G1)}/nodes`, { method: 'POST', headers: { Authorization: `Bearer ${KEY_RO}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ entityTypeId: 1, name: 'kx-直连写拒-' + TS }) })
      check('KX-09h kg_read 直连写节点 403', dWrite.status === 403, `${dWrite.status}`)

      // 补 kg_write 后 token 刷新写放行（范围以接入当前勾选为准）
      await api('PUT', `/api/access-app/${ID_RO}`, { token, body: { name: 'kx接入只读-' + TS, description: 'kg scope e2e', scopes: ['kg_read', 'kg_write'] } })
      const refRO = await api('POST', '/api/external/token/refresh', { body: { refreshToken: tokRO.json?.refreshToken } })
      const rwWrite = await api('POST', xkg(G1) + '/entity-types', { token: refRO.json?.accessToken, body: { name: 'kx-补写后-' + TS, color: '#22c55e' } })
      check('KX-09i 补 kg_write 刷新后建实体类型 200', refRO.status === 200 && rwWrite.status === 200, `ref=${refRO.status} write=${rwWrite.status} ${rwWrite.text.slice(0, 120)}`)

      await api('DELETE', `/api/access-app/${ID_RO}`, { token })
    }

    // ===== KX-10 外部批量导入：类型名自动创建 + 业务 key + 名称引用 + 逐条失败报告 =====
    const personType = 'KX导入人员-' + TS
    const companyType = 'KX导入公司-' + TS
    const workRel = 'KX任职-' + TS
    const nameZ = '张三-' + TS
    const nameAcme = 'Acme-' + TS
    const nameGlobex = 'Globex-' + TS
    const importBody = {
      mode: 'upsert',
      autoCreateTypes: true,
      nodes: [
        { key: 'kx-p-1', entityTypeName: personType, name: nameZ, description: '工程师', properties: { city: '深圳', age: '30' } },
        { key: 'kx-p-2', entityTypeName: companyType, name: nameAcme },
        { key: 'kx-p-3', entityTypeName: companyType, name: nameGlobex },
        { entityTypeId: 999999, name: 'KX坏类型行-' + TS },
        { key: 'kx-p-1', entityTypeName: personType, name: 'KX重复key行-' + TS },
      ],
      edges: [
        { relationTypeName: workRel, source: { key: 'kx-p-1' }, target: { name: nameAcme, entityTypeName: companyType } },
        { relationTypeName: workRel, source: { name: nameZ }, target: { key: 'kx-p-3' } },
        { relationTypeName: workRel, source: { key: 'kx-p-9' }, target: { key: 'kx-p-2' } },
      ],
    }
    {
      const imp = await api('POST', xkg(G1) + '/import', { token: AT1, body: importBody })
      check('KX-10a 导入 200 计数：节点 created=3 failed=2 / 边 created=2 failed=1', imp.status === 200
        && imp.json?.nodeCreatedCount === 3 && imp.json?.nodeUpdatedCount === 0 && imp.json?.nodeFailedCount === 2
        && imp.json?.edgeCreatedCount === 2 && imp.json?.edgeFailedCount === 1, `${imp.status} ${imp.text.slice(0, 240)}`)
      check('KX-10b 自动创建类型回显（2 实体类型 + 1 关系类型）', (imp.json?.createdEntityTypeNames ?? []).includes(personType)
        && (imp.json?.createdEntityTypeNames ?? []).includes(companyType) && (imp.json?.createdRelationTypeNames ?? []).includes(workRel), JSON.stringify(imp.json?.createdEntityTypeNames))
      const nodeResults = imp.json?.results?.filter((x) => x.kind === 'node') ?? []
      const edgeResults = imp.json?.results?.filter((x) => x.kind === 'edge') ?? []
      check('KX-10c 坏行逐条报告：node idx3 类型不存在 / idx4 key 重复', nodeResults[3]?.ok === false && (nodeResults[3]?.message ?? '').includes('实体类型')
        && nodeResults[4]?.ok === false && (nodeResults[4]?.message ?? '').includes('key'), JSON.stringify(nodeResults))
      check('KX-10d 好行回 id：node idx0-2 / edge idx0-1，坏边 idx2 报 key 未命中', nodeResults.slice(0, 3).every((x) => x.ok === true && !!x.id)
        && edgeResults[0]?.ok === true && edgeResults[1]?.ok === true && !!edgeResults[0].id
        && edgeResults[2]?.ok === false && (edgeResults[2]?.message ?? '').includes('key'), JSON.stringify(edgeResults))

      const byKey = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-1')}`, { token: AT1 })
      check('KX-10e by-key 查询 200 回显名称与属性', byKey.status === 200 && byKey.json?.name === nameZ && byKey.json?.properties?.city === '深圳' && byKey.json?.properties?.age === '30', `${byKey.status} ${byKey.text.slice(0, 200)}`)
      check('KX-10f by-key 不存在 404', (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-none')}`, { token: AT1 })).status === 404)

      const schema = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      const cntPerson = Number((schema.json?.entityTypes ?? []).find((x) => x.name === personType)?.count)
      const cntCompany = Number((schema.json?.entityTypes ?? []).find((x) => x.name === companyType)?.count)
      check('KX-10g schema 计数：人员=1 公司=2（自动建类型含推断属性）', cntPerson === 1 && cntCompany === 2, JSON.stringify({ cntPerson, cntCompany }))
    }

    // ===== KX-11 幂等重导：key upsert 更新 + 边去重 + key 收养 + 只读门禁 + 按 key 批删 =====
    {
      const reBody = JSON.parse(JSON.stringify(importBody))
      reBody.nodes[0] = { key: 'kx-p-1', entityTypeName: personType, name: '张三改-' + TS, properties: { city: '杭州' } }
      reBody.nodes.push({ key: 'kx-p-2b', entityTypeName: companyType, name: nameAcme })
      // 边改用 key 引用：节点改名后仍能稳定解析（key 的核心价值）
      reBody.edges = [
        { relationTypeName: workRel, source: { key: 'kx-p-1' }, target: { key: 'kx-p-2b' } },
        { relationTypeName: workRel, source: { key: 'kx-p-1' }, target: { key: 'kx-p-3' } },
      ]
      const imp2 = await api('POST', xkg(G1) + '/import', { token: AT1, body: reBody })
      check('KX-11a 幂等重导：节点 updated=4 created=0（含 key 收养行）/ 边 skipped=2 created=0', imp2.status === 200
        && imp2.json?.nodeUpdatedCount === 4 && imp2.json?.nodeCreatedCount === 0
        && imp2.json?.edgeSkippedCount === 2 && imp2.json?.edgeCreatedCount === 0, `${imp2.status} ${imp2.text.slice(0, 240)}`)

      const det = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-1')}`, { token: AT1 })
      check('KX-11b upsert 整体覆盖：新名称+city=杭州+age 被清空', det.status === 200 && det.json?.name === '张三改-' + TS
        && det.json?.properties?.city === '杭州' && det.json?.properties?.age === undefined, `${det.status} ${det.text.slice(0, 200)}`)

      const adopt = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-2b')}`, { token: AT1 })
      const oldKey = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-2')}`, { token: AT1 })
      check('KX-11c 无 key 行按（类型+名称）匹配并收养新 key：kx-p-2b 200 / kx-p-2 404', adopt.status === 200 && adopt.json?.name === nameAcme && oldKey.status === 404, `adopt=${adopt.status} old=${oldKey.status}`)

      // 只读 token：导入/批删是写档 403，by-key 是读档放行（验证新路由分档正确）
      const accRO2 = await api('POST', '/api/access-app', { token, body: { teamId: T1, name: 'kx接入导入只读-' + TS, description: 'import scope e2e', scopes: ['kg_read'] } })
      const tokRO2 = await api('POST', '/api/external/token', { body: { accessAppKey: accRO2.json?.key } })
      const AT_RO2 = tokRO2.json?.accessToken
      check('KX-11d kg_read token 导入 403', (await api('POST', xkg(G1) + '/import', { token: AT_RO2, body: importBody })).status === 403)
      check('KX-11e kg_read token 批删 403', (await api('POST', xkg(G1) + '/nodes/batch-delete', { token: AT_RO2, body: { keys: ['kx-p-1'] } })).status === 403)
      check('KX-11f kg_read token by-key 查询 200', (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-1')}`, { token: AT_RO2 })).status === 200)
      await api('DELETE', `/api/access-app/${accRO2.json?.accessAppId}`, { token })

      const del = await api('POST', xkg(G1) + '/nodes/batch-delete', { token: AT1, body: { keys: ['kx-p-1', 'kx-p-2b', 'kx-p-3', 'kx-p-none'] } })
      check('KX-11g 按 key 批删 deletedCount=3（不存在的 key 忽略）', del.status === 200 && del.json?.deletedCount === 3, `${del.status} ${del.text.slice(0, 160)}`)
      check('KX-11h 删除后 by-key 404', (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-p-1')}`, { token: AT1 })).status === 404)
      const gone = await api('POST', xkg(G1) + '/nodes/list', { token: AT1, body: { pageNo: 1, pageSize: 10, keyword: '张三' } })
      check('KX-11i 批删连带边（DETACH）：keyword=张三 节点 0 条', gone.status === 200 && Number(gone.json?.total) === 0, `${gone.status} total=${gone.json?.total}`)
    }

    // ===== KX-12 connected 图谱导入 409 只读 =====
    if (CONN > 0) {
      check('KX-12 connected 图谱外部导入 409', (await api('POST', xkg(CONN) + '/import', { token: AT1, body: { nodes: [{ entityTypeName: 't', name: 'n' }] } })).status === 409)
    } else {
      console.warn('WARN | KX-12 跳过：connected 图谱不可用（图数据库探活失败）')
    }

    // ===== KX-13 按 key 同步闭环：单节点 key 收养 + keys/list 枚举 + 按引用删边 =====
    {
      const syncRel = 'KX协作-' + TS
      const syncImp = await api('POST', xkg(G1) + '/import', { token: AT1, body: {
        mode: 'upsert', autoCreateTypes: true,
        nodes: [
          { key: 'kx-s-1', entityTypeName: personType, name: '同步甲-' + TS },
          { key: 'kx-s-2', entityTypeName: personType, name: '同步乙-' + TS },
          { key: 'kx-s-3', entityTypeName: companyType, name: '同步丙-' + TS },
        ],
        edges: [
          { relationTypeName: syncRel, source: { key: 'kx-s-1' }, target: { key: 'kx-s-2' } },
          { relationTypeName: syncRel, source: { key: 'kx-s-1' }, target: { key: 'kx-s-3' } },
        ],
      } })
      check('KX-13a 同步数据导入 200（节点 created=3 边 created=2）', syncImp.status === 200 && syncImp.json?.nodeCreatedCount === 3 && syncImp.json?.edgeCreatedCount === 2, `${syncImp.status} ${syncImp.text.slice(0, 200)}`)

      const solo = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_A, name: '同步单建-' + TS, key: 'kx-s-solo' } })
      check('KX-13b 单节点带 key 创建 200 且 by-key 可查', solo.status === 200 && (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-s-solo')}`, { token: AT1 })).status === 200, `${solo.status}`)
      const solo2 = await api('POST', xkg(G1) + '/nodes', { token: AT1, body: { entityTypeId: ET_A, name: '同步收养-' + TS } })
      await api('PUT', `${xkg(G1)}/nodes/${encodeURIComponent(solo2.json?.value)}`, { token: AT1, body: { entityTypeId: ET_A, name: '同步收养-' + TS, key: 'kx-s-adopt' } })
      const adoptDet = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-s-adopt')}`, { token: AT1 })
      check('KX-13c PUT 附 key 收养后 by-key 命中同一节点', adoptDet.status === 200 && adoptDet.json?.nodeId === solo2.json?.value, `${adoptDet.status}`)

      const keysP1 = await api('POST', xkg(G1) + '/nodes/keys/list', { token: AT1, body: { pageNo: 1, pageSize: 3 } })
      const keysP2 = await api('POST', xkg(G1) + '/nodes/keys/list', { token: AT1, body: { pageNo: 2, pageSize: 3 } })
      const allKeys = [...(keysP1.json?.items ?? []), ...(keysP2.json?.items ?? [])]
      check('KX-13d keys/list 第 1 页 3 条字段齐全（key/nodeId/name）', keysP1.status === 200 && (keysP1.json?.items ?? []).length === 3 && (keysP1.json?.items ?? []).every((x) => x.key && x.nodeId && x.name), `${keysP1.status} ${keysP1.text.slice(0, 200)}`)
      check('KX-13e keys/list 第 2 页余 2 条', keysP2.status === 200 && (keysP2.json?.items ?? []).length === 2, JSON.stringify(keysP2.json?.items?.length))
      check('KX-13f keys/list 含导入 key（kx-s-1）', allKeys.some((x) => x.key === 'kx-s-1'), JSON.stringify(allKeys.map((x) => x.key)))

      const delEdge = await api('POST', xkg(G1) + '/edges/batch-delete', { token: AT1, body: { items: [
        { relationTypeName: syncRel, source: { key: 'kx-s-1' }, target: { key: 'kx-s-2' } },
      ] } })
      check('KX-13g 按引用删边 deletedCount=1', delEdge.status === 200 && delEdge.json?.deletedCount === 1, `${delEdge.status} ${delEdge.text.slice(0, 200)}`)
      const delEdge2 = await api('POST', xkg(G1) + '/edges/batch-delete', { token: AT1, body: { items: [
        { relationTypeName: syncRel, source: { key: 'kx-s-1' }, target: { key: 'kx-s-2' } },
        { relationTypeName: syncRel, source: { key: 'kx-s-ghost' }, target: { key: 'kx-s-2' } },
      ] } })
      check('KX-13h 重删幂等 0、幽灵 key 行失败不阻断', delEdge2.status === 200 && delEdge2.json?.deletedCount === 0 && delEdge2.json?.results?.[0]?.ok === true && delEdge2.json?.results?.[1]?.ok === false, `${delEdge2.status} ${delEdge2.text.slice(0, 200)}`)
      const s1Node = (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-s-1')}`, { token: AT1 })).json?.nodeId
      const elAfter = await api('POST', xkg(G1) + '/edges/list', { token: AT1, body: { pageNo: 1, pageSize: 10, nodeId: s1Node } })
      check('KX-13i kx-s-1 剩 1 条边（另一条未误删）', elAfter.status === 200 && Number(elAfter.json?.total) === 1, `total=${elAfter.json?.total}`)

      await api('POST', xkg(G1) + '/edges/batch-delete', { token: AT1, body: { items: [{ relationTypeName: syncRel, source: { key: 'kx-s-1' }, target: { key: 'kx-s-3' } }] } })
      const delSyncNodes = await api('POST', xkg(G1) + '/nodes/batch-delete', { token: AT1, body: { keys: ['kx-s-1', 'kx-s-2', 'kx-s-3', 'kx-s-solo', 'kx-s-adopt'] } })
      const keysEmpty = await api('POST', xkg(G1) + '/nodes/keys/list', { token: AT1, body: { pageNo: 1, pageSize: 10 } })
      check('KX-13j 先删边后删点：deletedCount=5 且 keys/list 清空', delSyncNodes.status === 200 && delSyncNodes.json?.deletedCount === 5 && (keysEmpty.json?.items ?? []).length === 0, `del=${delSyncNodes.json?.deletedCount} keys=${JSON.stringify(keysEmpty.json?.items?.length)}`)
    }

    // ===== KX-14 导入预检 validateOnly：只读预测不落库 =====
    {
      const vType = 'KX预检类型-' + TS
      const vRel = 'KX预检关系-' + TS
      const vBody = {
        mode: 'upsert', autoCreateTypes: true, validateOnly: true,
        nodes: [
          { key: 'kx-v-1', entityTypeName: vType, name: '预检甲-' + TS, properties: { p1: 'x' } },
          { key: 'kx-v-1', entityTypeName: vType, name: '预检重复key-' + TS },
        ],
        edges: [
          { relationTypeName: vRel, source: { key: 'kx-v-1' }, target: { key: 'kx-v-1' } },
          { relationTypeName: vRel, source: { key: 'kx-v-1' }, target: { key: 'kx-v-none' } },
        ],
      }
      const v = await api('POST', xkg(G1) + '/import', { token: AT1, body: vBody })
      check('KX-14a 预检 200 预测计数：节点 created=1 failed=1 / 边 created=1 failed=1', v.status === 200
        && v.json?.nodeCreatedCount === 1 && v.json?.nodeFailedCount === 1
        && v.json?.edgeCreatedCount === 1 && v.json?.edgeFailedCount === 1, `${v.status} ${v.text.slice(0, 240)}`)
      const vNodeResults = v.json?.results?.filter((x) => x.kind === 'node') ?? []
      check('KX-14b 预检逐条结果 id 恒空（null 字段被 WhenWritingNull 省略）且 action=created', vNodeResults[0]?.ok === true && vNodeResults[0]?.id == null && vNodeResults[0]?.action === 'created', JSON.stringify(vNodeResults))
      check('KX-14c 预检回显将创建类型（实体+关系）', (v.json?.createdEntityTypeNames ?? []).includes(vType) && (v.json?.createdRelationTypeNames ?? []).includes(vRel), JSON.stringify(v.json?.createdEntityTypeNames))
      check('KX-14d 预检不落库：by-key 404', (await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-v-1')}`, { token: AT1 })).status === 404)
      const vSchema = await api('GET', xkg(G1) + '/schema', { token: AT1 })
      check('KX-14e 预检不建类型：schema 无该类型', !(vSchema.json?.entityTypes ?? []).some((x) => x.name === vType), JSON.stringify((vSchema.json?.entityTypes ?? []).map((x) => x.name)))

      const real = await api('POST', xkg(G1) + '/import', { token: AT1, body: { ...vBody, validateOnly: false } })
      const realByKey = await api('GET', `${xkg(G1)}/nodes/by-key/${encodeURIComponent('kx-v-1')}`, { token: AT1 })
      check('KX-14f 同体真导入：created=1 且 by-key 200', real.status === 200 && real.json?.nodeCreatedCount === 1 && realByKey.status === 200, `${real.status} ${real.text.slice(0, 200)}`)
      check('KX-14g 真导入边入库（自环边、约束任意）', real.json?.edgeCreatedCount === 1, JSON.stringify(real.json?.edgeCreatedCount))
      await api('POST', xkg(G1) + '/nodes/batch-delete', { token: AT1, body: { keys: ['kx-v-1'] } })
    }

  } finally {
    // ===== 清理：删图谱（连带类型/节点/边）、接入点、团队；可重复执行 =====
    await api('DELETE', kg(G1), { token })
    await api('DELETE', kg(G2), { token })
    if (CONN > 0) await api('DELETE', kg(CONN), { token })
    if (ACC1) await api('DELETE', `/api/access-app/${ACC1}`, { token })
    if (ACC2) await api('DELETE', `/api/access-app/${ACC2}`, { token })
    // 团队不可解散：清理改为管理员禁用归档
    await api('PUT', `/api/admin/team/${T1}/disable`, { token, body: { isDisable: true } })
    await api('PUT', `/api/admin/team/${T2}/disable`, { token, body: { isDisable: true } })
  }

  const skipNote = kx07Skipped ? ' | KX-07 skipped (connected 探活失败)' : ''
  console.log(`\n===== 知识图谱外部接口 E2E 汇总: PASS=${PASS} FAIL=${FAIL}${skipNote} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('脚本异常:', e); process.exit(2) })
