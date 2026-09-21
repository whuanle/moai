// 知识图谱 Text2Cypher 插件 E2E（真实 HTTP；后端 127.0.0.1:5210，可用 argv[2] 覆盖）
// 场景编号 @KT-S1..S10，与 docs/knowledgegraph/bdd.md 的 Text2Cypher 章节一一对应：
//   KT-S1 模板注册（dynamic_templates 含 kg_cypher_query 且 isDynamic）+ 团队实例创建 200
//   KT-S2 越团队绑定：T2 实例绑定 T1 的图谱 403
//   KT-S3 Schema 自描述：{Schema:true} 返回 EntityTypes≥1 且 Usage 含 $kgId
//   KT-S4 托管图带 $kgId 只读查询成功且 RowCount≥1
//   KT-S5 托管图缺 $kgId 被拒且错误提示含 $kgId
//   KT-S6 只读守卫：CREATE/MERGE/DETACH DELETE/SET/CALL 全部拒绝
//   KT-S7 MaxRows 截断：RowCount==2 且 Truncated==true
//   KT-S8 超时场景（依赖慢查询负载，SKIP 手动验证）
//   KT-S9 接入图（connected）查询无需 $kgId 成功
//   KT-S10 跨团队运行实例：404 或 success=false
// 运行前置：后端必须是含 kg_cypher_query 模板的**新构建**（旧构建无此模板必然失败，勿对旧构建执行）
//   + Memgraph/图数据库可达 + KG_ENABLED=true；未开启/连不上时打印 SKIP 并退出码 0（CI 无图数据库不失败）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const skip = (reason) => {
  console.warn(`\nSKIP | 知识图谱 Text2Cypher E2E 未执行: ${reason}`)
  console.warn('      需要含 kg_cypher_query 新构建的后端 + 图数据库可达 且 KG_ENABLED=true。')
  process.exit(0)
}

