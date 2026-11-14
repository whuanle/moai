// 渠道级批量授权 E2E（零真实依赖）
// 用法: node local-dev/ai-channel-auth-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5000）
// 覆盖：
//   - 渠道级授权查询（模型数统计 + 各私有模型授权团队并集回显）
//   - 渠道级批量授权全量替换：所有私有模型授权一致、公开模型不受影响
//   - 模型列表 authorizedTeamNames 回显
//   - 授权后团队可用性变化（/team/{teamId}/ai/model/available）
//   - admin 门禁、团队自有渠道无渠道授权概念 400
const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
const crypto = await import('node:crypto')

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
      Authorization: token ? `Bearer ${token}` : '',
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
const admin = await mkuser('ad')
// admin 需要 root 权限：改用种子 root 账号
const adminToken = await (async () => {
  const r = await api(null, '/api/auth/login', 'POST', { userName: 'admin', password: enc('abcd123456') })
  return r.body?.accessToken
})()
check('admin(root) 登录', Boolean(adminToken))

const ownerA = await mkuser('aa')
const ownerB = await mkuser('ab')
const teamA = await api(ownerA.token, '/api/team', 'POST', { name: `ac-e2e-a-${ts}` })
const teamB = await api(ownerB.token, '/api/team', 'POST', { name: `ac-e2e-b-${ts}` })
const teamAId = Number(teamA.body?.value)
const teamBId = Number(teamB.body?.value)
check('准备两个团队', teamAId > 0 && teamBId > 0)

// ---------- 渠道与模型 ----------
const channel = await api(adminToken, '/api/ai/channel', 'POST', {
  providerKey: 'openai', name: `ac-渠道-${ts}`, protocolFamily: 'openAIChatCompletions',
  baseUrl: 'http://127.0.0.1:1/v1', apiKey: 'sk-stub', enabled: true,
})
check('admin 创建系统渠道', channel.status === 200, JSON.stringify(channel.body))
const chList = await api(adminToken, '/api/ai/channel')
const ch = (chList.body?.items ?? []).find((x) => x.name === `ac-渠道-${ts}`)
const chId = ch?.id

async function mkmodel(modelId, name, isPublic) {
  return api(adminToken, '/api/ai/model', 'POST', {
    channelId: chId, enabled: true, isPublic,
    meta: { modelId, name, modelKind: 'conversation' },
  })
}
await mkmodel('ac-public-model', 'AC 公开模型', true)
await mkmodel('ac-private-a', 'AC 私有A', false)
await mkmodel('ac-private-b', 'AC 私有B', false)
const modelList = await api(adminToken, `/api/ai/model?channelId=${chId}`)
const items = modelList.body?.items ?? []
const publicModel = items.find((x) => x.modelId === 'ac-public-model')
const privateIds = items.filter((x) => x.modelId.startsWith('ac-private-')).map((x) => x.id)
check('准备 1 公开 + 2 私有模型', Boolean(publicModel?.id) && privateIds.length === 2, JSON.stringify(items.map((x) => x.modelId)))

// ---------- AC-S1 初始渠道级授权视图 ----------
let view = await api(adminToken, `/api/ai/channel/${chId}/authorization`)
check('AC-S1 渠道级授权初始视图（统计+空并集）',
  view.status === 200 && view.body?.modelCount === 3 && view.body?.privateModelCount === 2 && view.body?.publicModelCount === 1 && (view.body?.items ?? []).length === 0,
  JSON.stringify(view.body))

// ---------- AC-S2 渠道级批量授权（增量语义） ----------
const put1 = await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [teamAId, teamBId], revokeTeamIds: [] })
check('AC-S2 渠道级批量授权两个团队', put1.status === 200, JSON.stringify(put1.body))

const authA = await api(adminToken, `/api/ai/model/${privateIds[0]}/authorization`)
const authB = await api(adminToken, `/api/ai/model/${privateIds[1]}/authorization`)
const idsOf = (r) => (r.body?.items ?? []).map((x) => Number(x.teamId)).sort((a, b) => a - b)
check('AC-S2 私有模型授权补齐为 {A,B}', idsOf(authA).length === 2 && idsOf(authB).length === 2, `A=${JSON.stringify(idsOf(authA))} B=${JSON.stringify(idsOf(authB))}`)

// 幂等：重复 grant 已授权团队，无新增不报错
await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [teamAId], revokeTeamIds: [] })
const authA2 = await api(adminToken, `/api/ai/model/${privateIds[0]}/authorization`)
check('AC-S2 重复 grant 幂等（仍 {A,B}）', idsOf(authA2).length === 2, JSON.stringify(idsOf(authA2)))

const pubAuth = await api(adminToken, `/api/ai/model/${publicModel.id}/authorization`)
check('AC-S2 公开模型不受影响（仍公开）', pubAuth.body?.isPublic === true, JSON.stringify(pubAuth.body))

