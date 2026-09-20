// 流程应用（MoAI.App.Workflow）E2E（场景 @WF-Sn）
// 用法：node local-dev/workflow-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中（含 workflow 模块），且已执行 asserts/app_workflow.sql 建表（新库由 EnsureCreated 自动建）。
// 覆盖：草稿保存/查询、发布校验与版本快照、调试执行（条件分支/跳过传播/JS 沙箱/插值）、
//       知识库检索节点（wikiIds 归属校验/空命中结构/未配置失败）、问题分类节点校验闭环、
//       HTTP 请求节点（GET 参数与字段提取/POST JSON 请求体/非 2xx 默认中断/报错捕获/超时/配置校验）、
//       启动参数校验、运行历史与详情、角色门禁、核心节点不变量（无开始/双开始/无结束草稿 400）。
//       开始节点固定 question 契约（对话注入 + 对话/调试 query 镜像兼容旧编排）、对话历史 MAF 压缩有界、
//       调试 Tab 按最新草稿执行（免发布，仅管理员，X-Moai-Workflow-Draft 头）、
//       Agent 应用节点（agentApp）调用已发布 Agent + 流程↔Agent 循环嵌套防护。
import crypto from 'node:crypto'
import http from 'node:http'

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
// start(question) → compute(JS: hasResult=query==='yes') → check(条件)
//   → [true] hit(JS: answer='命中:{compute.summary}') → end
//   → [false] miss(JS: answer='未命中') → end
const DEF_NODES = [
  { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
  {
    key: 'compute', name: '计算', type: 'javaScript',
    config: { code: 'function run(inputs, sys, nodes, system) {\n  return { hasResult: inputs.flag === "yes", summary: "sum-" + inputs.flag, env: system.env }\n}' },
    inputs: { flag: { expressionType: 'variable', value: 'start.question', required: true } },
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
      ...(n.type === 'switch'
        ? { branches: (n.config?.branches ?? []) }
        : n.type === 'questionClassifier'
          ? { classes: (n.config?.classes ?? []), settings: { ...n.config, classes: undefined } }
          : { settings: n.config ?? {} }),
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

  // WF-04 请求校验 + 核心节点不变量（开始恰好一个/结束至少一个，违反即 400）
  check('WF-04a 空 definition 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: '', editorData: '{}' } })).status === 400)
  check('WF-04b 非法 teamId 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: 0, definition: '{}', editorData: '{}' } })).status === 400)
  check('WF-04c 无开始节点 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify({ ...buildDefinition(), nodes: DEF_NODES.filter((n) => n.type !== 'start') }), editorData: '{}' } })).status === 400)
  check('WF-04d 无结束节点 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify({ ...buildDefinition(), nodes: DEF_NODES.filter((n) => n.type !== 'end') }), editorData: '{}' } })).status === 400)
  check('WF-04e 双开始节点 400', (await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify({ ...buildDefinition(), nodes: [...DEF_NODES, DEF_NODES[0]] }), editorData: '{}' } })).status === 400)

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
  const runTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'yes' }) } })
  const rt = runTrue.json ?? {}
  const nodeState = (key) => (rt.nodes ?? []).find((n) => n.nodeKey === key)?.state
  check('WF-09a true 分支执行完成', runTrue.status === 200 && rt.status === 'completed', `${runTrue.status} ${runTrue.text.slice(0, 200)}`)
  check('WF-09b 命中插值输出', rt.output && JSON.parse(rt.output).answer === '命中:sum-yes', rt.output ?? '')
  check('WF-09c 未命中分支被跳过', nodeState('miss') === 'skipped', JSON.stringify(rt.nodes)?.slice(0, 200))
  check('WF-09d 上游节点完成', nodeState('compute') === 'completed' && nodeState('check') === 'completed' && nodeState('end') === 'completed')

  // WF-10 调试执行：false 分支
  const runFalse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'no' }) } })
  const rf = runFalse.json ?? {}
  check('WF-10 false 分支输出', runFalse.status === 200 && rf.status === 'completed' && rf.output && JSON.parse(rf.output).missAnswer === '未命中' && JSON.parse(rf.output).answer === null, `${runFalse.status} ${rf.output ?? ''}`)

  // WF-11 缺必需启动参数 → 挂起
  const runMissing = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: '{}' } })
  const rm = runMissing.json ?? {}
  const startNode = (rm.nodes ?? []).find((n) => n.nodeKey === 'start')
  check('WF-11 缺参数挂起且 start 失败', runMissing.status === 200 && rm.status === 'suspended' && startNode?.state === 'failed' && (startNode.errorMessage ?? '').includes('question'), `${runMissing.status} ${runMissing.text.slice(0, 200)}`)

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
  const runDefault = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'yes' }) } })
  const rd = runDefault.json ?? {}
  const computeOut = (rd.nodes ?? []).find((n) => n.nodeKey === 'compute')?.output
  check('WF-14a 全局变量默认值 dev', runDefault.status === 200 && computeOut && JSON.parse(computeOut).env === 'dev', computeOut ?? '')

  const runOverride = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'yes' }), systemJson: JSON.stringify({ env: 'prod' }) } })
  const ro = runOverride.json ?? {}
  const computeOut2 = (ro.nodes ?? []).find((n) => n.nodeKey === 'compute')?.output
  check('WF-14b 全局变量覆盖 prod', runOverride.status === 200 && computeOut2 && JSON.parse(computeOut2).env === 'prod', computeOut2 ?? '')

  // WF-15 系统设置·对话开场白（草稿/发布双轨）：流程应用经 agent-config 只写开场白字段（模型/知识库/插件/技能不适用不落库）；
  // 发布后改开场白只落草稿，线上详情仍按发布快照下发，重新发布后生效
  const osSave = await api('PUT', `/api/app/${APP}/agent-config`, { token: owner.token, body: { modelId: null, prompt: '', wikiIds: [], plugins: [], openingStatement: '你好，我是流程助手', openingStatementEnabled: true } })
  check('WF-15a 流程应用保存开场白 200', osSave.status === 200, `${osSave.status} ${osSave.text.slice(0, 160)}`)
  const osGet = await api('GET', `/api/app/${APP}/agent-config`, { token: owner.token })
  check('WF-15b 开场白回读（草稿值）', osGet.status === 200 && osGet.json?.openingStatement === '你好，我是流程助手' && osGet.json?.openingStatementEnabled === true, osGet.text.slice(0, 200))
  check('WF-15c Member 保存开场白 403', (await api('PUT', `/api/app/${APP}/agent-config`, { token: member.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: 'x', openingStatementEnabled: true } })).status === 403)
  const cfg3 = (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: owner.token })).json ?? {}
  check('WF-15d 开场白草稿不触碰编排定义但状态转未发布变更', cfg3.version === 1 && cfg3.status === 0 && typeof cfg3.draftDefinition === 'string' && cfg3.draftDefinition.includes('"compute"'), JSON.stringify({ v: cfg3.version, s: cfg3.status }).slice(0, 80))
  const detailAfter = await api('GET', `/api/app/${APP}`, { token: owner.token })
  check('WF-15e 详情仍下发已发布开场白（发布时未配置，故为空）', detailAfter.status === 200 && detailAfter.json?.openingStatement === '' && detailAfter.json?.openingStatementEnabled === false, detailAfter.text.slice(0, 160))
  // 重新发布：开场白随编排定义一起进入发布快照
  const osPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  const cfg3b = (await api('GET', `/api/app/workflow/config?appId=${APP}&teamId=${TID}`, { token: owner.token })).json ?? {}
  const detailRepublished = await api('GET', `/api/app/${APP}`, { token: owner.token })
  check('WF-15f 重新发布后开场白生效且状态回 1', osPublish.status === 200 && cfg3b.version === 2 && cfg3b.status === 1
    && detailRepublished.json?.openingStatement === '你好，我是流程助手' && detailRepublished.json?.openingStatementEnabled === true,
    `${osPublish.status} ${JSON.stringify({ v: cfg3b.version, s: cfg3b.status })} ${detailRepublished.text.slice(0, 120)}`)

  // WF-16 条件脚本模式：check 节点 config.conditionScript（Jint），按脚本返回值路由分支
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildScriptConditionDefinition()), editorData: JSON.stringify(buildEditorData(buildScriptConditionDefinition())) } })
  const runScriptTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'no' }) } })
  const rst = runScriptTrue.json ?? {}
  check('WF-16a 脚本条件 env=dev 走真分支', runScriptTrue.status === 200 && rst.status === 'completed' && rst.output && JSON.parse(rst.output).answer === '命中:sum-no', `${runScriptTrue.status} ${rst.output ?? runScriptTrue.text.slice(0, 160)}`)
  const runScriptFalse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'no' }), systemJson: JSON.stringify({ env: 'prod' }) } })
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
  const runSwitchTrue = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'yes' }) } })
  const rswt = runSwitchTrue.json ?? {}
  check('WF-17a 多条件 b1 命中走 hit', runSwitchTrue.status === 200 && rswt.status === 'completed' && rswt.output && JSON.parse(rswt.output).answer === '命中:sum-yes', `${runSwitchTrue.status} ${rswt.output ?? runSwitchTrue.text.slice(0, 160)}`)
  const runSwitchElse = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'no' }) } })
  const rwse = runSwitchElse.json ?? {}
  check('WF-17b 多条件未命中走 else', runSwitchElse.status === 200 && rwse.status === 'completed' && rwse.output && JSON.parse(rwse.output).missAnswer === '未命中', `${runSwitchElse.status} ${rwse.output ?? runSwitchElse.text.slice(0, 160)}`)

  // ==================== WF-18 知识库检索节点（knowledgeSearch） ====================
  // owner 团队创建知识库（不配置 embedding → 检索时静默跳过、返回空命中）；outsider 建自己的团队与知识库用于越权校验
  const kw = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'wf-wiki-' + TS, description: 'e2e', isPublic: false } })
  const WIKI = Number(kw.json?.value ?? 0)
  check('WF-18a 创建团队知识库 200', kw.status === 200 && WIKI > 0, `${kw.status} ${kw.text.slice(0, 120)}`)
  const oTeamR = await api('POST', '/api/team', { token: outsider.token, body: { name: 'wf-oteam-' + TS } })
  const oTeam = Number(oTeamR.json?.value ?? 0)
  const oWikiR = await api('POST', '/api/wiki', { token: outsider.token, body: { teamId: oTeam, name: 'wf-owiki-' + TS, isPublic: false } })
  const OWIKI = Number(oWikiR.json?.value ?? 0)

  const buildKsDefinition = (wikiIds) => ({
    id: '', name: 'wf-ks', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '检索问题' }] },
      {
        key: 'ks', name: '知识库检索', type: 'knowledgeSearch',
        config: { wikiIds, topK: 3 },
        inputs: { query: { expressionType: 'variable', value: 'start.question', required: true } },
        outputs: [
          { name: 'query', fieldType: 'string' },
          { name: 'count', fieldType: 'number' },
          { name: 'hits', fieldType: 'array' },
          { name: 'contents', fieldType: 'array' },
          { name: 'text', fieldType: 'string' },
        ],
      },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: {
          count: { expressionType: 'variable', value: 'ks.count', required: false },
          text: { expressionType: 'variable', value: 'ks.text', required: false },
          hits: { expressionType: 'variable', value: 'ks.hits', required: false },
        },
        outputs: [{ name: 'count', fieldType: 'number' }, { name: 'text', fieldType: 'string' }, { name: 'hits', fieldType: 'array' }],
      },
    ],
    connections: [
      { id: 'k1', source: 'start', target: 'ks' },
      { id: 'k2', source: 'ks', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, ks: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })

  // WF-18b 越权：引用他人团队知识库保存草稿 400
  const badSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsDefinition([WIKI, OWIKI])), editorData: '{}' } })
  check('WF-18b 引用他团队知识库保存 400', badSave.status === 400 && badSave.text.includes(String(OWIKI)), `${badSave.status} ${badSave.text.slice(0, 160)}`)

  // WF-18c 引用不存在知识库保存 400
  const ghostSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsDefinition([WIKI, 99999999])), editorData: '{}' } })
  check('WF-18c 引用不存在知识库保存 400', ghostSave.status === 400, `${ghostSave.status} ${ghostSave.text.slice(0, 160)}`)

  // WF-18d 合法配置保存 + 发布
  const ksSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsDefinition([WIKI])), editorData: JSON.stringify(buildEditorData(buildKsDefinition([WIKI]))) } })
  check('WF-18d 合法知识库配置保存 200', ksSave.status === 200, `${ksSave.status} ${ksSave.text.slice(0, 160)}`)
  const ksPublish = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-18e 含检索节点发布 200', ksPublish.status === 200, `${ksPublish.status} ${ksPublish.text.slice(0, 160)}`)

  // WF-18f 调试执行：未配 embedding → 空命中但结构完整（query/count/hits/contents/text）
  const ksRun = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: '检索一下' }) } })
  const ksr = ksRun.json ?? {}
  const ksOut = ksr.nodes?.find((n) => n.nodeKey === 'ks')?.output
  const ksParsed = ksOut ? JSON.parse(ksOut) : null
  check('WF-18f 检索节点执行完成', ksRun.status === 200 && ksr.status === 'completed' && (ksr.nodes ?? []).find((n) => n.nodeKey === 'ks')?.state === 'completed', `${ksRun.status} ${ksRun.text.slice(0, 200)}`)
  check('WF-18g 输出结构完整且空命中', ksParsed !== null && ksParsed.query === '检索一下' && ksParsed.count === 0 && Array.isArray(ksParsed.hits) && Array.isArray(ksParsed.contents) && ksParsed.text === '', ksOut ?? '')
  const ksEnd = ksr.nodes?.find((n) => n.nodeKey === 'end')?.output
  check('WF-18h 下游引用检索输出', ksEnd && JSON.parse(ksEnd).count === 0 && JSON.parse(ksEnd).text === '', ksEnd ?? '')

  // WF-18i 未配置知识库（空 wikiIds）→ 节点失败并提示
  const noWikiDef = buildKsDefinition([])
  noWikiDef.nodes.find((n) => n.key === 'ks').config = { wikiIds: [] }
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(noWikiDef), editorData: '{}' } })
  const noWikiRun = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x' }) } })
  const nwr = noWikiRun.json ?? {}
  const ksState = (nwr.nodes ?? []).find((n) => n.nodeKey === 'ks')
  check('WF-18i 未配置知识库节点失败', noWikiRun.status === 200 && nwr.status === 'suspended' && ksState?.state === 'failed' && (ksState.errorMessage ?? '').includes('wikiId'), `${noWikiRun.status} ${noWikiRun.text.slice(0, 200)}`)

  // 恢复合法草稿，保持与已发布快照一致
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsDefinition([WIKI])), editorData: '{}' } })

  // ==================== WF-19 知识库检索节点·变量动态绑定 ====================
  // 节点不配置静态知识库（config.wikiIds 缺省），wikiIds 输入绑定 start.wikiId 变量，运行时动态解析
  const buildKsDynDefinition = () => ({
    id: '', name: 'wf-ks-dyn', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [
        { name: 'question', fieldType: 'string', isRequired: true, description: '检索问题' },
        { name: 'wikiId', fieldType: 'dynamic', isRequired: false, description: '知识库 id（动态）' },
      ] },
      {
        key: 'ks', name: '知识库检索', type: 'knowledgeSearch',
        config: { topK: 3 },
        inputs: {
          query: { expressionType: 'variable', value: 'start.question', required: true },
          wikiId: { expressionType: 'variable', value: 'start.wikiId', required: false },
        },
        outputs: [
          { name: 'query', fieldType: 'string' },
          { name: 'count', fieldType: 'number' },
          { name: 'hits', fieldType: 'array' },
          { name: 'contents', fieldType: 'array' },
          { name: 'text', fieldType: 'string' },
        ],
      },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: {
          count: { expressionType: 'variable', value: 'ks.count', required: false },
          text: { expressionType: 'variable', value: 'ks.text', required: false },
        },
        outputs: [{ name: 'count', fieldType: 'number' }, { name: 'text', fieldType: 'string' }],
      },
    ],
    connections: [
      { id: 'd1', source: 'start', target: 'ks' },
      { id: 'd2', source: 'ks', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, ks: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })

  // WF-19a 动态绑定定义（无静态知识库）保存 200
  const dynSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsDynDefinition()), editorData: '{}' } })
  check('WF-19a 动态绑定定义保存 200', dynSave.status === 200, `${dynSave.status} ${dynSave.text.slice(0, 160)}`)

  // WF-19b 运行时动态解析：start.wikiId = 本团队知识库 → 节点完成
  const dynRun = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: '动态检索', wikiId: WIKI }) } })
  const dynr = dynRun.json ?? {}
  const dynKs = (dynr.nodes ?? []).find((n) => n.nodeKey === 'ks')
  check('WF-19b 变量绑定运行时解析', dynRun.status === 200 && dynr.status === 'completed' && dynKs?.state === 'completed', `${dynRun.status} ${dynRun.text.slice(0, 200)}`)
  const dynParsed = dynKs?.output ? JSON.parse(dynKs.output) : null
  check('WF-19c 动态输出结构完整', dynParsed !== null && dynParsed.query === '动态检索' && dynParsed.count === 0 && Array.isArray(dynParsed.hits) && dynParsed.text === '', dynKs?.output ?? '')

  // WF-19d 动态传入他团队知识库 id → 运行时团队过滤兜底，静默忽略
  const dynRunForeign = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x', wikiId: OWIKI }) } })
  const dynrf = dynRunForeign.json ?? {}
  const foreignKs = (dynrf.nodes ?? []).find((n) => n.nodeKey === 'ks')
  check('WF-19d 动态传入他团队知识库被过滤', dynRunForeign.status === 200 && dynrf.status === 'completed' && foreignKs?.state === 'completed', `${dynRunForeign.status} ${dynRunForeign.text.slice(0, 160)}`)

  // WF-19e 未传 wikiId 且无静态配置 → 节点失败并提示 wikiIds
  const dynRunNone = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x' }) } })
  const dynrn = dynRunNone.json ?? {}
  const noneKs = (dynrn.nodes ?? []).find((n) => n.nodeKey === 'ks')
  check('WF-19e 未绑定且未配置时节点失败', dynRunNone.status === 200 && dynrn.status === 'suspended' && noneKs?.state === 'failed' && (noneKs.errorMessage ?? '').includes('wikiId'), `${dynRunNone.status} ${dynRunNone.text.slice(0, 200)}`)

  // WF-19f 单数 config.wikiId 静态配置（现行为）也可直接检索
  const buildKsSingleDefinition = () => {
    const def = buildKsDynDefinition()
    const ks = def.nodes.find((n) => n.key === 'ks')
    ks.config = { wikiId: WIKI, topK: 3 }
    delete ks.inputs.wikiId
    return def
  }
  const singleSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildKsSingleDefinition()), editorData: '{}' } })
  check('WF-19f 单数静态配置保存 200', singleSave.status === 200, `${singleSave.status} ${singleSave.text.slice(0, 160)}`)
  const singleRun = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: '静态单库' }) } })
  const sr = singleRun.json ?? {}
  const singleKs = (sr.nodes ?? []).find((n) => n.nodeKey === 'ks')
  check('WF-19g 单数静态知识库配置执行完成', singleRun.status === 200 && sr.status === 'completed' && singleKs?.state === 'completed', `${singleRun.status} ${singleRun.text.slice(0, 160)}`)
  // ==================== WF-20 问题分类节点（questionClassifier） ====================
  // start(query) → clf(问题分类) → [c1] hit1 / [c2] hit2 → end；AI 调用不可桩，此处验证
  // 校验闭环（缺分类/标记无效发布 400）与确定性失败路径（未配置模型节点失败挂起）
  const buildClfDefinition = () => ({
    id: '', name: 'wf-clf', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
      {
        key: 'clf', name: '问题分类', type: 'questionClassifier',
        config: { aiModelId: '', historyCount: 6, classes: [{ id: 'c1', label: '售前咨询' }, { id: 'c2', label: '售后咨询' }] },
        inputs: {
          query: { expressionType: 'variable', value: 'start.question', required: true },
          history: { expressionType: 'variable', value: 'start.history', required: false },
        },
        outputs: [
          { name: 'result', fieldType: 'string', description: '命中的分类 id' },
          { name: 'className', fieldType: 'string', description: '命中的分类名称' },
        ],
      },
      { key: 'hit1', name: '分支一', type: 'javaScript', config: { code: 'function run(inputs) { return { answer: "one" } }' }, inputs: {}, outputs: [{ name: 'answer', fieldType: 'string' }] },
      { key: 'hit2', name: '分支二', type: 'javaScript', config: { code: 'function run(inputs) { return { answer: "two" } }' }, inputs: {}, outputs: [{ name: 'answer', fieldType: 'string' }] },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: {
          one: { expressionType: 'variable', value: 'hit1.answer', required: false },
          two: { expressionType: 'variable', value: 'hit2.answer', required: false },
        },
        outputs: [{ name: 'one', fieldType: 'string' }, { name: 'two', fieldType: 'string' }],
      },
    ],
    connections: [
      { id: 'f1', source: 'start', target: 'clf' },
      { id: 'f2', source: 'clf', target: 'hit1', condition: 'c1' },
      { id: 'f3', source: 'clf', target: 'hit2', condition: 'c2' },
      { id: 'f4', source: 'hit1', target: 'end' },
      { id: 'f5', source: 'hit2', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, clf: { x: 300, y: 200 }, hit1: { x: 520, y: 120 }, hit2: { x: 520, y: 280 }, end: { x: 740, y: 200 } } },
  })

  // WF-20a 合法问题分类定义保存 200（草稿轻校验不拦）
  const clfSave = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildClfDefinition()), editorData: JSON.stringify(buildEditorData(buildClfDefinition())) } })
  check('WF-20a 问题分类定义保存 200', clfSave.status === 200, `${clfSave.status} ${clfSave.text.slice(0, 160)}`)

  // WF-20b 缺分类（空 classes）发布 400
  const noClassesDef = buildClfDefinition()
  noClassesDef.nodes.find((n) => n.key === 'clf').config.classes = []
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(noClassesDef), editorData: '{}' } })
  const noClassesPublish = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-20b 缺分类发布 400', noClassesPublish.status === 400 && noClassesPublish.text.includes('分类'), `${noClassesPublish.status} ${noClassesPublish.text.slice(0, 160)}`)

  // WF-20c 出边分类标记无效（else）发布 400
  const badMarkerDef = buildClfDefinition()
  badMarkerDef.connections.find((c) => c.id === 'f3').condition = 'else'
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(badMarkerDef), editorData: '{}' } })
  const badMarkerPublish = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-20c 出边分类标记无效发布 400', badMarkerPublish.status === 400 && badMarkerPublish.text.includes('分类标记无效'), `${badMarkerPublish.status} ${badMarkerPublish.text.slice(0, 160)}`)

  // WF-20d 恢复合法定义并发布 200
  await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildClfDefinition()), editorData: JSON.stringify(buildEditorData(buildClfDefinition())) } })
  const clfPublish = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-20d 含问题分类节点发布 200', clfPublish.status === 200, `${clfPublish.status} ${clfPublish.text.slice(0, 160)}`)

  // WF-20e 未配置 AI 模型：分类节点失败、实例挂起
  const clfRun = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: '怎么退货' }) } })
  const cr = clfRun.json ?? {}
  const clfNode = (cr.nodes ?? []).find((n) => n.nodeKey === 'clf')
  check('WF-20e 未配置模型节点失败挂起', clfRun.status === 200 && cr.status === 'suspended' && clfNode?.state === 'failed' && (clfNode.errorMessage ?? '').includes('模型'), `${clfRun.status} ${clfRun.text.slice(0, 200)}`)
  // WF-20f 分类失败后下游分支未执行（实例挂起，下游入边保持待执行）
  const hitStates = (cr.nodes ?? []).filter((n) => ['hit1', 'hit2'].includes(n.nodeKey)).map((n) => n.state)
  check('WF-20f 分类失败下游分支未执行', hitStates.length === 2 && hitStates.every((s) => s === 'pending'), JSON.stringify(cr.nodes)?.slice(0, 200))

  // 恢复合法定义草稿，保持与已发布快照一致
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildClfDefinition()), editorData: JSON.stringify(buildEditorData(buildClfDefinition())) } })

  // ==================== WF-21 HTTP 请求节点（http） ====================
  // 脚本内起本地桩服务，后端调试执行时回环访问：/get 回显查询参数、/fail 返回 500、/slow 延迟 4s
  const stubHits = []
  const stub = http.createServer((req, res) => {
    res.on('error', () => {})
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      stubHits.push({ method: req.method, url: req.url, contentType: req.headers['content-type'] ?? '', auth: req.headers.authorization ?? '', body })
      if ((req.url ?? '').startsWith('/fail')) {
        res.writeHead(500, { 'Content-Type': 'application/json' })
        res.end('{"error":"boom"}')
        return
      }

      if ((req.url ?? '').startsWith('/slow')) {
        setTimeout(() => {
          if (res.writableEnded || res.destroyed) return
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"ok":true}')
        }, 4000)
        return
      }

      const query = new URL(req.url ?? 'http://x/', 'http://x/').searchParams
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ data: { title: 'stub-title' }, echo: query.get('q') ?? '', bodyLen: body.length }))
    })
  })
  await new Promise((resolve) => stub.listen(0, '127.0.0.1', resolve))
  stub.unref()
  const STUB_PORT = stub.address().port
  const stubUrl = (path) => `http://127.0.0.1:${STUB_PORT}${path}`

  const buildHttpDefinition = ({ url, method = 'GET', extraConfig = {} }) => ({
    id: '', name: 'wf-http', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
      {
        key: 'http1', name: 'HTTP 请求', type: 'http',
        config: {
          method, url, timeoutSeconds: 30,
          params: [], headers: [], bodyType: 'none', body: '', errorCapture: false,
          extract: [{ name: 'title', path: '$.data.title', fieldType: 'string' }],
          ...extraConfig,
        },
        inputs: {},
        outputs: [
          { name: 'statusCode', fieldType: 'number' },
          { name: 'rawResponse', fieldType: 'dynamic' },
          { name: 'hasError', fieldType: 'boolean' },
          { name: 'errorMessage', fieldType: 'string' },
          { name: 'title', fieldType: 'string' },
        ],
      },
      { key: 'end', name: '结束', type: 'end', inputs: { title: { expressionType: 'variable', value: 'http1.title', required: false } }, outputs: [{ name: 'title', fieldType: 'string' }] },
    ],
    connections: [
      { id: 'h1', source: 'start', target: 'http1' },
      { id: 'h2', source: 'http1', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, http1: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })
  const httpNodeOf = (r) => (r.nodes ?? []).find((n) => n.nodeKey === 'http1')
  const httpOutputOf = (r) => { const o = httpNodeOf(r)?.output; return o ? JSON.parse(o) : null }

  // WF-21a GET + 查询参数（插值引用启动参数）+ 输出字段提取，下游 end 引用提取结果
  const getDef = buildHttpDefinition({ url: stubUrl('/get'), extraConfig: { params: [{ name: 'q', value: '{start.question}' }] } })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(getDef), editorData: JSON.stringify(buildEditorData(getDef)) } })
  const hitsBefore = stubHits.length
  const httpGetRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'hello' }) } })
  const hgr = httpGetRun.json ?? {}
  const hgOut = httpOutputOf(hgr)
  check('WF-21a HTTP GET 节点执行完成', httpGetRun.status === 200 && hgr.status === 'completed' && hgOut !== null && hgOut.statusCode === 200 && hgOut.hasError === false, `${httpGetRun.status} ${httpGetRun.text.slice(0, 200)}`)
  const stubCall = stubHits[hitsBefore]
  check('WF-21b 桩收到插值查询参数', stubCall !== undefined && stubCall.url === '/get?q=hello', JSON.stringify(stubHits.slice(hitsBefore)).slice(0, 200))
  check('WF-21c 提取字段与下游引用', hgOut?.title === 'stub-title' && JSON.parse(hgr.nodes.find((n) => n.nodeKey === 'end').output).title === 'stub-title', `${JSON.stringify(hgOut)} ${httpGetRun.text.slice(0, 120)}`)

  // WF-21d POST JSON 请求体（插值替换字符串与数值位置）
  const postDef = buildHttpDefinition({ url: stubUrl('/post'), method: 'POST', extraConfig: { bodyType: 'json', body: '{"q":"{start.question}","n":3}' } })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(postDef), editorData: JSON.stringify(buildEditorData(postDef)) } })
  const hitsBeforePost = stubHits.length
  const httpPostRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'hi' }) } })
  const hpr = httpPostRun.json ?? {}
  const postCall = stubHits[hitsBeforePost]
  check('WF-21d POST JSON 请求体与 Content-Type', httpPostRun.status === 200 && hpr.status === 'completed' && postCall !== undefined && postCall.method === 'POST' && postCall.body === '{"q":"hi","n":3}' && postCall.contentType.startsWith('application/json'), `${httpPostRun.status} ${JSON.stringify(postCall ?? {}).slice(0, 200)}`)

  // WF-21e 非 2xx 且未开报错捕获：节点失败、实例挂起
  const failDef = buildHttpDefinition({ url: stubUrl('/fail') })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(failDef), editorData: JSON.stringify(buildEditorData(failDef)) } })
  const httpFailRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x' }) } })
  const hfr = httpFailRun.json ?? {}
  const failNode = httpNodeOf(hfr)
  check('WF-21e 非 2xx 默认节点失败挂起', httpFailRun.status === 200 && hfr.status === 'suspended' && failNode?.state === 'failed' && (failNode.errorMessage ?? '').includes('500'), `${httpFailRun.status} ${httpFailRun.text.slice(0, 200)}`)

  // WF-21f 开启报错捕获：节点完成输出 hasError/errorMessage，下游可继续
  const captureDef = buildHttpDefinition({ url: stubUrl('/fail'), extraConfig: { errorCapture: true } })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(captureDef), editorData: JSON.stringify(buildEditorData(captureDef)) } })
  const httpCaptureRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x' }) } })
  const hcr = httpCaptureRun.json ?? {}
  const hcOut = httpOutputOf(hcr)
  check('WF-21f 报错捕获完成且输出错误信息', httpCaptureRun.status === 200 && hcr.status === 'completed' && hcOut !== null && hcOut.hasError === true && (hcOut.errorMessage ?? '').includes('500') && hcOut.statusCode === 500, `${httpCaptureRun.status} ${JSON.stringify(hcOut ?? {}).slice(0, 200)}`)
  check('WF-21g 报错捕获下游继续执行', (hcr.nodes ?? []).find((n) => n.nodeKey === 'end')?.state === 'completed' && JSON.parse((hcr.nodes ?? []).find((n) => n.nodeKey === 'end').output).title === null, JSON.stringify(hcr.nodes)?.slice(0, 200))

  // WF-21h 超时：桩延迟 4s、节点超时 1s → 节点失败并提示超时
  const slowDef = buildHttpDefinition({ url: stubUrl('/slow'), extraConfig: { timeoutSeconds: 1 } })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(slowDef), editorData: JSON.stringify(buildEditorData(slowDef)) } })
  const httpSlowRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: 'x' }) } })
  const hsr = httpSlowRun.json ?? {}
  const slowNode = httpNodeOf(hsr)
  check('WF-21h 请求超时节点失败', httpSlowRun.status === 200 && hsr.status === 'suspended' && slowNode?.state === 'failed' && (slowNode.errorMessage ?? '').includes('超时'), `${httpSlowRun.status} ${httpSlowRun.text.slice(0, 200)}`)

  // WF-21i 配置校验：非法方法/缺地址发布 400
  const badMethodDef = buildHttpDefinition({ url: stubUrl('/get'), extraConfig: { method: 'FETCH' } })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(badMethodDef), editorData: '{}' } })
  const badMethodPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-21i 非法请求方法发布 400', badMethodPublish.status === 400 && badMethodPublish.text.includes('请求方法无效'), `${badMethodPublish.status} ${badMethodPublish.text.slice(0, 160)}`)

  const noUrlDef = buildHttpDefinition({ url: '' })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(noUrlDef), editorData: '{}' } })
  const noUrlPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-21j 缺请求地址发布 400', noUrlPublish.status === 400 && noUrlPublish.text.includes('未配置请求地址'), `${noUrlPublish.status} ${noUrlPublish.text.slice(0, 160)}`)

  // 恢复合法定义草稿，保持与已发布快照一致
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(getDef), editorData: JSON.stringify(buildEditorData(getDef)) } })

  // ==================== WF-22 发布应用对话（会话 + sys.* 系统变量） ====================
  // 「对话回显」流程：start(query) → echo(JS 读 sys.*) → end(reply=插值)。
  // 验证：流程应用会话创建、AG-UI 对话执行发布流程、sys.userId/appId/conversationId/messageId/currentTime
  //       注入、sys.history 跨轮累积、会话消息落库、未发布流程对话失败可见。
  const chatSse = async (appId, token, sessionId, text, extraHeaders = {}) => {
    const res = await fetch(`${BASE}/api/agent/${appId}/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...extraHeaders },
      body: JSON.stringify({
        threadId: sessionId,
        runId: crypto.randomUUID(),
        state: {},
        messages: [{ id: crypto.randomUUID(), role: 'user', content: text }],
        tools: [],
        context: [],
        forwardedProps: {},
      }),
    })
    const raw = await res.text()
    const events = raw.split('\n')
      .filter((l) => l.startsWith('data:'))
      .map((l) => { try { return JSON.parse(l.slice(5).trim()) } catch { return null } })
      .filter(Boolean)
    const reply = events
      .filter((e) => e.type === 'TEXT_MESSAGE_CONTENT' && typeof e.delta === 'string')
      .map((e) => e.delta)
      .join('')
    const runError = events.find((e) => e.type === 'RUN_ERROR')?.message ?? null
    return { status: res.status, reply, runError }
  }
  const poll = async (fn, ms = 8000) => {
    const deadline = Date.now() + ms
    while (Date.now() < deadline) {
      if (await fn()) return true
      await new Promise((r) => setTimeout(r, 400))
    }
    return false
  }

  const buildChatDefinition = () => ({
    id: '', name: 'wf-chat', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户消息' }] },
      {
        key: 'echo', name: '回显系统变量', type: 'javaScript',
        config: { code: 'function run(inputs, sys) {\n  return { userId: sys.userId || "", appId: sys.appId || "", conversationId: sys.conversationId || "", messageId: sys.messageId || "", historyCount: (sys.history || []).length, currentTime: sys.currentTime || "" };\n}' },
        inputs: {},
        outputs: [
          { name: 'userId', fieldType: 'string' },
          { name: 'appId', fieldType: 'string' },
          { name: 'conversationId', fieldType: 'string' },
          { name: 'messageId', fieldType: 'string' },
          { name: 'historyCount', fieldType: 'number' },
          { name: 'currentTime', fieldType: 'string' },
        ],
      },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: { reply: { expressionType: 'interpolation', value: 'uid={echo.userId}|app={echo.appId}|conv={echo.conversationId}|msg={echo.messageId}|h={echo.historyCount}|t={echo.currentTime}|q={start.question}', required: true } },
        outputs: [{ name: 'reply', fieldType: 'string' }],
      },
    ],
    connections: [
      { id: 'h1', source: 'start', target: 'echo' },
      { id: 'h2', source: 'echo', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, echo: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })

  // WF-22a Member 对已发布流程应用创建会话 200（此前仅 Agent 应用允许）
  const wfSession = await api('POST', `/api/app/${APP}/session`, { token: member.token, body: { title: 'wf 对话', promptId: 0 } })
  const WSESSION = String(wfSession.json?.value ?? '')
  check('WF-22a 流程应用创建会话 200', wfSession.status === 200 && isGuid(WSESSION), `${wfSession.status} ${wfSession.text.slice(0, 160)}`)
  check('WF-22b 非成员创建会话 404', (await api('POST', `/api/app/${APP}/session`, { token: outsider.token, body: { promptId: 0 } })).status === 404)

  // WF-22c 已发布定义为问题分类流程（无模型）：对话驱动执行失败，错误文本可见
  const clfChat = await chatSse(APP, member.token, WSESSION, '怎么退货')
  check('WF-22c 对话执行失败错误可见', clfChat.status === 200 && clfChat.reply.includes('执行失败') && clfChat.runError === null, `status=${clfChat.status} reply=${clfChat.reply.slice(0, 160)} err=${clfChat.runError}`)

  // WF-22d 发布「对话回显」流程
  const chatSave = await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildChatDefinition()), editorData: JSON.stringify(buildEditorData(buildChatDefinition())) } })
  check('WF-22d 对话回显草稿保存 200', chatSave.status === 200, `${chatSave.status} ${chatSave.text.slice(0, 160)}`)
  const chatPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-22e 对话回显发布 200', chatPublish.status === 200, `${chatPublish.status} ${chatPublish.text.slice(0, 160)}`)

  // WF-22f~h 第一轮：sys.userId/appId/conversationId/messageId/currentTime 注入，history 为空
  const chat1 = await chatSse(APP, member.token, WSESSION, '你好一')
  const reply1 = chat1.reply
  check('WF-22f 第一轮对话完成', chat1.status === 200 && chat1.runError === null && reply1.includes(`uid=${member.userId}`) && reply1.includes(`app=${APP}`), `status=${chat1.status} reply=${reply1.slice(0, 200)} err=${chat1.runError}`)
  check('WF-22g 会话 id 注入 conversationId', reply1.includes(`conv=${WSESSION}`), reply1.slice(0, 200))
  check('WF-22h messageId/currentTime 注入且 history 为空', /msg=[0-9a-f]{32}/.test(reply1) && /t=\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}/.test(reply1) && reply1.includes('h=0'), reply1.slice(0, 200))

  // WF-22i 第二轮：sys.history 含第一轮 user+assistant 两条
  const chat2 = await chatSse(APP, member.token, WSESSION, '你好二')
  check('WF-22i 第二轮历史记录累积', chat2.status === 200 && chat2.runError === null && chat2.reply.includes('h=2') && chat2.reply.includes('q=你好二') && chat2.reply.includes(`conv=${WSESSION}`), `reply=${chat2.reply.slice(0, 200)} err=${chat2.runError}`)

  // WF-22j 会话消息落库（user/assistant 交替 4 条）
  const msgOk = await poll(async () => {
    const m = await api('GET', `/api/app/session/${WSESSION}/messages`, { token: member.token })
    const items = m.json?.items ?? []
    return items.length >= 4 && items[0].role === 'user' && items[1].role === 'assistant' && items[2].role === 'user' && items[3].role === 'assistant'
      && (items[2].content ?? '') === '你好二' && (items[1].content ?? '').includes(`conv=${WSESSION}`)
  })
  check('WF-22j 会话消息落库且回复正确', msgOk, 'messages 轮询超时')

  // WF-22k 会话列表可见；WF-22l 对话实例计入运行历史（非调试）
  const slist = await api('GET', `/api/app/${APP}/session/list`, { token: member.token })
  check('WF-22k 会话列表含新会话', slist.status === 200 && (slist.json?.items ?? []).some((x) => String(x.sessionId) === WSESSION), slist.text.slice(0, 160))
  const instAfter = await api('GET', `/api/app/workflow/instances?appId=${APP}&teamId=${TID}&pageNo=1&pageSize=50`, { token: owner.token })
  check('WF-22l 对话实例计入运行历史', instAfter.status === 200 && (instAfter.json?.total ?? 0) >= 2, `${instAfter.status} ${instAfter.text.slice(0, 160)}`)

  // ==================== WF-23 开始节点 question 契约 ====================
  // 「问题透传」流程：start(question) → qjs(JS 读 nodes.start.question 与镜像 nodes.start.query) → end。
  // 验证：流程对话时用户问题注入开始节点 question 字段；旧编排绑定的 start.query 镜像同值（后向兼容）
  const buildQuestionDefinition = () => ({
    id: '', name: 'wf-q', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
      {
        key: 'qjs', name: '读取问题', type: 'javaScript',
        config: { code: 'function run(inputs, sys, nodes) {\n  return { q: nodes.start.question || "", legacy: nodes.start.query || "" };\n}' },
        inputs: {},
        outputs: [{ name: 'q', fieldType: 'string' }, { name: 'legacy', fieldType: 'string' }],
      },
      {
        key: 'end', name: '结束', type: 'end',
        inputs: { reply: { expressionType: 'interpolation', value: 'q={qjs.q}|legacy={qjs.legacy}', required: true } },
        outputs: [{ name: 'reply', fieldType: 'string' }],
      },
    ],
    connections: [
      { id: 'w1', source: 'start', target: 'qjs' },
      { id: 'w2', source: 'qjs', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 }, qjs: { x: 320, y: 200 }, end: { x: 560, y: 200 } } },
  })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildQuestionDefinition()), editorData: JSON.stringify(buildEditorData(buildQuestionDefinition())) } })
  const qPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-23a 问题透传流程发布 200', qPublish.status === 200, `${qPublish.status} ${qPublish.text.slice(0, 160)}`)
  const qSession = await api('POST', `/api/app/${APP}/session`, { token: member.token, body: { title: 'wf 问题契约', promptId: 0 } })
  const QSESSION = String(qSession.json?.value ?? '')
  const qChat = await chatSse(APP, member.token, QSESSION, '今天天气如何')
  check('WF-23b 用户问题注入 start.question', qChat.status === 200 && qChat.runError === null && qChat.reply.includes('q=今天天气如何'), `status=${qChat.status} reply=${qChat.reply.slice(0, 160)} err=${qChat.runError}`)
  check('WF-23c 旧编排 start.query 镜像同值', qChat.reply.includes('legacy=今天天气如何'), qChat.reply.slice(0, 160))

  // WF-23d 调试执行同样补 query 镜像：历史草稿（start 声明旧参数 query、下游绑定 start.query）
  // 仅传固定契约 question 也能跑通 —— 调试与对话行为一致
  const buildLegacyQueryDefinition = () => ({
    id: '', name: 'wf-legacy-q', version: 0, status: 'draft',
    nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'query', fieldType: 'string', isRequired: true, description: '旧参数' }] },
      { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'variable', value: 'start.query', required: true } }, outputs: [] },
    ],
    connections: [{ id: 'l1', source: 'start', target: 'end' }],
    ui: { nodePositions: { start: { x: 80, y: 200 }, end: { x: 560, y: 200 } } },
  })
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildLegacyQueryDefinition()), editorData: '{}' } })
  const legacyRun = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: APP, teamId: TID, inputJson: JSON.stringify({ question: '旧编排兼容问题' }) } })
  const lr = legacyRun.json ?? {}
  check('WF-23d 调试执行 query 镜像兼容旧编排', legacyRun.status === 200 && lr.status === 'completed' && lr.output && JSON.parse(lr.output).reply === '旧编排兼容问题', `${legacyRun.status} ${legacyRun.text.slice(0, 200)}`)

  // ==================== WF-24 对话历史压缩（sys.history 有界） ====================
  // 恢复发布「对话回显」流程；同一会话连发 10 轮，末轮 sys.history 经 MAF 压缩管线
  //（滑动窗口 PreserveTurns=6）约束在 ~12 条（≤14），不随轮次线性增长
  await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP, teamId: TID, definition: JSON.stringify(buildChatDefinition()), editorData: JSON.stringify(buildEditorData(buildChatDefinition())) } })
  const hcPublish = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: APP, teamId: TID } })
  check('WF-24a 回显流程重新发布 200', hcPublish.status === 200, `${hcPublish.status} ${hcPublish.text.slice(0, 160)}`)
  const cSession = await api('POST', `/api/app/${APP}/session`, { token: member.token, body: { title: 'wf 压缩', promptId: 0 } })
  const CSESSION = String(cSession.json?.value ?? '')
  let lastH = -1
  let lastReply = ''
  for (let i = 1; i <= 10; i++) {
    const r = await chatSse(APP, member.token, CSESSION, `第${i}轮问题`)
    lastReply = r.reply
    if (r.runError !== null) { lastH = -1; break }
    const m = /h=(\d+)/.exec(r.reply)
    if (!m) { lastH = -1; break }
    lastH = Number(m[1])
  }
  check('WF-24b 十轮对话全部成功', lastH >= 0 && lastReply.includes('q=第10轮问题'), `lastH=${lastH} reply=${lastReply.slice(0, 120)}`)
  check('WF-24c 历史压缩后有界（2 ≤ h ≤ 14）', lastH >= 2 && lastH <= 14, `lastH=${lastH}`)

  // ==================== WF-25 调试 Tab 按最新草稿执行（免发布，与内部应用一致） ====================
  // 「调试」Tab 携带 X-Moai-Workflow-Draft=1：未发布流程也能对话（按最新草稿，仅团队管理员），
  // 实例在运行历史中记为「调试」类型；正式对话（无该头）仍要求已发布。
  const draftHeaders = { 'X-Moai-Workflow-Draft': '1' }
  const w2 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '草稿调试' + TS, appType: 'workflow' } })
  const APP2 = String(w2.json?.value ?? '')
  check('WF-25a 新建未发布流程应用', w2.status === 200 && isGuid(APP2), `${w2.status} ${w2.text.slice(0, 120)}`)
  const draftSave = await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: APP2, teamId: TID, definition: JSON.stringify(buildQuestionDefinition()), editorData: '{}' } })
  check('WF-25b 保存草稿不发布', draftSave.status === 200, `${draftSave.status} ${draftSave.text.slice(0, 120)}`)
  const dSession = await api('POST', `/api/app/${APP2}/session`, { token: owner.token, body: { title: '草稿调试会话', promptId: 0 } })
  const DSESSION = String(dSession.json?.value ?? '')
  check('WF-25c 管理员对未发布应用建会话', dSession.status === 200 && isGuid(DSESSION), `${dSession.status} ${dSession.text.slice(0, 120)}`)
  const draftChat = await chatSse(APP2, owner.token, DSESSION, '草稿模式问题', draftHeaders)
  check('WF-25d 未发布草稿调试对话成功', draftChat.status === 200 && draftChat.runError === null && draftChat.reply.includes('q=草稿模式问题'), `status=${draftChat.status} reply=${draftChat.reply.slice(0, 160)} err=${draftChat.runError}`)
  const formalChat = await chatSse(APP2, owner.token, DSESSION, '正式对话')
  check('WF-25e 未发布正式对话仍被拒', formalChat.status === 200 && formalChat.reply.includes('尚未发布'), `reply=${formalChat.reply.slice(0, 160)}`)
  const memberDraft = await chatSse(APP, member.token, WSESSION, '越权草稿调试', draftHeaders)
  check('WF-25f 普通成员携带草稿头被拒', memberDraft.status === 200 && memberDraft.reply.includes('仅团队管理员'), `reply=${memberDraft.reply.slice(0, 160)}`)
  const draftInstances = await api('GET', `/api/app/workflow/instances?appId=${APP2}&teamId=${TID}&pageNo=1&pageSize=10`, { token: owner.token })
  const draftItem = (draftInstances.json?.items ?? [])[0]
  check('WF-25g 草稿调试实例计入运行历史且为调试类型', draftInstances.status === 200 && (draftInstances.json?.total ?? 0) >= 1 && draftItem?.isDebug === true, `${draftInstances.status} ${JSON.stringify(draftItem ?? {}).slice(0, 160)}`)

  // ==================== WF-26 Agent 应用节点 + 循环嵌套防护 ====================
  // 流程可经 agentApp 节点调用已发布 Agent 应用；Agent 又可把流程绑为工具（workflow_apps），
  // 二者成环须被拦截：选项标记 circular、保存/调试 400、运行期以根流程为基准防线。
  let usableModelId = ''
  {
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken
    if (adminToken) {
      const all = await api('GET', '/api/ai/model', { token: adminToken })
      const chat = (all.json?.items ?? []).find((m) => !/embed|rerank/i.test(String(m.modelId ?? m.name ?? '')))
      if (chat) {
        const cur = await api('GET', `/api/ai/model/${chat.id}/authorization`, { token: adminToken })
        const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
        const grant = await api('PUT', `/api/ai/model/${chat.id}/authorization`, { token: adminToken, body: { teamIds } })
        if (grant.status === 200) usableModelId = String(chat.id)
      }
    }
  }
  check('WF-26a 测试团队获得可用对话模型', !!usableModelId, usableModelId || '未拿到 admin/模型授权')
  if (usableModelId) {
    const ag = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '流程节点Agent' + TS, appType: 'agent' } })
    const AG = String(ag.json?.value ?? '')
    check('WF-26b 创建 Agent 应用', ag.status === 200 && isGuid(AG), `${ag.status} ${ag.text.slice(0, 120)}`)
    await api('PUT', `/api/app/${AG}/agent-config`, { token: owner.token, body: { modelId: usableModelId, prompt: '你是回声助手，每句回答都以「Agent回复：」开头。', wikiIds: [], plugins: [] } })
    const agPub = await api('POST', `/api/app/${AG}/publish`, { token: owner.token })
    check('WF-26c Agent 应用配置模型并发布 200', agPub.status === 200, `${agPub.status} ${agPub.text.slice(0, 120)}`)

    const w3 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '调用Agent' + TS, appType: 'workflow' } })
    const W3 = String(w3.json?.value ?? '')
    const buildAgentFlow = () => ({
      id: '', name: 'wf-agent', version: 0, status: 'draft',
      nodes: [
        { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true }] },
        { key: 'agent', name: 'Agent 节点', type: 'agentApp', config: { agentAppId: AG }, inputs: { prompt: { expressionType: 'interpolation', value: '用户问题：{start.question}', required: true } }, outputs: [{ name: 'answer', fieldType: 'string' }] },
        { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'variable', value: 'agent.answer', required: true } }, outputs: [] },
      ],
      connections: [{ id: 'g1', source: 'start', target: 'agent' }, { id: 'g2', source: 'agent', target: 'end' }],
      ui: {},
    })
    const opt1 = await api('GET', `/api/app/workflow/agent-options?appId=${W3}&teamId=${TID}`, { token: owner.token })
    const optItem = (opt1.json?.items ?? []).find((x) => String(x.appId) === AG)
    check('WF-26d 选项含已发布 Agent 且未成环', opt1.status === 200 && !!optItem && optItem.circular === false, opt1.text.slice(0, 160))
    const run3 = await api('POST', '/api/app/workflow/debug-run', { token: owner.token, body: { appId: W3, teamId: TID, definition: JSON.stringify(buildAgentFlow()), editorData: '{}', inputJson: JSON.stringify({ question: '你好' }) } })
    const r3 = run3.json ?? {}
    check('WF-26e Agent 节点调试执行完成', run3.status === 200 && r3.status === 'completed', `${run3.status} ${run3.text.slice(0, 200)}`)
    check('WF-26f Agent 回复进入流程输出', r3.output && String(JSON.parse(r3.output).reply ?? '').length > 0, r3.output ?? '')

    // 成环：发布 W3 → Agent 绑定 W3 为流程工具并重新发布（快照生效）→ 选项标记、保存/调试被拦
    const w3Pub = await api('POST', `/api/app/workflow/publish`, { token: owner.token, body: { appId: W3, teamId: TID } })
    check('WF-26g 流程发布 200', w3Pub.status === 200, `${w3Pub.status}`)
    await api('PUT', `/api/app/${AG}/agent-config`, { token: owner.token, body: { modelId: usableModelId, prompt: '你是回声助手，每句回答都以「Agent回复：」开头。', wikiIds: [], plugins: [], workflowApps: [W3] } })
    const agPub2 = await api('POST', `/api/app/${AG}/publish`, { token: owner.token })
    check('WF-26h Agent 绑定流程工具并重新发布 200', agPub2.status === 200, `${agPub2.status} ${agPub2.text.slice(0, 120)}`)
    const opt2 = await api('GET', `/api/app/workflow/agent-options?appId=${W3}&teamId=${TID}`, { token: owner.token })
    const optItem2 = (opt2.json?.items ?? []).find((x) => String(x.appId) === AG)
    check('WF-26i 成环后选项标记 circular', optItem2?.circular === true, JSON.stringify(optItem2 ?? {}))
    const saveCyclic = await api('POST', `/api/app/workflow/draft`, { token: owner.token, body: { appId: W3, teamId: TID, definition: JSON.stringify(buildAgentFlow()), editorData: '{}' } })
    check('WF-26j 成环后保存草稿被拒 400', saveCyclic.status === 400 && (saveCyclic.text ?? '').includes('循环嵌套'), `${saveCyclic.status} ${saveCyclic.text.slice(0, 160)}`)
    const runCyclic = await api('POST', `/api/app/workflow/debug-run`, { token: owner.token, body: { appId: W3, teamId: TID, inputJson: JSON.stringify({ question: 'hi' }) } })
    check('WF-26k 成环后调试执行被拒 400', runCyclic.status === 400 && (runCyclic.text ?? '').includes('循环嵌套'), `${runCyclic.status} ${runCyclic.text.slice(0, 160)}`)
  }

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
