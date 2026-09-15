// 外部应用接入 · 外部 token E2E（场景 @EA-Sn）
// 用法：node local-dev/external-app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/external_app.sql 建表（新库 EnsureCreated 自动建）。
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

async function mkExternalApp(owner, teamId, name, isAuth) {
  const c = await api('POST', '/api/app', { token: owner.token, body: { teamId, name, description: 'e2e', appType: 'agent', isExternal: true, isAuth } })
  if (c.status !== 200) throw new Error(`创建外部应用失败: ${c.status} ${c.text.slice(0, 160)}`)
  const appId = String(c.json.value)
  const p = await api('POST', `/api/app/${appId}/publish`, { token: owner.token })
  if (p.status !== 200) throw new Error(`发布外部应用失败: ${p.status}`)
  return appId
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('eo')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'external-team-' + TS } })).json.value)

  // 准备：外部应用（is_auth=true）、外部应用（is_auth=false）、范围外外部应用、内部应用
  const APP_AUTH = await mkExternalApp(owner, TID, '外部·需授权' + TS.slice(-4), true)
  const APP_FREE = await mkExternalApp(owner, TID, '外部·免授权' + TS.slice(-4), false)
  const APP_OUT = await mkExternalApp(owner, TID, '外部·未授权' + TS.slice(-4), true)
  const APP_IN = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '内部应用' + TS.slice(-4), appType: 'agent' } })).json.value)
  // 其他团队的外部应用（用于跨团队 403 断言）
  const TID2 = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'external-team2-' + TS } })).json.value)
  const APP_OTHER_TEAM = await mkExternalApp(owner, TID2, '外部·他团队' + TS.slice(-4), true)

  // EA-01 未携带 token 访问外部接口
  check('EA-01 无 token 访问 /api/external/app/list 401', (await api('GET', '/api/external/app/list')).status === 401)

  // EA-02 内部用户 token 访问外部接口（audience 隔离：内部 aud 不被外部 scheme 接受）
  check('EA-02 内部用户 token 访问外部接口 401', (await api('GET', '/api/external/app/list', { token: owner.token })).status === 401)

  // EA-03 创建应用接入（团队级授权，无需绑定应用列表）
  const acc = await api('POST', '/api/access-app', { token: owner.token, body: { teamId: TID, name: 'e2e接入', description: 'external e2e' } })
  check('EA-03 创建应用接入 200 且返回 key', acc.status === 200 && typeof acc.json?.key === 'string' && acc.json.key.startsWith('moai-ac-'), `${acc.status} ${acc.text.slice(0, 160)}`)
  const KEY = acc.json?.key
  const ACC_ID = acc.json?.accessAppId

  // EA-04 错误 key 换 token
  check('EA-04 错误 key 换 token 401', (await api('POST', '/api/external/token', { body: { accessAppKey: 'moai-ac-notexist000000000000000000' } })).status === 401)

  // EA-05 应用 token：仅 key
  const appTok = await api('POST', '/api/external/token', { body: { accessAppKey: KEY } })
  check('EA-05a 应用 token 签发 200 tokenType=app', appTok.status === 200 && appTok.json?.tokenType === 'app' && typeof appTok.json?.accessToken === 'string', `${appTok.status} ${appTok.text.slice(0, 160)}`)
  check('EA-05b expiresIn>0 且无 externalId', appTok.json?.expiresIn > 0 && appTok.json?.externalId == null)

  // EA-06 应用 token 访问外部接口：团队级授权，返回归属团队全部外部应用
  const appList = await api('GET', '/api/external/app/list', { token: appTok.json.accessToken })
  const teamAppIds = (appList.json?.items ?? []).map((x) => x.appId)
  check('EA-06 应用 token 查团队外部应用列表 200 含 3 个外部应用', appList.status === 200 && teamAppIds.includes(APP_AUTH) && teamAppIds.includes(APP_FREE) && teamAppIds.includes(APP_OUT), `${appList.status} ${appList.text.slice(0, 200)}`)
  check('EA-06b 团队外部应用列表不含内部应用', !teamAppIds.includes(APP_IN), JSON.stringify(teamAppIds))

  // EA-07 外部 token 不能访问内部接口（audience 隔离）
  check('EA-07 外部 token 访问内部 /api/app/list 401', (await api('GET', `/api/app/list?teamId=${TID}`, { token: appTok.json.accessToken })).status === 401)

  // EA-08 用户 token：key + appId + externalUserId
  const EXT_UID = 'erp-user-' + TS
  const userTok = await api('POST', '/api/external/token', { body: { accessAppKey: KEY, appId: APP_AUTH, externalUserId: EXT_UID, nickname: '小明' } })
  check('EA-08a 用户 token 签发 200 tokenType=user', userTok.status === 200 && userTok.json?.tokenType === 'user' && /^[0-9]+$/.test(String(userTok.json?.externalId)), `${userTok.status} ${userTok.text.slice(0, 160)}`)
  check('EA-08b 返回 externalUserId', userTok.json?.externalUserId === EXT_UID)

  // EA-09 同 key + 同 externalUserId 复用同一外部身份
  const userTok2 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY, appId: APP_AUTH, externalUserId: EXT_UID } })
  check('EA-09 重复申请复用 externalId', userTok2.status === 200 && userTok2.json?.externalId === userTok.json.externalId, `${userTok2.status}`)

  // EA-10 用户 token 查应用列表：团队级授权，含归属团队全部外部应用
  const userList = await api('GET', '/api/external/app/list', { token: userTok.json.accessToken })
  const userListIds = (userList.json?.items ?? []).map((x) => x.appId)
  check('EA-10 用户 token 列表含团队外部应用', userList.status === 200 && userListIds.includes(APP_AUTH) && userListIds.includes(APP_OUT), `${userList.status} ${userList.text.slice(0, 200)}`)

  // EA-11 用户 token 申请其他团队的应用（团队级授权拒绝）
  const outOfScope = await api('POST', '/api/external/token', { body: { accessAppKey: KEY, appId: APP_OTHER_TEAM, externalUserId: EXT_UID } })
  check('EA-11 跨团队应用 403', outOfScope.status === 403, `${outOfScope.status} ${outOfScope.text.slice(0, 160)}`)

  // EA-12 参数组合校验
  check('EA-12a key+appId 缺 externalUserId 400', (await api('POST', '/api/external/token', { body: { accessAppKey: KEY, appId: APP_AUTH } })).status === 400)
  check('EA-12b key+externalUserId 缺 appId 400', (await api('POST', '/api/external/token', { body: { accessAppKey: KEY, externalUserId: 'x' } })).status === 400)
  check('EA-12c 无 key 无 appId 400', (await api('POST', '/api/external/token', { body: { externalUserId: 'x' } })).status === 400)

  // EA-13 匿名方式访问 is_auth=true 应用 → 403
  check('EA-13 免 key 匿名换 is_auth=true 应用 403', (await api('POST', '/api/external/token', { body: { appId: APP_AUTH } })).status === 403)

  // EA-14 匿名方式换 is_auth=false 应用 → 生成临时外部身份
  const anonTok = await api('POST', '/api/external/token', { body: { appId: APP_FREE } })
  check('EA-14a 匿名 token 签发 200', anonTok.status === 200 && anonTok.json?.tokenType === 'user' && /^[0-9]+$/.test(String(anonTok.json?.externalId)), `${anonTok.status} ${anonTok.text.slice(0, 160)}`)
  check('EA-14b 匿名 externalUserId 为临时值', typeof anonTok.json?.externalUserId === 'string' && anonTok.json.externalUserId.startsWith('ext-'))

  // EA-15 refresh token 刷新用户 token（旋转）
  const refreshed = await api('POST', '/api/external/token/refresh', { body: { refreshToken: userTok.json.refreshToken } })
  check('EA-15a 刷新 200 返回新 token 对', refreshed.status === 200 && typeof refreshed.json?.accessToken === 'string' && typeof refreshed.json?.refreshToken === 'string', `${refreshed.status} ${refreshed.text.slice(0, 160)}`)
  check('EA-15b 刷新后身份一致', refreshed.json?.externalId === userTok.json.externalId && refreshed.json?.tokenType === 'user')

  // EA-16 刷新后的 access token 可用
  check('EA-16 刷新后 token 访问外部接口 200', (await api('GET', '/api/external/app/list', { token: refreshed.json.accessToken })).status === 200)

  // EA-17 access token 冒充 refresh token → 401
  check('EA-17 access token 冒充 refresh 401', (await api('POST', '/api/external/token/refresh', { body: { refreshToken: appTok.json.accessToken } })).status === 401)

  // EA-18 刷新应用 token（团队级授权，签发归属团队资源访问范围）
  const refreshedApp = await api('POST', '/api/external/token/refresh', { body: { refreshToken: appTok.json.refreshToken } })
  check('EA-18 应用 token 刷新 200 tokenType=app', refreshedApp.status === 200 && refreshedApp.json?.tokenType === 'app', `${refreshedApp.status} ${refreshedApp.text.slice(0, 160)}`)

  // EA-19 删除接入后 refresh 吊销（401），其应用 token / 用户 token 刷新均失效
  const del = await api('DELETE', `/api/access-app/${ACC_ID}`, { token: owner.token })
  check('EA-19a 删除应用接入 200', del.status === 200, `${del.status}`)
  check('EA-19b 删除后应用 token 刷新 401', (await api('POST', '/api/external/token/refresh', { body: { refreshToken: appTok.json.refreshToken } })).status === 401)
  check('EA-19c 删除后用户 token 刷新 401', (await api('POST', '/api/external/token/refresh', { body: { refreshToken: userTok.json.refreshToken } })).status === 401)
  check('EA-19d 删除后 key 再换 token 401', (await api('POST', '/api/external/token', { body: { accessAppKey: KEY } })).status === 401)

  // ===== 外部会话与对话端点（@EA-S9/S10）：重新建接入并换新 token =====
  const acc2 = await api('POST', '/api/access-app', { token: owner.token, body: { teamId: TID, name: 'e2e接入2', description: 'session e2e' } })
  const KEY2 = acc2.json?.key

  // EA-20 用户 token 建外部会话
  const EXT_UID2 = 'erp-user-2-' + TS
  const userTok3 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY2, appId: APP_AUTH, externalUserId: EXT_UID2 } })
  const appTok2 = await api('POST', '/api/external/token', { body: { accessAppKey: KEY2 } })
  check('EA-20a 用户 token 建会话 200', userTok3.status === 200 && (await api('POST', `/api/external/agent/${APP_AUTH}/session`, { token: userTok3.json.accessToken, body: { title: '外部会话' } })).status === 200)
  const session = await api('POST', `/api/external/agent/${APP_AUTH}/session`, { token: userTok3.json.accessToken, body: {} })
  const SESSION_ID = session.json?.value
  check('EA-20b 建会话返回 Guid', session.status === 200 && isGuid(SESSION_ID), `${session.status} ${session.text.slice(0, 140)}`)

  // EA-21 会话列表与消息
  const list = await api('GET', `/api/external/agent/${APP_AUTH}/session/list`, { token: userTok3.json.accessToken })
  check('EA-21a 会话列表 200 含新建会话', list.status === 200 && (list.json?.items ?? []).some((x) => x.sessionId === SESSION_ID), `${list.status} ${list.text.slice(0, 160)}`)
  const msgs = await api('GET', `/api/external/session/${SESSION_ID}/messages`, { token: userTok3.json.accessToken })
  check('EA-21b 会话消息 200 空列表', msgs.status === 200 && Array.isArray(msgs.json?.items) && msgs.json.items.length === 0, `${msgs.status} ${msgs.text.slice(0, 140)}`)

  // EA-22 会话端点鉴权与范围
  check('EA-22a 应用 token 建会话 403', (await api('POST', `/api/external/agent/${APP_AUTH}/session`, { token: appTok2.json.accessToken, body: {} })).status === 403)
  check('EA-22b 跨团队应用建会话 403', (await api('POST', `/api/external/agent/${APP_OTHER_TEAM}/session`, { token: userTok3.json.accessToken, body: {} })).status === 403)
  check('EA-22b2 同团队其他应用建会话 200（团队级授权）', (await api('POST', `/api/external/agent/${APP_OUT}/session`, { token: userTok3.json.accessToken, body: {} })).status === 200)
  check('EA-22c 无 token 建会话 401', (await api('POST', `/api/external/agent/${APP_AUTH}/session`, { body: {} })).status === 401)
  check('EA-22d 内部用户 token 建会话 401', (await api('POST', `/api/external/agent/${APP_AUTH}/session`, { token: owner.token, body: {} })).status === 401)

  // EA-23 其他外部用户不能读他人会话
  const otherTok = await api('POST', '/api/external/token', { body: { accessAppKey: KEY2, appId: APP_AUTH, externalUserId: 'erp-other-' + TS } })
  check('EA-23 他人查会话消息 404', otherTok.status === 200 && (await api('GET', `/api/external/session/${SESSION_ID}/messages`, { token: otherTok.json.accessToken })).status === 404)

  // EA-23b 他团队 token 不能读本团队会话（团队级隔离，QueryExternalSessionMessagesCommandHandler 404 兜底）
  const accOtherTeam = await api('POST', '/api/access-app', { token: owner.token, body: { teamId: TID2, name: 'e2e接入他团队', description: 'cross-team e2e' } })
  const otherTeamTok = await api('POST', '/api/external/token', { body: { accessAppKey: accOtherTeam.json?.key, appId: APP_OTHER_TEAM, externalUserId: EXT_UID2 } })
  check('EA-23b 他团队 token 查会话消息 404', otherTeamTok.status === 200 && (await api('GET', `/api/external/session/${SESSION_ID}/messages`, { token: otherTeamTok.json.accessToken })).status === 404, `${otherTeamTok.status}`)

  // EA-24 对话端点（AG-UI SSE）鉴权与范围
  check('EA-24a 无 token 对话 401', (await api('POST', `/api/external/agent/${APP_AUTH}/chat`, { body: {} })).status === 401)
  check('EA-24b 无效 token 对话 401', (await api('POST', `/api/external/agent/${APP_AUTH}/chat`, { token: 'invalid-token', body: {} })).status === 401)
  check('EA-24c 内部用户 token 对话 401', (await api('POST', `/api/external/agent/${APP_AUTH}/chat`, { token: owner.token, body: {} })).status === 401)
  check('EA-24d 跨团队应用对话 403', (await api('POST', `/api/external/agent/${APP_OTHER_TEAM}/chat`, { token: userTok3.json.accessToken, body: {} })).status === 403)

  // EA-25 真实对话链路：AG-UI 协议体，验证外部用户 id 通过派发器会话归属校验
  // （应用未配置模型时派发器以 SSE 文本返回装配错误，HTTP 200 即说明归属校验与派发链路通）
  const chat = await api('POST', `/api/external/agent/${APP_AUTH}/chat`, {
    token: userTok3.json.accessToken,
    body: { threadId: SESSION_ID, runId: 'run-1', messages: [{ id: 'm1', role: 'user', content: '你好' }] },
  })
  check('EA-25 外部对话 SSE 请求 200（归属校验通过）', chat.status === 200, `${chat.status} ${chat.text.slice(0, 200)}`)

  // ===== 访问点配置（4a） =====
  // EA-26 内部配置读写
  check('EA-26a 未保存时返回默认配置', (await api('GET', `/api/app/${APP_AUTH}/access-point`, { token: owner.token })).json?.panelWidth === 380)
  const saved = await api('PUT', `/api/app/${APP_AUTH}/access-point`, {
    token: owner.token,
    body: { title: '在线客服', subtitle: '欢迎咨询', placeholder: '请输入', primaryColor: '#1677ff', position: 'bottomLeft', launcherText: '咨询', panelWidth: 420, panelHeight: 600, defaultOpen: false, enabled: true },
  })
  check('EA-26b 保存访问点配置 200', saved.status === 200, `${saved.status} ${saved.text.slice(0, 140)}`)
  check('EA-26c 非法颜色 400', (await api('PUT', `/api/app/${APP_AUTH}/access-point`, { token: owner.token, body: { primaryColor: 'red', position: 'bottomRight', panelWidth: 380, panelHeight: 560, enabled: true } })).status === 400)
  check('EA-26d 面板宽度越界 400', (await api('PUT', `/api/app/${APP_AUTH}/access-point`, { token: owner.token, body: { position: 'bottomRight', panelWidth: 100, panelHeight: 560, enabled: true } })).status === 400)

  // EA-27 公开配置（匿名）
  const pub = await api('GET', `/api/external/app/${APP_AUTH}/access-point`)
  check('EA-27a 公开配置 200 且生效', pub.status === 200 && pub.json?.title === '在线客服' && pub.json?.position === 'bottomLeft' && pub.json?.panelWidth === 420, `${pub.status} ${pub.text.slice(0, 160)}`)
  check('EA-27b 公开配置含 isAuth/enabled/appName', pub.json?.isAuth === true && pub.json?.enabled === true && typeof pub.json?.appName === 'string')
  const internalAppId = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '内部应用B' + TS.slice(-3), appType: 'agent' } })).json.value)
  check('EA-27d 内部应用配置访问点 400', (await api('PUT', `/api/app/${internalAppId}/access-point`, { token: owner.token, body: { position: 'bottomRight', panelWidth: 380, panelHeight: 560, enabled: true } })).status === 400)
  check('EA-27e 不存在应用公开配置 404', (await api('GET', `/api/external/app/${crypto.randomUUID()}/access-point`)).status === 404)

  // EA-28 悬浮组件静态托管
  const widget = await fetch(`${BASE}/embed/moai-widget.js`)
  check('EA-28 组件脚本 /embed/moai-widget.js 200', widget.status === 200 && (widget.headers.get('content-type') ?? '').includes('javascript'), `${widget.status}`)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => {
  console.error('E2E 执行失败:', e.message)
  process.exit(1)
})
