// 团队模型网关 E2E 冒烟脚本（不依赖真实上游模型渠道）
// 用法: node local-dev/gateway-e2e.mjs [baseUrl]   （需后端运行中，默认 http://127.0.0.1:5210）
// 覆盖：密钥管理 API、/api/aigateway/{teamId}/v1/models 两种鉴权头、路由团队校验、禁用/删除即时失效、模型未授权 404、成员权限矩阵
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
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

async function login(userName, password) {
  const r = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, password: enc(password) }),
  })
  const body = await r.json()
  return body?.accessToken ?? null
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

async function register(userName, password) {
  const r = await fetch(`${BASE}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      userName,
      email: `${userName}@test.local`,
      nickName: userName,
      phone: `139${String(Math.floor(Math.random() * 1e8)).padStart(8, '0')}`,
      password: enc(password),
    }),
  })
  return r.status
}

// ---------- 准备 ----------
const ts = Date.now().toString().slice(-8)
const adminToken = await login('admin', 'abcd123456')
check('admin 登录', Boolean(adminToken))

check('注册成员 bob', (await register(`gwbob${ts}`, 'bob12345678')) === 200)
const bobToken = await login(`gwbob${ts}`, 'bob12345678')
check('bob 登录', Boolean(bobToken))

const users = await api(adminToken, `/api/usermanage/users?pageNo=1&pageSize=50&searchText=gwbob${ts}`)
const bobUser = (users.body?.items ?? []).find((x) => x.userName === `gwbob${ts}`)
check('查到 bob 用户 id', Boolean(bobUser?.id), JSON.stringify(users.body?.items?.slice(0, 2)))

// ---------- 团队与密钥管理 ----------
const teamName = `gw-e2e-${ts}`
const team = await api(adminToken, '/api/team', 'POST', { name: teamName })
const teamId = team.body?.value
check('admin 创建团队', team.status === 200 && Number(teamId) > 0, JSON.stringify(team.body))

const created = await api(adminToken, `/api/team/${teamId}/gateway/keys`, 'POST', { name: 'e2e 密钥' })
const secret = created.body?.secret
const keyId = created.body?.apiKeyId
check('创建 API Key 返回原文', created.status === 200 && typeof secret === 'string' && secret.startsWith('moai-'), JSON.stringify(created.body))
check('创建 API Key 返回前缀', created.body?.keyPrefix === secret.slice(0, 13))

const keys = await api(adminToken, `/api/team/${teamId}/gateway/keys`)
check('密钥列表不泄露原文', keys.status === 200 && keys.body?.items?.some((x) => x.id === keyId && x.keyPrefix && !JSON.stringify(x).includes(secret)))

// ---------- 网关鉴权 ----------
const gwBase = `${BASE}/api/aigateway/${teamId}/v1`
const noKey = await fetch(`${gwBase}/models`)
check('无密钥访问 /api/aigateway/{teamId}/v1/models 返回 401', noKey.status === 401)

const badKey = await fetch(`${gwBase}/models`, { headers: { Authorization: 'Bearer moai-invalidinvalidinvalidinvalidinvalid' } })
check('伪造密钥返回 401', badKey.status === 401)

const okAuth = await fetch(`${gwBase}/models`, { headers: { Authorization: `Bearer ${secret}` } })
const okBody = await okAuth.json()
check('Bearer 密钥访问 models 返回 200', okAuth.status === 200 && okBody?.object === 'list' && Array.isArray(okBody.data))

const okHeader = await fetch(`${gwBase}/models`, { headers: { 'x-api-key': secret } })
check('x-api-key 密钥访问 models 返回 200', okHeader.status === 200)

// 路由 teamId 严格校验：用错误的团队 id 访问，返回 403
const wrongTeam = await fetch(`${BASE}/api/aigateway/${teamId + 1}/v1/models`, { headers: { Authorization: `Bearer ${secret}` } })
check('路由团队 id 与密钥不一致返回 403', wrongTeam.status === 403)

const chat404 = await fetch(`${gwBase}/chat/completions`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${secret}` },
  body: JSON.stringify({ model: 'gw-e2e-不存在', messages: [{ role: 'user', content: 'hi' }] }),
})
const chat404Body = await chat404.json()
check('未授权模型返回 404 + OpenAI 错误信封', chat404.status === 404 && chat404Body?.error?.code === 'model_not_found', JSON.stringify(chat404Body))

const msg404 = await fetch(`${gwBase}/messages`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'x-api-key': secret },
  body: JSON.stringify({ model: 'gw-e2e-不存在', max_tokens: 64, messages: [{ role: 'user', content: 'hi' }] }),
})
const msg404Body = await msg404.json()
check('messages 协议未授权模型返回 Anthropic 错误信封', msg404.status === 404 && msg404Body?.type === 'error' && msg404Body?.error?.type === 'not_found_error', JSON.stringify(msg404Body))

// ---------- 禁用/删除即时失效 ----------
await api(adminToken, `/api/team/${teamId}/gateway/keys/${keyId}`, 'PUT', { isDisable: true })
const disabled = await fetch(`${gwBase}/models`, { headers: { Authorization: `Bearer ${secret}` } })
check('禁用后密钥立即失效(401)', disabled.status === 401)

await api(adminToken, `/api/team/${teamId}/gateway/keys/${keyId}`, 'PUT', { isDisable: false })
const reEnabled = await fetch(`${gwBase}/models`, { headers: { Authorization: `Bearer ${secret}` } })
check('重新启用后恢复(200)', reEnabled.status === 200)

// ---------- 成员权限矩阵 ----------
const addBob = await api(adminToken, `/api/team/${teamId}/users`, 'POST', { userId: Number(bobUser.id), role: 0 })
check('把 bob 加入团队', addBob.status === 200, JSON.stringify(addBob.body))

const bobModels = await api(bobToken, `/api/team/${teamId}/gateway/models`)
check('成员可查看可用模型', bobModels.status === 200 && Array.isArray(bobModels.body?.items))

const bobKeys = await api(bobToken, `/api/team/${teamId}/gateway/keys`)
check('普通成员不可管理密钥(403)', bobKeys.status === 403)

// ---------- 清理 ----------
const delKey = await api(adminToken, `/api/team/${teamId}/gateway/keys/${keyId}`, 'DELETE')
check('删除密钥', delKey.status === 200)
const deleted = await fetch(`${gwBase}/models`, { headers: { Authorization: `Bearer ${secret}` } })
check('删除后密钥失效(401)', deleted.status === 401)

// 团队不可解散：清理改为管理员禁用团队
const disable = await api(adminToken, `/api/admin/team/${teamId}/disable`, 'PUT', { isDisable: true })
check('禁用团队（清理）', disable.status === 200)

console.log(`\n结果: ${pass} passed, ${fail} failed`)
process.exit(fail > 0 ? 1 : 0)