// ---------- AC-S3 模型列表回显授权团队名 ----------
const list2 = await api(adminToken, `/api/ai/model?channelId=${chId}`)
const privItem = (list2.body?.items ?? []).find((x) => x.modelId === 'ac-private-a')
const pubItem = (list2.body?.items ?? []).find((x) => x.modelId === 'ac-public-model')
check('AC-S3 私有模型行回显授权团队名', Array.isArray(privItem?.authorizedTeamNames) && privItem.authorizedTeamNames.length === 2, JSON.stringify(privItem?.authorizedTeamNames))
check('AC-S3 公开模型行授权团队名为空', Array.isArray(pubItem?.authorizedTeamNames) && pubItem.authorizedTeamNames.length === 0, JSON.stringify(pubItem?.authorizedTeamNames))

// ---------- AC-S4 团队可用性 ----------
const gwA = await api(ownerA.token, `/api/team/${teamAId}/ai/model/available`)
const gwB = await api(ownerB.token, `/api/team/${teamBId}/ai/model/available`)
const modelIdsOf = (r) => (r.body?.items ?? []).map((x) => x.modelId)
check('AC-S4 团队 A 可用模型含私有模型与公开模型', modelIdsOf(gwA).includes('ac-private-a') && modelIdsOf(gwA).includes('ac-public-model'), JSON.stringify(modelIdsOf(gwA)))
check('AC-S4 团队 B 可用模型含私有模型', modelIdsOf(gwB).includes('ac-private-b'), JSON.stringify(modelIdsOf(gwB)))

// ---------- AC-S5 取消授权（revoke 增量） ----------
await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [], revokeTeamIds: [teamAId] })
const gwA2 = await api(ownerA.token, `/api/team/${teamAId}/ai/model/available`)
const gwB2 = await api(ownerB.token, `/api/team/${teamBId}/ai/model/available`)
check('AC-S5 取消后团队 A 失去私有模型', !modelIdsOf(gwA2).includes('ac-private-a'), JSON.stringify(modelIdsOf(gwA2)))
check('AC-S5 取消后团队 B 保留私有模型', modelIdsOf(gwB2).includes('ac-private-b'), JSON.stringify(modelIdsOf(gwB2)))
view = await api(adminToken, `/api/ai/channel/${chId}/authorization`)
check('AC-S5 渠道级并集回显为 {B}', (view.body?.items ?? []).length === 1 && Number(view.body.items[0].teamId) === teamBId, JSON.stringify(view.body?.items))

// revoke 未授权的团队：跳过不报错（幂等）
const revokeAgain = await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [], revokeTeamIds: [teamAId] })
check('AC-S5 重复 revoke 幂等 200', revokeAgain.status === 200)

// grant 与 revoke 并发提交（同团队以 grant 优先，不被取消）
const both = await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [teamAId], revokeTeamIds: [teamAId] })
const gwA3 = await api(ownerA.token, `/api/team/${teamAId}/ai/model/available`)
check('AC-S5 同团队 grant 优先于 revoke', both.status === 200 && modelIdsOf(gwA3).includes('ac-private-a'), JSON.stringify(modelIdsOf(gwA3)))

// ---------- AC-S6 清空授权（revoke 全部） ----------
const putEmpty = await api(adminToken, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [], revokeTeamIds: [teamAId, teamBId] })
check('AC-S6 清空授权 200', putEmpty.status === 200)
const gwB3 = await api(ownerB.token, `/api/team/${teamBId}/ai/model/available`)
check('AC-S6 清空后团队 B 失去私有模型', !modelIdsOf(gwB3).includes('ac-private-b'), JSON.stringify(modelIdsOf(gwB3)))

// ---------- AC-S7 门禁 ----------
const forbidden = await api(ownerA.token, `/api/ai/channel/${chId}/authorization`, 'PUT', { grantTeamIds: [], revokeTeamIds: [] })
check('AC-S7 非 admin 渠道级授权 403', forbidden.status === 403, `${forbidden.status}`)

// ---------- AC-S8 团队自有渠道无渠道授权概念 ----------
const teamCh = await api(ownerA.token, `/api/team/${teamAId}/ai/channel`, 'POST', {
  providerKey: 'openai', name: `ac-team-渠道-${ts}`, protocolFamily: 'openAIChatCompletions', baseUrl: 'http://127.0.0.1:1/v1', enabled: true,
})
const teamChList = await api(ownerA.token, `/api/team/${teamAId}/ai/channel`)
const teamChId = (teamChList.body?.items ?? []).find((x) => x.name === `ac-team-渠道-${ts}`)?.id
check('AC-S8 准备团队自有渠道', teamCh.status === 200 && Boolean(teamChId))
const teamChAuth = await api(adminToken, `/api/ai/channel/${teamChId}/authorization`, 'PUT', { grantTeamIds: [teamAId], revokeTeamIds: [] })
check('AC-S8 团队自有渠道调用渠道级授权 400', teamChAuth.status === 400, `${teamChAuth.status}`)

// ---------- 清理 ----------
await api(adminToken, `/api/ai/channel/${chId}`, 'DELETE')
await api(ownerA.token, `/api/team/${teamAId}/ai/channel/${teamChId}`, 'DELETE')
await api(adminToken, `/api/admin/team/${teamAId}/disable`, 'PUT', { isDisable: true })
await api(adminToken, `/api/admin/team/${teamBId}/disable`, 'PUT', { isDisable: true })

console.log(`\n结果: PASS=${pass} FAIL=${fail}`)
process.exit(fail > 0 ? 1 : 0)
