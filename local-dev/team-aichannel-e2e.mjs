// 团队自有模型渠道 E2E（自带 OpenAI 兼容桩服务，零真实依赖）
// 用法: node local-dev/team-aichannel-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5000）
// 覆盖：
//   - 团队模型渠道 CRUD 门禁（Member 403 / 非成员 404）
//   - 渠道与模型管理（创建/更新/删除/重复校验/团队模型不可公开）
//   - 供应商模型同步（桩 /models）+ 挑选导入 + exists 标记
//   - 团队可用模型聚合：团队自有渠道 + 系统公开模型（/team/{teamId}/ai/model/available isTeamChannel 标记）
//   - 模型网关下线：/api/aigateway/* 404、model 范围代码 400、网关用量统计随之下线
//   - 团队隔离：其他团队看不到本团队自有模型；跨团队/系统渠道操作 404
//   - 额度体系下线：额度端点 404、团队模型无授权概念 400
const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
const crypto = await import('node:crypto')
const http = await import('node:http')

let pass = 0
let fail = 0
function check(name, cond, detail = '') {
  if (cond) {
    pass++
    console.log(`PASS ${name}`)
  } else {
    fail++
    console.log(`FAIL ${name} ${detail}`)
  }
}

// ---------- 本地 OpenAI 兼容桩服务 ----------
const STUB_PORT = 18900 + Math.floor(Math.random() * 500)
const stubModels = ['tc-stub-model-a', 'tc-stub-model-b']
const stubServer = http.createServer((req, res) => {
  const url = req.url ?? ''
  res.setHeader('Content-Type', 'application/json')
  if (url === '/v1/models') {
    res.end(JSON.stringify({ object: 'list', data: stubModels.map((id) => ({ id, object: 'model', owned_by: 'stub' })) }))
    return
  }
  if (url === '/v1/chat/completions') {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let model = 'unknown'
      try { model = JSON.parse(body)?.model ?? 'unknown' } catch { /* ignore */ }
      res.end(JSON.stringify({
        id: 'chatcmpl-stub',
        object: 'chat.completion',
        created: Math.floor(Date.now() / 1000),
        model,
        choices: [{ index: 0, message: { role: 'assistant', content: `stub reply for ${model}` }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 3, completion_tokens: 5, total_tokens: 8 },
      }))
    })
    return
  }
  res.statusCode = 404
  res.end(JSON.stringify({ error: 'not found' }))
})
await new Promise((resolve) => stubServer.listen(STUB_PORT, '127.0.0.1', resolve))
const STUB_BASE = `http://127.0.0.1:${STUB_PORT}/v1`

// ---------- HTTP 工具 ----------
const { rsaPublic } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())

function enc(text) {
  const key = crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(text)).toString('base64')
}

async function api(token, path, method = 'GET', payload) {
  const r = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  })
  let body = null
  try { body = await r.json() } catch { /* no body */ }
  return { status: r.status, body }
}

const ts = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${ts}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  await api(null, '/api/auth/register', 'POST', { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: enc('Test1234') })
  const l = await api(null, '/api/auth/login', 'POST', { userName: name, password: enc('Test1234') })
  return { name, userId: Number(l.body?.userId), token: l.body?.accessToken }
}

// ---------- 准备 ----------
const adminToken = await loginAdmin()
function loginAdmin() {
  return (async () => {
    const r = await api(null, '/api/auth/login', 'POST', { userName: 'admin', password: enc('abcd123456') })
    return r.body?.accessToken
  })()
}
check('admin 登录', Boolean(adminToken))

const owner = await mkuser('ow')
const member = await mkuser('me')
check('注册团队成员账号', Boolean(owner.token) && Boolean(member.token))

const team = await api(owner.token, '/api/team', 'POST', { name: `tch-e2e-${ts}`, description: 'team aichannel e2e' })
const teamId = Number(team.body?.value)
check('owner 创建团队', team.status === 200 && teamId > 0, JSON.stringify(team.body))
await api(owner.token, `/api/team/${teamId}/users`, 'POST', { userId: member.userId, role: 0 })