// dataJson 是插件结果的 JSON 文本，内部属性名经 STJ 默认序列化为 PascalCase；
// HTTP 响应外层字段为 camelCase。对内层字段断言一律用 pick 兼容两种大小写，不要写死。
const safeParse = (s) => { try { return JSON.parse(s) } catch { return null } }
const pick = (obj, name) => {
  if (obj == null || typeof obj !== 'object') return undefined
  if (Object.prototype.hasOwnProperty.call(obj, name)) return obj[name]
  const camel = name.charAt(0).toLowerCase() + name.slice(1)
  if (Object.prototype.hasOwnProperty.call(obj, camel)) return obj[camel]
  return undefined
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
const saveInstance = (token, teamId, instanceKey, kgId, maxRows) => api('POST', `/api/team/${teamId}/plugin/dynamic`, {
  token,
  body: {
    teamId,
    instanceKey,
    templeteKey: 'kg_cypher_query',
    title: 'E2E 图查询',
    description: 'kg text2cypher e2e',
    config: JSON.stringify({ KgId: kgId, MaxRows: maxRows, TimeoutSeconds: 30 }),
    classifyId: 0,
  },
})
const runInstance = (token, teamId, key, request) => api('POST', `/api/team/${teamId}/plugin/run`, { token, body: { teamId, key, requestJson: JSON.stringify(request) } })

async function main() {
  // 能力开关的本地快速短路；真正以服务端 list.enabled 为准
  const envFlag = process.env.KG_ENABLED
  if (envFlag !== undefined && !['true', '1'].includes(String(envFlag).toLowerCase())) {
    skip(`环境变量 KG_ENABLED=${envFlag}`)
  }

  let si
  try {
    si = await api('GET', '/api/common/serverinfo')
  } catch (e) {
    skip(`后端不可达（${BASE}）：${e.message}`)
  }
  if (si.status !== 200 || !si.json?.rsaPublic) {
    skip(`serverinfo 获取失败 (${si.status})`)
  }
  RSA_KEY = si.json.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (login.status !== 200 || !login.json?.accessToken) {
    throw new Error(`root 登录失败: ${login.status} ${login.text.slice(0, 120)}`)
  }
  const token = login.json.accessToken

  // 准备团队 T1：root 创建即 Owner，满足团队 Owner/Admin 门禁
  const team1 = await api('POST', '/api/team', { token, body: { name: 'kg-t2cy-' + TS } })
  if (team1.status !== 200 || !Number(team1.json?.value)) {
    throw new Error(`创建团队 T1 失败: ${team1.status} ${team1.text.slice(0, 120)}`)
  }
  const T1 = Number(team1.json.value)

  // 能力检测：list.enabled
  const list0 = await api('GET', `/api/knowledge-graph/list?teamId=${T1}`, { token })
  if (list0.status !== 200) {
    throw new Error(`GET list 失败: ${list0.status} ${list0.text.slice(0, 120)}`)
  }
  if (list0.json?.enabled !== true) {
    skip(`list.enabled=${list0.json?.enabled}`)
  }

  // ===== 准备：空白托管图 + 实体类型/关系类型/节点/边（照 kg-e2e S4/S5/S7 的建法）=====
  const c1 = await api('POST', '/api/knowledge-graph', { token, body: { teamId: T1, name: 'kg-t2cy-' + TS, templateKey: 'blank' } })
  if (c1.status !== 200 || !Number(c1.json?.value)) {
    throw new Error(`创建空白托管图失败: ${c1.status} ${c1.text.slice(0, 160)}`)
  }
  const GRAPH_ID = Number(c1.json.value)

  const tWh = await api('POST', `${kg(GRAPH_ID)}/entity-types`, { token, body: { name: '仓库' } })
  const tGd = await api('POST', `${kg(GRAPH_ID)}/entity-types`, { token, body: { name: '货物' } })
  if (tWh.status !== 200 || tGd.status !== 200 || !Number(tWh.json?.value) || !Number(tGd.json?.value)) {
    throw new Error(`建实体类型失败: ${tWh.status}/${tGd.status} ${tWh.text.slice(0, 120)}`)
  }
  const WH = Number(tWh.json.value)
  const GD = Number(tGd.json.value)
  const tRel = await api('POST', `${kg(GRAPH_ID)}/relation-types`, { token, body: { name: '存放于', sourceTypeId: GD, targetTypeId: WH } })
  if (tRel.status !== 200 || !Number(tRel.json?.value)) {
    throw new Error(`建关系类型失败: ${tRel.status} ${tRel.text.slice(0, 120)}`)
  }
  const KEEP = Number(tRel.json.value)

  const seedNode = (entityTypeId, name) => api('POST', `${kg(GRAPH_ID)}/nodes`, { token, body: { entityTypeId, name } })
  const nWh = await seedNode(WH, '中心仓库-' + TS)
  const nGd1 = await seedNode(GD, '货物甲-' + TS)
  const nGd2 = await seedNode(GD, '货物乙-' + TS)
  const nGd3 = await seedNode(GD, '货物丙-' + TS)
  if ([nWh, nGd1, nGd2, nGd3].some((r) => r.status !== 200 || !r.json?.value)) {
    throw new Error(`建节点失败: ${[nWh, nGd1, nGd2, nGd3].map((r) => r.status).join('/')}`)
  }
  const e1 = await api('POST', `${kg(GRAPH_ID)}/edges`, { token, body: { relationTypeId: KEEP, sourceNodeId: nGd1.json?.value, targetNodeId: nWh.json?.value } })
  const e2 = await api('POST', `${kg(GRAPH_ID)}/edges`, { token, body: { relationTypeId: KEEP, sourceNodeId: nGd2.json?.value, targetNodeId: nWh.json?.value } })
  if (e1.status !== 200 || e2.status !== 200) {
    throw new Error(`建边失败: ${e1.status}/${e2.status} ${e1.text.slice(0, 120)}`)
  }

  // 接入图探活兼夹具：建 connected 图成功即图库可达（KT-S9 用）；失败只降级，不中断托管图场景
  let CONN = 0
  {
    const probe = await api('POST', '/api/knowledge-graph', { token, body: { teamId: T1, name: 'kg-t2cy-conn-' + TS, mode: 'connected', database: 'neo4j' } })
    if (probe.status === 200 && Number(probe.json?.value)) {
      CONN = Number(probe.json.value)
    } else {
      console.warn(`WARN | 接入图探活失败 (${probe.status})，KT-S9 将跳过: ${probe.text.slice(0, 160)}`)
    }
  }

  // ===== KT-S1 模板注册 + 创建实例 =====
  const KEY1 = `kgcy_t1_${TS}`
  const create1 = await saveInstance(token, T1, KEY1, GRAPH_ID, 200)
  check('KT-S1a 创建 kg_cypher_query 实例 200', create1.status === 200, `${create1.status} ${create1.text.slice(0, 160)}`)
  const tpls = await api('GET', `/api/team/${T1}/plugin/dynamic_templates`, { token })
  const tplItems = tpls.json?.items ?? []
  const kgTpl = tplItems.find((x) => x.key === 'kg_cypher_query')
  check('KT-S1b 模板列表含 kg_cypher_query 且 isDynamic', tpls.status === 200 && Boolean(kgTpl) && kgTpl.isDynamic === true, JSON.stringify(tplItems.map((x) => x.key)))

  // ===== KT-S2 越团队绑定 403（T2 实例绑定 T1 的图谱）=====
  const team2 = await api('POST', '/api/team', { token, body: { name: 'kg-t2cy2-' + TS } })
  const T2 = Number(team2.json?.value)
  if (!Number.isFinite(T2) || T2 <= 0) {
    throw new Error(`创建团队 T2 失败: ${team2.status} ${team2.text.slice(0, 120)}`)
  }
  const cross = await saveInstance(token, T2, `kgcy_t2_${TS}`, GRAPH_ID, 50)
  check('KT-S2 越团队绑定图谱 403', cross.status === 403, `${cross.status} ${cross.text.slice(0, 160)}`)

  // ===== KT-S3 Schema 自描述 =====
  {
    const r = await runInstance(token, T1, KEY1, { Schema: true })
    const d = safeParse(r.json?.dataJson ?? '')
    const entityTypes = pick(d, 'EntityTypes') ?? []
    const usage = String(pick(d, 'Usage') ?? '')
    check('KT-S3 Schema 自描述成功且 EntityTypes≥1、Usage 含 $kgId',
      r.status === 200 && r.json?.success === true && entityTypes.length >= 1 && usage.includes('$kgId'),
      `${r.status} ${r.json?.success} ${String(r.json?.dataJson ?? r.json?.error ?? '').slice(0, 200)}`)
  }

  // ===== KT-S4 带 $kgId 只读查询成功 =====
  {
    const r = await runInstance(token, T1, KEY1, { Cypher: 'MATCH (n:KgNode {kgId: $kgId}) RETURN n.name AS name LIMIT 5' })
    const d = safeParse(r.json?.dataJson ?? '')
    const rowCount = Number(pick(d, 'RowCount') ?? 0)
    check('KT-S4 带 $kgId 查询成功且 RowCount≥1', r.status === 200 && r.json?.success === true && rowCount >= 1, `${r.status} ${String(r.json?.dataJson ?? r.json?.error ?? '').slice(0, 200)}`)
  }

  // ===== KT-S5 缺 $kgId 被拒 =====
  {
    const r = await runInstance(token, T1, KEY1, { Cypher: 'MATCH (n:KgNode) RETURN n LIMIT 5' })
    check('KT-S5 缺 $kgId 查询被拒且错误提示含 $kgId', r.json?.success === false && String(r.json?.error ?? '').includes('$kgId'), `${r.status} ${String(r.json?.error ?? '').slice(0, 200)}`)
  }

  // ===== KT-S6 只读守卫：写语句全部拒绝（守卫先于连接，不依赖图库可达）=====
  const writes = [
    ['CREATE', 'CREATE (n:KgNode {kgId: 1}) RETURN n'],
    ['MERGE', 'MERGE (n:KgNode {kgId: 1}) RETURN n'],
    ['DETACH DELETE', 'MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n'],
    ['SET', "MATCH (n:KgNode {kgId: $kgId}) SET n.name = 'hacked'"],
    ['CALL', 'CALL db.labels() YIELD label RETURN label'],
  ]
  for (const [label, cypher] of writes) {
    const r = await runInstance(token, T1, KEY1, { Cypher: cypher })
    check(`KT-S6 只读守卫拒绝 ${label}`, r.json?.success === false && /只读/.test(String(r.json?.error ?? '')), `${r.status} ${String(r.json?.error ?? '').slice(0, 160)}`)
  }

  // ===== KT-S7 MaxRows 截断（图内 4 节点 > MaxRows=2）=====
  {
    const KEYCAP = `kgcy_cap_${TS}`
    const createCap = await saveInstance(token, T1, KEYCAP, GRAPH_ID, 2)
    check('KT-S7a 创建 MaxRows=2 实例 200', createCap.status === 200, `${createCap.status} ${createCap.text.slice(0, 160)}`)
    if (createCap.status === 200) {
      const r = await runInstance(token, T1, KEYCAP, { Cypher: 'MATCH (n:KgNode {kgId: $kgId}) RETURN n.name AS name' })
      const d = safeParse(r.json?.dataJson ?? '')
      const rowCount = Number(pick(d, 'RowCount') ?? -1)
      const truncated = pick(d, 'Truncated') === true
      check('KT-S7b RowCount==2 且 Truncated==true', r.status === 200 && r.json?.success === true && rowCount === 2 && truncated, `${r.status} ${String(r.json?.dataJson ?? r.json?.error ?? '').slice(0, 240)}`)
      await api('DELETE', `/api/team/${T1}/plugin/${await findPluginId(token, T1, KEYCAP)}`, { token }).catch(() => {})
    }
  }

  // ===== KT-S8 超时场景（依赖慢查询负载，人工验证）=====
  console.warn('SKIP | KT-S8 超时场景依赖慢查询，需注入负载后手动验证')

  // ===== KT-S9 接入图查询无需 $kgId =====
  if (CONN > 0) {
    const KEYCONN = `kgcy_conn_${TS}`
    const createConn = await saveInstance(token, T1, KEYCONN, CONN, 100)
    if (createConn.status !== 200) {
      console.warn(`SKIP | KT-S9 接入图实例创建失败 (${createConn.status}): ${createConn.text.slice(0, 160)}`)
    } else {
      const r = await runInstance(token, T1, KEYCONN, { Cypher: 'MATCH (n) RETURN count(n) AS c LIMIT 1' })
      const d = safeParse(r.json?.dataJson ?? '')
      check('KT-S9 接入图查询无需 $kgId 成功', r.status === 200 && r.json?.success === true && Number(pick(d, 'RowCount') ?? 0) === 1, `${r.status} ${String(r.json?.dataJson ?? r.json?.error ?? '').slice(0, 240)}`)
      await api('DELETE', `/api/team/${T1}/plugin/${await findPluginId(token, T1, KEYCONN)}`, { token }).catch(() => {})
    }
  } else {
    console.warn('SKIP | KT-S9 图数据库不可达（接入图未建），跳过')
  }

  // ===== KT-S10 跨团队运行实例 404 或 success=false =====
  {
    const r = await runInstance(token, T2, KEY1, { Schema: true })
    check('KT-S10 跨团队运行实例 404 或失败', r.status === 404 || r.json?.success === false, `${r.status} ${r.text.slice(0, 160)}`)
  }

  // ===== 清理：删插件实例（尽力）→ 删图 → 禁用团队 =====
  for (const key of [KEY1]) {
    try {
      const pid = await findPluginId(token, T1, key)
      if (pid) await api('DELETE', `/api/team/${T1}/plugin/${pid}`, { token })
    } catch { /* 清理尽力而为 */ }
  }
  await api('DELETE', kg(GRAPH_ID), { token })
  if (CONN > 0) await api('DELETE', kg(CONN), { token })
  // 团队不可解散：清理改为管理员禁用归档
  await api('PUT', `/api/admin/team/${T1}/disable`, { token, body: { isDisable: true } })
  await api('PUT', `/api/admin/team/${T2}/disable`, { token, body: { isDisable: true } })

  console.log(`\n===== 知识图谱 Text2Cypher E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

// 按实例 key 找团队插件 id（清理用；找不到或查询失败返回空串，绝不让清理环节中断脚本）
async function findPluginId(token, teamId, pluginName) {
  try {
    const r = await api('GET', `/api/team/${teamId}/plugin/list`, { token })
    const item = (r.json?.items ?? []).find((x) => x.pluginName === pluginName)
    return item?.pluginId ?? ''
  } catch {
    return ''
  }
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
