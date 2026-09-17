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
    config: { code: 'function run(inputs, sys, nodes, system) {\n  return { hasResult: inputs.flag === "yes", summary: "sum-" + inputs.flag, env: system.env }\n}' },
    inputs: { flag: { expressionType: 'variable', value: 'start.query', required: true } },
    outputs: [{ name: 'hasResult', fieldType: 'boolean' }, { name: 'summary', fieldType: 'string' }, { name: 'env', fieldType: 'string' }],
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
const DEF_VARIABLES = [
  { name: 'env', fieldType: 'string', defaultValue: '"dev"', description: '运行环境' },
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

const buildDefinition = () => ({ id: '', name: 'wf-e2e', version: 0, status: 'draft', nodes: DEF_NODES, connections: DEF_CONNECTIONS, variables: DEF_VARIABLES, ui: { nodePositions: POS } })
/** 条件节点脚本模式变体：check 用 config.conditionScript 路由（env=dev 或 hasResult 走真分支） */
const buildScriptConditionDefinition = () => {
  const def = buildDefinition()
  const check = def.nodes.find((n) => n.key === 'check')
  check.config = { conditionScript: "function condition(inputs, sys, nodes, system) {\n  return nodes.compute.hasResult === true || system.env === 'dev';\n}" }
  delete check.inputs.condition
  return def
}
const buildEditorData = (def = buildDefinition()) => ({
  nodes: def.nodes.map((n) => ({
    id: n.key, type: n.type,
    meta: { position: POS[n.key] ?? { x: 100, y: 100 }, defaultExpanded: true },
    data: {
      title: n.name, content: '', inputs: n.inputs, outputs: n.outputs,
      ...(n.type === 'switch' ? { branches: (n.config?.branches ?? []) } : { settings: n.config ?? {} }),
    },
    blocks: [],
    edges: def.connections.filter((c) => c.source === n.key).map((c) => ({
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

  // WF-14 全局变量：默认值生效 + 启动传入覆盖（compute JS 经第 4 参 system 读取）
  const runDefault = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'yes' }) } })
  const rd = runDefault.json ?? {}
  const computeOut = (rd.nodes ?? []).find((n) => n.nodeKey === 'compute')?.output
  check('WF-14a 全局变量默认值 dev', runDefault.status === 200 && computeOut && JSON.parse(computeOut).env === 'dev', computeOut ?? '')

  const runOverride = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'yes' }), systemJson: JSON.stringify({ env: 'prod' }) } })
  const ro = runOverride.json ?? {}
  const computeOut2 = (ro.nodes ?? []).find((n) => n.nodeKey === 'compute')?.output
  check('WF-14b 全局变量覆盖 prod', runOverride.status === 200 && computeOut2 && JSON.parse(computeOut2).env === 'prod', computeOut2 ?? '')

  // WF-15 系统设置·对话开场白：流程应用经 agent-config 只写开场白字段（模型/知识库/插件/技能不适用不落库）
  const osSave = await api('PUT', `/api/app/${APP}/agent-config`, { token: owner.token, body: { modelId: null, prompt: '', wikiIds: [], plugins: [], openingStatement: '你好，我是流程助手', openingStatementEnabled: true } })
  check('WF-15a 流程应用保存开场白 200', osSave.status === 200, `${osSave.status} ${osSave.text.slice(0, 160)}`)
  const osGet = await api('GET', `/api/app/${APP}/agent-config`, { token: owner.token })
  check('WF-15b 开场白回读', osGet.status === 200 && osGet.json?.openingStatement === '你好，我是流程助手' && osGet.json?.openingStatementEnabled === true, osGet.text.slice(0, 200))
  check('WF-15c Member 保存开场白 403', (await api('PUT', `/api/app/${APP}/agent-config`, { token: member.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: 'x', openingStatementEnabled: true } })).status === 403)
  const cfg3 = (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: owner.token })).json ?? {}
  check('WF-15d 开场白保存不影响流程草稿', cfg3.version === 1 && cfg3.status === 1 && typeof cfg3.draftDefinition === 'string' && cfg3.draftDefinition.includes('"compute"'), JSON.stringify({ v: cfg3.version, s: cfg3.status }).slice(0, 80))
  const detailAfter = await api('GET', `/api/app/${APP}`, { token: owner.token })
  check('WF-15e 应用详情下发开场白', detailAfter.status === 200 && detailAfter.json?.openingStatement === '你好，我是流程助手' && detailAfter.json?.openingStatementEnabled === true, detailAfter.text.slice(0, 160))

  // WF-16 条件脚本模式：check 节点 config.conditionScript（Jint），按脚本返回值路由分支
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildScriptConditionDefinition()), editorData: JSON.stringify(buildEditorData(buildScriptConditionDefinition())) } })
  const runScriptTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'no' }) } })
  const rst = runScriptTrue.json ?? {}
  check('WF-16a 脚本条件 env=dev 走真分支', runScriptTrue.status === 200 && rst.status === 'completed' && rst.output && JSON.parse(rst.output).answer === '命中:sum-no', `${runScriptTrue.status} ${rst.output ?? runScriptTrue.text.slice(0, 160)}`)
  const runScriptFalse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'no' }), systemJson: JSON.stringify({ env: 'prod' }) } })
  const rsf = runScriptFalse.json ?? {}
  check('WF-16b 脚本条件 env=prod 走假分支', runScriptFalse.status === 200 && rsf.status === 'completed' && rsf.output && JSON.parse(rsf.output).missAnswer === '未命中', `${runScriptFalse.status} ${rsf.output ?? runScriptFalse.text.slice(0, 160)}`)

  // WF-17 多条件节点（switch/if-else）：b1=compute.hasResult，b2 固定 false，else 兜底
  const buildSwitchDefinition = () => {
    const def = buildDefinition()
    def.nodes = def.nodes.filter((n) => n.key !== 'check')
    def.nodes.splice(2, 0, {
      key: 'sw', name: '多条件', type: 'switch',
      config: { branches: [
        { id: 'b1', label: '有结果', binding: { expressionType: 'variable', value: 'compute.hasResult' } },
        { id: 'b2', label: '永不命中', binding: { expressionType: 'fixed', value: 'false' } },
      ] },
      inputs: {}, outputs: [],
    })
    def.connections = def.connections.filter((c) => !['c2', 'c3', 'c4'].includes(c.id))
    def.connections.push(
      { id: 'c2', source: 'compute', target: 'sw' },
      { id: 'c3', source: 'sw', target: 'hit', condition: 'b1' },
      { id: 'c4', source: 'sw', target: 'miss', condition: 'else' },
    )
    return def
  }
  const switchDef = buildSwitchDefinition()
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(switchDef), editorData: JSON.stringify(buildEditorData(switchDef)) } })
  const runSwitchTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'yes' }) } })
  const rswt = runSwitchTrue.json ?? {}
  check('WF-17a 多条件 b1 命中走 hit', runSwitchTrue.status === 200 && rswt.status === 'completed' && rswt.output && JSON.parse(rswt.output).answer === '命中:sum-yes', `${runSwitchTrue.status} ${rswt.output ?? runSwitchTrue.text.slice(0, 160)}`)
  const runSwitchElse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ query: 'no' }) } })
  const rwse = runSwitchElse.json ?? {}
  check('WF-17b 多条件未命中走 else', runSwitchElse.status === 200 && rwse.status === 'completed' && rwse.output && JSON.parse(rwse.output).missAnswer === '未命中', `${runSwitchElse.status} ${rwse.output ?? runSwitchElse.text.slice(0, 160)}`)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