const other = await mkuser('ot')
const otherTeam = await api(other.token, '/api/team', 'POST', { name: `tch-e2e-b-${ts}`, description: 'team aichannel e2e b' })
const otherTeamId = Number(otherTeam.body?.value)
check('第二团队创建', otherTeam.status === 200 && otherTeamId > 0)

// ---------- 门禁 ----------
const chUrl = `/api/team/${teamId}/ai/channel`
const memberList = await api(member.token, chUrl)
check('TC-S1 成员查团队渠道 403', memberList.status === 403, `${memberList.status}`)
const outsiderList = await api(other.token, chUrl)
check('TC-S1 非成员查团队渠道 404', outsiderList.status === 404, `${outsiderList.status}`)

// ---------- 渠道 CRUD ----------
const createCh = await api(owner.token, chUrl, 'POST', {
  providerKey: 'openai', name: `tc-渠道-${ts}`, protocolFamily: 'openAIChatCompletions',
  baseUrl: STUB_BASE, apiKey: 'sk-stub-key', enabled: true,
})
check('TC-S2 owner 创建团队渠道', createCh.status === 200, JSON.stringify(createCh.body))

const dupCh = await api(owner.token, chUrl, 'POST', {
  providerKey: 'openai', name: `tc-渠道-${ts}`, protocolFamily: 'openAIChatCompletions', baseUrl: STUB_BASE, enabled: true,
})
check('TC-S2 渠道名重复 409', dupCh.status === 409, `${dupCh.status}`)

const chList = await api(owner.token, chUrl)
const channel = (chList.body?.items ?? []).find((x) => x.name === `tc-渠道-${ts}`)
check('TC-S2 渠道列表回显', chList.status === 200 && Boolean(channel?.id), JSON.stringify(chList.body))
const channelId = channel?.id

const updCh = await api(owner.token, `${chUrl}/${channelId}`, 'PUT', {
  providerKey: 'openai', name: `tc-渠道改-${ts}`, protocolFamily: 'openAIChatCompletions',
  baseUrl: STUB_BASE, enabled: true,
})
check('TC-S2 更新团队渠道', updCh.status === 200, JSON.stringify(updCh.body))

// 系统管理端看不到团队渠道
const adminChList = await api(adminToken, '/api/ai/channel')
check('TC-S2 管理端渠道列表不含团队渠道', adminChList.status === 200 && !(adminChList.body?.items ?? []).some((x) => x.name === `tc-渠道改-${ts}`))

// ---------- 模型管理 ----------
const mUrl = `/api/team/${teamId}/ai/model`
const createModel = await api(owner.token, mUrl, 'POST', {
  channelId, enabled: true, isPublic: true,
  meta: { modelId: 'tc-model-manual', name: 'TC 手动模型', modelKind: 'conversation' },
})
check('TC-S3 团队模型不支持公开 400', createModel.status === 400, `${createModel.status} ${JSON.stringify(createModel.body)}`)

const createModel2 = await api(owner.token, mUrl, 'POST', {
  channelId, enabled: true,
  meta: { modelId: 'tc-model-manual', name: 'TC 手动模型', modelKind: 'conversation', contextWindow: 8192 },
})
check('TC-S3 创建团队模型', createModel2.status === 200, JSON.stringify(createModel2.body))

const dupModel = await api(owner.token, mUrl, 'POST', {
  channelId, enabled: true,
  meta: { modelId: 'tc-model-manual', name: 'TC 手动模型', modelKind: 'conversation' },
})
check('TC-S3 渠道下模型重复 409', dupModel.status === 409, `${dupModel.status}`)

const modelList = await api(owner.token, `${mUrl}?channelId=${channelId}`)
const manualModel = (modelList.body?.items ?? []).find((x) => x.modelId === 'tc-model-manual')
check('TC-S3 模型列表回显', modelList.status === 200 && Boolean(manualModel?.id), JSON.stringify(modelList.body))
const manualModelId = manualModel?.id

