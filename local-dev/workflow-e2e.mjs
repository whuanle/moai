// 流程应用（MoAI.App.Workflow）E2E（场景 @WF-Sn）
// 用法：node local-dev/workflow-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中（含 workflow 模块），且已执行 asserts/app_workflow.sql 建表（新库由 EnsureCreated 自动建）。
// 覆盖：草稿保存/查询、发布校验与版本快照、调试执行（条件分支/跳过传播/JS 沙箱/插值）、
//       启动参数校验、运行历史与详情、角色门禁。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

// ==================== 测试流程定义（引擎确定性行为，不依赖模型/外部插件） ====================
// start(query) → compute(JS: hasResult=query==='yes') → check(条件)
//   → [true] hit(JS: answer='命中:{compute.summary}') → end
//   → [false] miss(JS: answer='未命中') → end
const DEF_NODES = [
  { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'query', fieldType: 'string', isRequired: true, description: '标志位' }] },
  {
    key: 'compute', name: '计算', type: 'javaScript',
    config: { code: 'function run(inputs, sys, nodes) {\n  return { hasResult: inputs.flag === "yes", summary: "sum-" + inputs.flag }\n}' },
    inputs: { flag: { expressionType: 'variable', value: 'start.query', required: true } },
    outputs: [{ name: 'hasResult', fieldType: 'boolean' }, { name: 'summary', fieldType: 'string' }],
  },
  { key: 'check', name: '是否有结果', type: 'condition', inputs: { condition: { expressionType: 'variable', value: 'compute.hasResult', required: true } }, outputs: [] },
  {
    key: 'hit', name: '命中分支', type: 'javaScript',
    config: { code: 'function run(inputs) { return { answer: inputs.s } }' },
    inputs: { s: { expressionType: 'interpolation', value: '命中:{compute.summary}', required: true } },
    outputs: [{ name: 'answer', fieldType: 'string' }],
  },
  {
    key: 'miss', name: '未命中分支', type: 'javaScript',
    config: { code: 'function run(inputs) { return { answer: inputs.s } }' },
    inputs: { s: { expressionType: 'fixed', value: '未命中', required: true } },
    outputs: [{ name: 'answer', fieldType: 'string' }],
  },
  {
    key: 'end', name: '结束', type: 'end',
    inputs: {
      answer: { expressionType: 'variable', value: 'hit.answer', required: false },
      missAnswer: { expressionType: 'variable', value: 'miss.answer', required: false },
    },
    outputs: [{ name: 'answer', fieldType: 'string' }, { name: 'missAnswer', fieldType: 'string' }],
  },
]
const DEF_CONNECTIONS = [
  { id: 'c1', source: 'start', target: 'compute' },
  { id: 'c2', source: 'compute', target: 'check' },
  { id: 'c3', source: 'check', target: 'hit', condition: 'true', label: '满足' },
  { id: 'c4', source: 'check', target: 'miss', condition: 'false', label: '不满足' },
  { id: 'c5', source: 'hit', target: 'end' },
  { id: 'c6', source: 'miss', target: 'end' },
]
const POS = { start: { x: 80, y: 200 }, compute: { x: 280, y: 200 }, check: { x: 480, y: 200 }, hit: { x: 680, y: 120 }, miss: { x: 680, y: 300 }, end: { x: 880, y: 200 } }

