// 应用删除验收：@AP-S91~S93。仅创建并删除本次隔离团队中的应用，不依赖真实模型。
// node local-dev/app-delete-e2e.mjs [baseUrl]；默认 http://127.0.0.1:5000，APP_BASE 可覆盖。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
const suffix = crypto.randomBytes(5).toString('hex')
let rsaPublic, passed = 0, failed = 0
const resources = []
async function api(method, path, token, body) {
  const response = await fetch(BASE + path, {
    method,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  let json
  try { json = JSON.parse(text) } catch { /* Empty/non-JSON response. */ }
  return { status: response.status, json, text }
}
function check(name, condition, detail = '') {
  if (condition) passed++
  else failed++
  console.log(`${condition ? 'PASS' : 'FAIL'} | ${name}${condition ? '' : ` — ${detail}`}`)
}
function requireOk(result, label) {
  if (result.status !== 200) throw new Error(`${label}: ${result.status} ${result.text.slice(0, 160)}`)
  return result.json
}
async function expectStatus(name, status, method, path, token, body) {
  const result = await api(method, path, token, body)
  check(name, result.status === status, `${result.status} ${result.text.slice(0, 160)}`)
  return result
}
function encrypt(value) {
  return crypto.publicEncrypt({ key: crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' }), padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(value)).toString('base64')
}
async function user(role) {
  const userName = `del${role}${suffix}`
  requireOk(await api('POST', '/api/auth/register', undefined, { userName, nickName: userName, email: `${userName}@test.local`, phone: `15${crypto.randomInt(100000000, 999999999)}`, password: encrypt('Test1234') }), 'register')
  const login = requireOk(await api('POST', '/api/auth/login', undefined, { userName, password: encrypt('Test1234') }), 'login')
  return { token: login.accessToken, id: Number(login.userId) }
}
async function createApp(teamId, ownerToken, name, appType = 'agent', isExternal = false) {
  const result = requireOk(await api('POST', '/api/app', ownerToken, { teamId, name, appType, isExternal }), `create ${name}`)
  resources.push({ id: result.value, token: ownerToken })
  return result.value
}

async function main() {
  rsaPublic = requireOk(await api('GET', '/api/common/serverinfo'), 'serverinfo').rsaPublic
  const owner = await user('o'), admin = await user('a'), member = await user('m'), outsider = await user('x')
  const teamId = Number(requireOk(await api('POST', '/api/team', owner.token, { name: `delete-${suffix}` }), 'create team').value)
  for (const [actor, role] of [[admin, 1], [member, 0]]) {
    requireOk(await api('POST', `/api/team/${teamId}/users`, owner.token, { userId: actor.id, role }), 'add member')
  }
  try {
    const app = await createApp(teamId, owner.token, '待删除Agent')
    const sibling = await createApp(teamId, owner.token, '保留对照应用')
    const unknown = crypto.randomUUID()
    await expectStatus('S91 未登录删除', 401, 'DELETE', `/api/app/${app}`)
    await expectStatus('S91 Member 删除', 403, 'DELETE', `/api/app/${app}`, member.token)
    await expectStatus('S91 非成员删除', 404, 'DELETE', `/api/app/${app}`, outsider.token)
    await expectStatus('S91 未知应用', 404, 'DELETE', `/api/app/${unknown}`, owner.token)
    await expectStatus('S91 拒绝删除未影响原应用', 200, 'GET', `/api/app/${app}`, owner.token)
    const session = requireOk(await api('POST', `/api/app/${app}/session`, owner.token, { title: '删除验收' }), 'create session').value
    requireOk(await api('POST', `/api/app/${app}/debug/session`, owner.token), 'create debug session')
    await expectStatus('S92 Admin 删除内部Agent', 200, 'DELETE', `/api/app/${app}`, admin.token)
    await expectStatus('S92 重复删除', 404, 'DELETE', `/api/app/${app}`, owner.token)
    for (const [method, path, body] of [
      ['GET', `/api/app/${app}`], ['GET', `/api/app/${app}/agent-config`],
      ['GET', `/api/app/${app}/session/list`], ['POST', `/api/app/${app}/session`, { title: '已删除' }],
      ['POST', `/api/app/${app}/debug/session`], ['GET', `/api/app/session/${session}/messages`],
      ['PUT', `/api/app/session/${session}/title`, { title: '不可改名' }],
      ['PUT', `/api/app/session/${session}/prompt`, { promptId: 0 }],
      ['POST', `/api/app/session/${session}/tool-approval`, { toolName: 'dummy', approved: true }],
      ['DELETE', `/api/app/session/${session}`],
    ]) await expectStatus(`S92 删除后 ${method} ${path.replace(app, '{app}').replace(session, '{session}')}`, 404, method, path, owner.token, body)
    const list = await api('GET', `/api/app/list?teamId=${teamId}`, owner.token)
    check('S92 内部列表移除应用且保留同团队其他应用', list.status === 200 && !list.json?.items?.some(x => x.appId === app) && list.json?.items?.some(x => x.appId === sibling), list.text.slice(0, 200))
    await expectStatus('S92 其他应用详情不受影响', 200, 'GET', `/api/app/${sibling}`, owner.token)
    const replacement = await createApp(teamId, owner.token, '待删除Agent')
    check('S92 删除后允许同名重建且使用新ID', replacement !== app)
    for (const [name, type, external] of [['内部流程', 'workflow', false], ['外部流程', 'workflow', true]]) {
      const id = await createApp(teamId, owner.token, name, type, external)
      await expectStatus(`S92 Owner 删除${name}`, 200, 'DELETE', `/api/app/${id}`, owner.token)
      await expectStatus(`S92 ${name}详情不可达`, 404, 'GET', `/api/app/${id}`, owner.token)
    }

    const external = await createApp(teamId, owner.token, '外部Agent', 'agent', true)
    requireOk(await api('POST', `/api/app/${external}/publish`, owner.token), 'publish external')
    const externalToken = requireOk(await api('POST', '/api/external/token', undefined, { appId: external }), 'external token').accessToken
    const externalSession = requireOk(await api('POST', `/api/external/agent/${external}/session`, externalToken, { title: '删除验收' }), 'external session').value
    await expectStatus('S92 删除已发布外部Agent', 200, 'DELETE', `/api/app/${external}`, owner.token)
    await expectStatus('S92 旧外部token不能读取消息', 404, 'GET', `/api/external/session/${externalSession}/messages`, externalToken)
    await expectStatus('S92 删除后不可匿名换token', 404, 'POST', '/api/external/token', undefined, { appId: external })
    await expectStatus('S92 删除后访问点不可达', 404, 'GET', `/api/external/app/${external}/access-point`)
    const externalList = await api('GET', `/api/app/external/list?teamId=${teamId}`, owner.token)
    check('S92 外部列表移除应用', externalList.status === 200 && !externalList.json?.items?.some(x => x.appId === external), externalList.text.slice(0, 200))

    const workflow = await createApp(teamId, owner.token, '引用目标流程', 'workflow')
    const host = await createApp(teamId, owner.token, '引用流程的Agent')
    const definition = { id: '', name: 'delete-reference', version: 0, status: 'draft', nodes: [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true }] },
      { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'interpolation', value: '{start.question}', required: false } }, outputs: [{ name: 'reply', fieldType: 'string' }] },
    ], connections: [{ id: 'c1', source: 'start', target: 'end' }], variables: [], ui: { nodePositions: {} } }
    requireOk(await api('POST', '/api/app/workflow/draft', owner.token, { appId: workflow, teamId, definition: JSON.stringify(definition), editorData: '{}' }), 'save workflow')
    requireOk(await api('POST', '/api/app/workflow/publish', owner.token, { appId: workflow, teamId }), 'publish workflow')
    const bind = workflowApps => api('PUT', `/api/app/${host}/agent-config`, owner.token, { prompt: '', wikiIds: [], plugins: [], workflowApps })
    requireOk(await bind([workflow]), 'bind workflow')
    await expectStatus('S93 草稿引用阻止删除', 409, 'DELETE', `/api/app/${workflow}`, owner.token)
    requireOk(await api('POST', `/api/app/${host}/publish`, owner.token), 'publish host')
    requireOk(await bind([]), 'unbind draft')
    await expectStatus('S93 发布快照引用仍阻止删除', 409, 'DELETE', `/api/app/${workflow}`, owner.token)
    await expectStatus('S93 拒绝删除后目标仍存在', 200, 'GET', `/api/app/${workflow}`, owner.token)
    requireOk(await api('POST', `/api/app/${host}/publish`, owner.token), 'republish host')
    await expectStatus('S93 草稿与发布快照均解绑后允许删除', 200, 'DELETE', `/api/app/${workflow}`, owner.token)

    const agentTarget = await createApp(teamId, owner.token, '节点引用目标Agent')
    const flowHost = await createApp(teamId, owner.token, '包含Agent节点的流程', 'workflow')
    requireOk(await api('POST', `/api/app/${agentTarget}/publish`, owner.token), 'publish agent target')
    const agentDefinition = structuredClone(definition)
    agentDefinition.nodes.splice(1, 0, { key: 'agent', name: 'Agent节点', type: 'agentApp', config: { agentAppId: agentTarget }, inputs: { prompt: { expressionType: 'interpolation', value: '{start.question}', required: true } }, outputs: [{ name: 'answer', fieldType: 'string' }] })
    agentDefinition.connections = [{ id: 'c1', source: 'start', target: 'agent' }, { id: 'c2', source: 'agent', target: 'end' }]
    const saveFlow = value => api('POST', '/api/app/workflow/draft', owner.token, { appId: flowHost, teamId, definition: JSON.stringify(value), editorData: '{}' })
    const publishFlow = () => api('POST', '/api/app/workflow/publish', owner.token, { appId: flowHost, teamId })
    requireOk(await saveFlow(agentDefinition), 'save agent node')
    await expectStatus('S93 流程草稿Agent节点引用阻止删除', 409, 'DELETE', `/api/app/${agentTarget}`, owner.token)
    requireOk(await publishFlow(), 'publish agent node')
    requireOk(await saveFlow(definition), 'remove agent node from draft')
    await expectStatus('S93 流程发布定义Agent节点仍阻止删除', 409, 'DELETE', `/api/app/${agentTarget}`, owner.token)
    requireOk(await publishFlow(), 'republish without agent node')
    await expectStatus('S93 流程双轨解除Agent节点后允许删除', 200, 'DELETE', `/api/app/${agentTarget}`, owner.token)
  } finally {
    // 只清理本次创建的应用；先删宿主，解除引用后再回收目标。
    for (const resource of resources.toReversed()) await api('DELETE', `/api/app/${resource.id}`, resource.token)
  }
  console.log(`\n== app-delete e2e: ${passed} pass, ${failed} fail ==`)
  if (failed) process.exitCode = 1
}
main().catch(error => { console.error(error); process.exitCode = 1 })