const updModel = await api(owner.token, `${mUrl}/${manualModelId}`, 'PUT', {
  channelId, enabled: true,
  meta: { modelId: 'tc-model-manual', name: 'TC 手动模型改', modelKind: 'conversation', contextWindow: 8192 },
})
check('TC-S3 更新团队模型', updModel.status === 200, JSON.stringify(updModel.body))

// ---------- 同步 + 导入（桩 /models） ----------
const syncRes = await api(owner.token, `${mUrl}/sync`, 'POST', { channelId })
const syncItems = syncRes.body?.items ?? []
check('TC-S4 同步返回桩模型候选', syncRes.status === 200 && syncItems.length === stubModels.length && syncItems.every((x) => x.meta?.modelId), JSON.stringify(syncRes.body))

const importRes = await api(owner.token, `${mUrl}/import`, 'POST', {
  channelId,
  items: syncItems.map((x) => x.meta),
})
check('TC-S4 导入候选模型', importRes.status === 200, JSON.stringify(importRes.body))

const syncAgain = await api(owner.token, `${mUrl}/sync`, 'POST', { channelId })
check('TC-S4 再次同步 exists 标记', syncAgain.status === 200 && (syncAgain.body?.items ?? []).every((x) => x.exists === true), JSON.stringify(syncAgain.body))

// 导入默认停用，批量启用
let afterImport = await api(owner.token, `${mUrl}?channelId=${channelId}`)
let importedIds = (afterImport.body?.items ?? []).filter((x) => stubModels.includes(x.modelId)).map((x) => x.id)
check('TC-S4 导入默认停用', afterImport.status === 200 && importedIds.length === stubModels.length && (afterImport.body?.items ?? []).filter((x) => stubModels.includes(x.modelId)).every((x) => x.enabled === false))
const batchRes = await api(owner.token, `${mUrl}/batch`, 'POST', { modelIds: importedIds, enabled: true })
check('TC-S4 批量启用导入模型', batchRes.status === 200, JSON.stringify(batchRes.body))

// ---------- 团队可用模型聚合 ----------
// 系统公开模型（admin 创建）
const sysCh = await api(adminToken, '/api/ai/channel', 'POST', {
  providerKey: 'openai', name: `tc-sys-渠道-${ts}`, protocolFamily: 'openAIChatCompletions',
  baseUrl: STUB_BASE, apiKey: 'sk-stub-key', enabled: true,
})
check('TC-S5 admin 创建系统渠道', sysCh.status === 200, JSON.stringify(sysCh.body))
const sysChList = await api(adminToken, '/api/ai/channel')
const sysChannel = (sysChList.body?.items ?? []).find((x) => x.name === `tc-sys-渠道-${ts}`)
const sysModel = await api(adminToken, '/api/ai/model', 'POST', {
  channelId: sysChannel?.id, enabled: true, isPublic: true,
  meta: { modelId: 'tc-sys-public-model', name: 'TC 系统公开模型', modelKind: 'conversation' },
})
check('TC-S5 admin 创建系统公开模型', sysModel.status === 200, JSON.stringify(sysModel.body))

const gwModels = await api(owner.token, `/api/team/${teamId}/ai/model/available`)
const gwItems = gwModels.body?.items ?? []
const teamModel = gwItems.find((x) => x.modelId === 'tc-model-manual')
const sysPublic = gwItems.find((x) => x.modelId === 'tc-sys-public-model')
check('TC-S5 可用模型含团队自有模型 isTeamChannel=true', Boolean(teamModel) && teamModel?.isTeamChannel === true, JSON.stringify(gwItems.map((x) => [x.modelId, x.isTeamChannel])))
check('TC-S5 可用模型含系统公开模型 isTeamChannel=false', Boolean(sysPublic) && sysPublic?.isTeamChannel === false)