const buildDefinition = () => ({ id: '', name: 'wf-e2e', version: 0, status: 'draft', nodes: DEF_NODES, connections: DEF_CONNECTIONS, ui: { nodePositions: POS } })
const buildEditorData = () => ({
  nodes: DEF_NODES.map((n) => ({
    id: n.key, type: n.type,
    meta: { position: POS[n.key] ?? { x: 100, y: 100 }, defaultExpanded: true },
    data: { title: n.name, content: '', inputs: n.inputs, outputs: n.outputs, settings: n.config ?? {} },
    blocks: [],
    edges: DEF_CONNECTIONS.filter((c) => c.source === n.key).map((c) => ({
      sourceNodeID: c.source, targetNodeID: c.target,
      ...(c.condition ? { sourcePortID: c.condition } : {}),
    })),
  })),
  edges: [],
})

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('wo')
  const member = await mkuser('wm')
  const outsider = await mkuser('wx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'wf-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })

  // 创建流程应用与 Agent 应用（对照）
  const w = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '流程应用' + TS, appType: 'workflow' } })
  check('WF-01 流程应用创建 200', w.status === 200 && isGuid(w.json?.value), `${w.status} ${w.text.slice(0, 120)}`)
  const APP = String(w.json?.value ?? '')
  const agent = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '对照应用' + TS, appType: 'agent' } })
  const AGENT = String(agent.json?.value ?? '')

  // WF-02 未登录
  check('WF-02 未登录查配置 401', (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`)).status === 401)

  // WF-03 角色门禁
  check('WF-03a Member 保存草稿 403', (await api('POST', `/api/app/workflow/draft`, { token: member.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildDefinition()), editorData: JSON.stringify(buildEditorData()) } })).status === 403)
  check('WF-03b 非成员查配置 404', (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: outsider.token })).status === 404)
  const memberView = await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: member.token })
  check('WF-03c Member 查配置 200', memberView.status === 200)

  // WF-04 请求校验
  check('WF-04a 空 definition 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: '', editorData: '{}' } })).status === 400)
  check('WF-04b 非法 teamId 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: 0, definition: '{}', editorData: '{}' } })).status === 400)

  // WF-05 非 workflow 应用 400
  check('WF-05 Agent 应用调 workflow 草稿 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: AGENT, teamId: TID, definition: JSON.stringify(buildDefinition()), editorData: JSON.stringify(buildEditorData()) } })).status === 400)

  // WF-06 保存草稿 + 查询
  const save = await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildDefinition()), editorData: JSON.stringify(buildEditorData()) } })
  check('WF-06a 保存草稿 200', save.status === 200, `${save.status} ${save.text.slice(0, 160)}`)
  const cfg = await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: owner.token })
  const cfgBody = cfg.json ?? {}
  check('WF-06b 查配置含草稿', cfg.status === 200 && isGuid(cfgBody.configId) && typeof cfgBody.draftDefinition === 'string' && cfgBody.draftDefinition.includes('"compute"'), JSON.stringify(cfgBody).slice(0, 160))
  check('WF-06c 编辑器 JSON 保存', typeof cfgBody.draftEditorData === 'string' && cfgBody.draftEditorData.includes('"start"'))
  check('WF-06d 初始状态草稿', cfgBody.version === 0 && cfgBody.status === 0)

  // WF-07 非法定义发布 400（条件节点缺 false 出边）
  const broken = buildDefinition()
  broken.connections = broken.connections.filter((c) => !(c.source === 'check' && c.condition === 'false'))
  // 用非法草稿覆盖后发布，必须被拒绝且版本号不增长
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(broken), editorData: JSON.stringify(buildEditorData()) } })
  const badPublish2 = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-07 非法定义发布 400', badPublish2.status === 400 && badPublish2.text.includes('false'), `${badPublish2.status} ${badPublish2.text.slice(0, 160)}`)

  // WF-08 恢复合法草稿并发布
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildDefinition()), editorData: JSON.stringify(buildEditorData()) } })
  const publish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-08a 发布 200', publish.status === 200, `${publish.status} ${publish.text.slice(0, 160)}`)
  const cfg2 = (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: owner.token })).json ?? {}
  check('WF-08b 发布后 version=1 status=1', cfg2.version === 1 && cfg2.status === 1 && typeof cfg2.publishedDefinition === 'string' && cfg2.publishedDefinition.includes('"compute"'), JSON.stringify({ v: cfg2.version, s: cfg2.status }).slice(0, 120))
  const appDetail = await api('GET', `/api/app/${APP}`, { token: owner.token })
  check('WF-08c 应用置为已发布', (appDetail.json?.publishStatus ?? appDetail.json?.publish_status ?? -1) === 1 || appDetail.text.includes('"publishStatus":1'), appDetail.text.slice(0, 120))

  // WF-09 调试执行：true 分支
  const runTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'yes' }) } })
  const rt = runTrue.json ?? {}
  const nodeState = (key) => (rt.nodes ?? []).find((n) => n.nodeKey === key)?.state
  check('WF-09a true 分支执行完成', runTrue.status === 200 && rt.status === 'completed', `${runTrue.status} ${runTrue.text.slice(0, 200)}`)
  check('WF-09b 命中插值输出', rt.output && JSON.parse(rt.output).answer === '命中:sum-yes', rt.output ?? '')
  check('WF-09c 未命中分支被跳过', nodeState('miss') === 'skipped', JSON.stringify(rt.nodes)?.slice(0, 200))
  check('WF-09d 上游节点完成', nodeState('compute') === 'completed' && nodeState('check') === 'completed' && nodeState('end') === 'completed')

  // WF-10 调试执行：false 分支
  const runFalse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'no' }) } })
  const rf = runFalse.json ?? {}
  check('WF-10 false 分支输出', runFalse.status === 200 && rf.status === 'completed' && rf.output && JSON.parse(rf.output).missAnswer === '未命中' && JSON.parse(rf.output).answer === null, `${runFalse.status} ${rf.output ?? ''}`)

  // WF-11 缺必需启动参数 → 挂起
  const runMissing = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: '{}' } })
  const rm = runMissing.json ?? {}
  const startNode = (rm.nodes ?? []).find((n) => n.nodeKey === 'start')
  check('WF-11 缺参数挂起且 start 失败', runMissing.status === 200 && rm.status === 'suspended' && startNode?.state === 'failed' && (startNode.errorMessage ?? '').includes('query'), `${runMissing.status} ${runMissing.text.slice(0, 200)}`)

  // WF-12 非法启动参数 JSON 400
  check('WF-12 非法输入 JSON 400', (await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: '{bad' } })).status === 400)

  // WF-13 运行历史与详情
  const list = await api('GET', `/api/app/workflow/instances?appId=${APP}&teamId=${TID}&pageNo=1&pageSize=10`, { token: owner.token })
  check('WF-13a 实例列表 3 条', list.status === 200 && (list.json?.total ?? 0) >= 3, `${list.status} ${list.text.slice(0, 160)}`)
  const firstId = list.json?.items?.[0]?.instanceId
  check('WF-13b 列表项含触发人', typeof list.json?.items?.[0]?.createUserName === 'string' && list.json.items[0].createUserName.length > 0)
  const detail = await api('GET', `/api/app/workflow/instance?appId=${APP}&teamId=${TID}&instanceId=${firstId}`, { token: owner.token })
  check('WF-13c 详情含节点状态', detail.status === 200 && (detail.json?.nodes ?? []).length === DEF_NODES.length && (detail.json?.nodes ?? []).every((n) => typeof n.nodeKey === 'string'), `${detail.status} ${detail.text.slice(0, 160)}`)
  check('WF-13d Member 查历史 403', (await api('GET', `/api/app/workflow/instances?appId=${APP}&teamId=${TID}`, { token: member.token })).status === 403)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