// ---------- 模型网关下线 ----------
// 团队模型网关整体下线：/api/aigateway/* 端点移除，model 范围代码不再合法
const gwChat = await fetch(`${BASE}/api/aigateway/${teamId}/v1/chat/completions`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ model: 'tc-model-manual', messages: [{ role: 'user', content: 'hi' }] }),
})
// 端点已不存在：404（无路由）或 405（路径落到其他路由模板）均证明下线
check('TC-S5 模型网关 chat/completions 端点已下线', gwChat.status === 404 || gwChat.status === 405, `${gwChat.status}`)
const gwModelsEp = await fetch(`${BASE}/api/aigateway/${teamId}/v1/models`)
check('TC-S5 模型网关 models 端点已下线 404', gwModelsEp.status === 404, `${gwModelsEp.status}`)

const acc = await api(owner.token, '/api/access-app', 'POST', { teamId, name: `tch-e2e-接入-${ts}`, description: 'team aichannel e2e', scopes: ['model'] })
check('TC-S5 创建应用接入含 model 范围 400（范围已随网关下线）', acc.status === 400, JSON.stringify(acc.body))

// ---------- 团队隔离 ----------
const otherModels = await api(other.token, `/api/team/${otherTeamId}/ai/model/available`)
check('TC-S6 其他团队看不到本团队自有模型', otherModels.status === 200 && !(otherModels.body?.items ?? []).some((x) => x.modelId === 'tc-model-manual'))

// 跨团队操作 404：other 团队 owner 尝试改/删本团队渠道
const crossUpd = await api(other.token, `/api/team/${otherTeamId}/ai/channel/${channelId}`, 'PUT', {
  providerKey: 'openai', name: 'hack', protocolFamily: 'openAIChatCompletions', enabled: true,
})
check('TC-S6 跨团队更新渠道 404', crossUpd.status === 404, `${crossUpd.status}`)
const crossDel = await api(other.token, `/api/team/${otherTeamId}/ai/channel/${channelId}`, 'DELETE')
check('TC-S6 跨团队删除渠道 404', crossDel.status === 404, `${crossDel.status}`)

// 团队端点操作系统渠道 404
const sysChDel = await api(owner.token, `${chUrl}/${sysChannel?.id}`, 'DELETE')
check('TC-S6 团队端点删除系统渠道 404', sysChDel.status === 404, `${sysChDel.status}`)

// ---------- 额度体系下线 ----------
// 端点已不存在：404（无路由）或 405（路径落到其他路由模板）均证明下线
const quotaPut = await api(adminToken, `/api/ai/model/${manualModelId}/quota/0`, 'PUT', { periodUnit: 2, periodValue: 1, limitValue: 1000 })
check('TC-S7 额度设置端点已移除', quotaPut.status === 404 || quotaPut.status === 405, `${quotaPut.status}`)
const quotaDel = await api(adminToken, `/api/ai/model/${manualModelId}/quota/0`, 'DELETE')
check('TC-S7 额度移除端点已移除', quotaDel.status === 404 || quotaDel.status === 405, `${quotaDel.status}`)
const teamModelAuth = await api(adminToken, `/api/ai/model/${manualModelId}/authorization`, 'PUT', { teamIds: [] })
check('TC-S7 团队模型无授权概念 400', teamModelAuth.status === 400, `${teamModelAuth.status}`)

// ---------- 清理 ----------
await api(owner.token, `${mUrl}/${manualModelId}`, 'DELETE')
await api(owner.token, chUrl, 'POST', undefined) // 占位无效请求忽略
const delCh = await api(owner.token, `${chUrl}/${channelId}`, 'DELETE')
check('清理：删除团队渠道', delCh.status === 200, `${delCh.status}`)
await api(adminToken, `/api/ai/channel/${sysChannel?.id}`, 'DELETE')
await api(owner.token, `/api/access-app/${acc.body?.accessAppId}`, 'DELETE')
await api(adminToken, `/api/admin/team/${teamId}/disable`, 'PUT', { isDisable: true })
await api(adminToken, `/api/admin/team/${otherTeamId}/disable`, 'PUT', { isDisable: true })

stubServer.close()
console.log(`\n结果: PASS=${pass} FAIL=${fail}`)
process.exit(fail > 0 ? 1 : 0)
