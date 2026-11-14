// 内部应用访问点身份穿透 · E2E（场景 @IA-Sn）
// 覆盖：隐藏连接器（is_visible）登录列表排除/管理端可见/个人中心绑定列表排除 /
//       用户 upsert 携带 oauth 绑定（幂等复用/已绑他人 409/同渠道换绑 409）/
//       颁发端点 POST /api/subsys/system/app-tokens（未绑定自动建户/未知渠道 404/多匹配 409/禁用 403/
//       外部应用 400/未发布 403/访问点关闭 403/成功换发）/
//       访问用户 token 会话与 AG-UI 对话全链路（会话落真实 userId）+ 站内会话列表合并（聊天记录对上）/
//       refresh 轮换与禁用吊销 / token 绑定单应用（跨应用 403/跨应用会话 404）/ 访问点关闭即拒 / audience 隔离。
// 前置：后端运行中；种子账号 admin / abcd123456（root）。零真实外部依赖（飞书连接器假凭证、桩模型渠道）。
// 用法：node local-dev/internal-app-access-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 IA_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.IA_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0
let FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) {
    PASS++
    console.log(`PASS | ${name}`)
  } else {
    FAIL++
    console.log(`FAIL | ${name}${detail ? ' — ' + detail : ''}`)
  }
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

// 密钥回显（RSA+AES 信封，与 system-access-app-e2e 同实现）
async function revealSecret(rootToken, id) {
  const { publicEncrypt, createPublicKey, createDecipheriv, randomBytes } = await import('node:crypto')
  const pub = createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  const envelopeKey = randomBytes(32)
  const encryptedKey = publicEncrypt({ key: pub, padding: 1 }, Buffer.from(envelopeKey.toString('base64'), 'utf8')).toString('base64')
  const r = await api('POST', `/api/subsystem/${id}/reveal-secret`, { token: rootToken, body: { encryptedKey } })
  if (r.status !== 200) throw new Error(`reveal-secret 失败: ${r.status} ${r.text.slice(0, 160)}`)
  const iv = Buffer.from(r.json.iv, 'base64')
  const payload = Buffer.from(r.json.data, 'base64')
  const decipher = createDecipheriv('aes-256-gcm', envelopeKey, iv)
  decipher.setAuthTag(payload.subarray(-16))
  return Buffer.concat([decipher.update(payload.subarray(0, -16)), decipher.final()]).toString('utf8')
}

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)
const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

const si = await (await fetch(BASE + '/api/common/serverinfo')).json()
RSA_KEY = si.rsaPublic

const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
if (rootLogin.status !== 200) {
  console.error('root 登录失败，终止:', rootLogin.status, rootLogin.text.slice(0, 200))
  process.exit(1)
}
const root = rootLogin.json.accessToken

async function mkuser(p) {
  const name = uname(p)
  await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

const owner = await mkuser('ia')
const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'ia-team-' + TS } })).json.value)

// 应用接入（系统级 moai-sa-）：内网网站持有的接入凭据
const created = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'app', name: `内网门户 ${TS}`, description: 'internal-app-access e2e' } })
const SA_KEY = created.json?.key
const SA_ID = created.json?.id
const SA_SECRET = await revealSecret(root, SA_ID)

// IA-S1 机器 token 换取
const mt = await api('POST', '/api/subsys/token', { body: { appId: SA_KEY, secret: SA_SECRET } })
check('IA-S1a 机器 token 换取 200', mt.status === 200 && typeof mt.json?.accessToken === 'string', `${mt.status} ${mt.text.slice(0, 140)}`)
const machine = mt.json?.accessToken

// 系统级子系统可以使用系统资源协作面，但不能进行内部应用访问点身份穿透
const subCreated = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: `ia-sub-${TS}`, name: `身份穿透禁止子系统-${TS}`, teamScope: 'allTeams' } })
const SUB_KEY = subCreated.json?.key
const SUB_SECRET = await revealSecret(root, subCreated.json?.id)
const subTokenResult = await api('POST', '/api/subsys/token', { body: { appId: SUB_KEY, secret: SUB_SECRET } })
const subsystemMachine = subTokenResult.json?.accessToken

// IA-S2 颁发端点门禁：无凭据 401；团队级 moai-ac- key 走错面 401
check('IA-S2a 无凭据调颁发端点 401', (await api('POST', '/api/subsys/system/app-tokens', { body: { appId: crypto.randomUUID(), oauthProviderId: crypto.randomUUID(), oauthSub: 'x' } })).status === 401)
const accTeam = await api('POST', '/api/access-app', { token: owner.token, body: { teamId: TID, name: 'ia-teamkey-' + TS, description: 'wrong face' } })
check('IA-S2b 团队级 moai-ac- key 调颁发端点 401', (await api('POST', '/api/subsys/system/app-tokens', { token: accTeam.json?.key, body: { appId: crypto.randomUUID(), oauthProviderId: crypto.randomUUID(), oauthSub: 'x' } })).status === 401)

// ===== 隐藏连接器（is_visible）=====
const HID_NAME = `内网SSO映射-${TS}`
const VIS_NAME = `可见渠道-${TS}`
const hidCreate = await api('POST', '/api/oauthconnect/connections', { token: root, body: { name: HID_NAME, provider: 'feishu', key: `hid-${TS}`, secret: 'fake-secret', iconUrl: 'https://example.com/icon.svg', isVisible: false } })
check('IA-S3a 创建隐藏连接器 200（假凭证即可）', hidCreate.status === 200, `${hidCreate.status} ${hidCreate.text.slice(0, 140)}`)
await api('POST', '/api/oauthconnect/connections', { token: root, body: { name: VIS_NAME, provider: 'feishu', key: `vis-${TS}`, secret: 'fake-secret', iconUrl: 'https://example.com/icon.svg' } })

const connList = (await api('GET', '/api/oauthconnect/connections', { token: root })).json?.items ?? []
const hid = connList.find((x) => x.name === HID_NAME)
const vis = connList.find((x) => x.name === VIS_NAME)
check('IA-S3b 管理端列表含隐藏连接器且 isVisible=false', hid != null && hid.isVisible === false, JSON.stringify(hid ?? null).slice(0, 160))
check('IA-S3c 管理端列表可见连接器 isVisible=true', vis != null && vis.isVisible === true)

const loginProviders = ((await api('GET', '/api/auth/oauth_prividers')).json?.items ?? []).map((x) => x.name)
check('IA-S3d 登录页列表不含隐藏连接器', !loginProviders.includes(HID_NAME), JSON.stringify(loginProviders))
check('IA-S3e 登录页列表含可见连接器', loginProviders.includes(VIS_NAME))

const HID_ID = hid?.id

// 旧客户端更新未携带 isVisible：保持原隐藏态（Update 命令 IsVisible 可空，null=不修改）
const hidUpd = await api('PUT', `/api/oauthconnect/connections/${HID_ID}`, {
  token: root,
  body: { name: HID_NAME, provider: 'feishu', key: `hid-${TS}`, secret: '', iconUrl: 'https://example.com/icon.svg' },
})
const hidAfterUpd = ((await api('GET', '/api/oauthconnect/connections', { token: root })).json?.items ?? []).find((x) => x.id === HID_ID)
check('IA-S3f 更新未携带 isVisible 保持隐藏（旧客户端兼容）', hidUpd.status === 200 && hidAfterUpd?.isVisible === false, `${hidUpd.status} ${hidUpd.text.slice(0, 120)}`)

// ===== 用户 upsert 携带 oauth 绑定（系统接口，内网网站自动同步用户并绑定 id）=====
const SYNC_SUB = 'portal-user-' + TS
const SYNC_UNION = 'portal-union-' + TS
const up1 = await api('POST', '/api/subsys/system/users', {
  token: machine,
  body: { userName: uname('sync'), nickName: '门户用户', oauthProviderId: HID_ID, oauthSub: SYNC_SUB, oauthUnionId: SYNC_UNION },
})
check('IA-S4a upsert 创建用户并绑定 200 oauthBound=true', up1.status === 200 && up1.json?.created === true && up1.json?.oauthBound === true && typeof up1.json?.initialPassword === 'string' && up1.json.initialPassword.length >= 8, `${up1.status} ${up1.text.slice(0, 160)}`)
const SYNC_USER_ID = up1.json?.userId
const SYNC_INIT_PWD = up1.json?.initialPassword

const up2 = await api('POST', '/api/subsys/system/users', {
  token: machine,
  body: { userName: (await api('GET', `/api/subsys/system/users/${SYNC_USER_ID}`, { token: machine })).json?.userName, nickName: '门户用户改', oauthProviderId: HID_ID, oauthSub: SYNC_SUB, oauthUnionId: SYNC_UNION },
})
check('IA-S4b 幂等重推复用绑定 created=false oauthBound=true', up2.status === 200 && up2.json?.created === false && up2.json?.oauthBound === true && up2.json?.userId === SYNC_USER_ID, `${up2.status} ${up2.text.slice(0, 160)}`)

// appid 直连双轨：同一接口可用 moai-sa- key 直连调用
const upDirect = await api('GET', `/api/subsys/system/users/${SYNC_USER_ID}`, { token: SA_KEY })
check('IA-S4c appid 直连双轨读用户 200', upDirect.status === 200 && String(upDirect.json?.userId) === String(SYNC_USER_ID), `${upDirect.status}`)

// 绑定冲突
const uOther = uname('other')
await api('POST', '/api/auth/register', { body: { userName: uOther, email: `${uOther}@test.local`, nickName: uOther, phone: phone(), password: rsa('Test1234') } })
check('IA-S5a 同 (渠道,sub) 绑给他人 409', (await api('POST', '/api/subsys/system/users', { token: machine, body: { userName: uOther, oauthProviderId: HID_ID, oauthSub: SYNC_SUB } })).status === 409)
check('IA-S5b 同用户同渠道换 sub 409（拒绝覆盖）', (await api('POST', '/api/subsys/system/users', { token: machine, body: { userName: (await api('GET', `/api/subsys/system/users/${SYNC_USER_ID}`, { token: machine })).json?.userName, oauthProviderId: HID_ID, oauthSub: SYNC_SUB + '-b' } })).status === 409)
check('IA-S5c 未知渠道 400', (await api('POST', '/api/subsys/system/users', { token: machine, body: { userName: uname('nx'), oauthProviderId: crypto.randomUUID(), oauthSub: 'x' } })).status === 400)
check('IA-S5d 缺 oauthSub 400', (await api('POST', '/api/subsys/system/users', { token: machine, body: { userName: uname('nx2'), oauthProviderId: HID_ID } })).status === 400)

// ===== 内部应用与访问点 =====
// 桩模型渠道（配置保存强校验需要 modelId；对话派发装配失败返回 SSE 文本但 HTTP 200）
const eaAdminToken = root
let STUB_MODEL_ID = ''
{
  const chName = `ia-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', { token: eaAdminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: 'http://127.0.0.1:9/v1', apiKey: 'ia-stub-key', enabled: true, description: 'internal-app-access 桩渠道' } })
  const channels = await api('GET', '/api/ai/channel', { token: eaAdminToken })
  const chId = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const modelName = `ia-stub-chat-${TS}`
  await api('POST', '/api/ai/model', { token: eaAdminToken, body: { channelId: chId, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'internal-app-access 桩模型' }, enabled: true, isPublic: false } })
  const models = await api('GET', `/api/ai/model?channelId=${chId}`, { token: eaAdminToken })
  STUB_MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  const cur = await api('GET', `/api/ai/model/${STUB_MODEL_ID}/authorization`, { token: eaAdminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${STUB_MODEL_ID}/authorization`, { token: eaAdminToken, body: { teamIds } })
}
const cfgBase = { modelId: STUB_MODEL_ID, prompt: '你是内部应用助手', wikiIds: [], plugins: [], workflowApps: [], openingStatement: '', openingStatementEnabled: false, quickInputs: [] }

async function mkInternalApp(name, { publish = true, accessPoint = true } = {}) {
  const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name, description: 'ia e2e', appType: 'agent' } })
  const appId = String(c.json.value)
  await api('PUT', `/api/app/${appId}/agent-config`, { token: owner.token, body: cfgBase })
  if (publish) await api('POST', `/api/app/${appId}/publish`, { token: owner.token })
  if (accessPoint) await api('PUT', `/api/app/${appId}/access-point`, { token: owner.token, body: { title: name, position: 'bottomRight', panelWidth: 380, panelHeight: 560, enabled: true } })
  return appId
}

const APP1 = await mkInternalApp('门户助手A' + TS.slice(-4))
const APP2 = await mkInternalApp('门户助手B' + TS.slice(-4))
const APP_UNPUBLISHED = await mkInternalApp('未发布应用' + TS.slice(-4), { publish: false, accessPoint: false })
const EXT_APP = String((await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '外部应用X' + TS.slice(-4), appType: 'agent', isExternal: true } })).json.value)
await api('POST', `/api/app/${EXT_APP}/publish`, { token: owner.token })

// IA-S6 访问点公开配置：authMode=token + 端点地址下发（API 地址发出来）
const pub1 = await api('GET', `/api/teamres/app/${APP1}/access-point`)
check('IA-S6a 内部应用公开配置 200 authMode=token', pub1.status === 200 && pub1.json?.authMode === 'token', `${pub1.status} ${pub1.text.slice(0, 160)}`)
check('IA-S6b 端点地址下发（chat/sessions/messages/token）', typeof pub1.json?.endpoints?.chat === 'string' && pub1.json.endpoints.chat.includes(`/api/teamres/agent/${APP1}/chat`)
  && typeof pub1.json?.endpoints?.sessions === 'string' && typeof pub1.json?.endpoints?.messages === 'string'
  && pub1.json?.endpoints?.token === `${BASE}/api/subsys/system/app-tokens`, JSON.stringify(pub1.json?.endpoints))
const extPub = await api('GET', `/api/teamres/app/${EXT_APP}/access-point`)
check('IA-S6c 外部应用公开配置 authMode=open 且无颁发端点', extPub.status === 200 && extPub.json?.authMode === 'open' && extPub.json?.endpoints?.token === '')

// ===== 颁发端点：第三方登录 id → uid 换算（未绑定时一次调用自动建户并绑定） =====
const issue = (body, token = machine) => api('POST', '/api/subsys/system/app-tokens', { token, body })

check('IA-S2c 系统级 subsystem 接入可颁发访问用户 token', (await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: SYNC_SUB }, subsystemMachine)).status === 200)

const AUTO_SUB = 'auto-provision-' + TS
const AUTO_UNION = 'auto-union-' + TS
const autoIssue = await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: AUTO_SUB, oauthUnionId: AUTO_UNION, nickName: '自动初始化用户' })
check('IA-S7a 未绑定身份一次调用自动建户并颁发 200', autoIssue.status === 200 && autoIssue.json?.userCreated === true && autoIssue.json?.userId != null && typeof autoIssue.json?.accessToken === 'string', `${autoIssue.status} ${autoIssue.text.slice(0, 180)}`)
const autoUserId = autoIssue.json?.userId
const autoUser = await api('GET', `/api/subsys/system/users/${autoUserId}`, { token: machine })
check('IA-S7b 自动建户已落库并回显用户', autoUser.status === 200 && String(autoUser.json?.userId) === String(autoUserId) && autoUser.json?.nickName === '自动初始化用户', `${autoUser.status} ${autoUser.text.slice(0, 160)}`)
const autoAgain = await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: AUTO_SUB, oauthUnionId: AUTO_UNION })
check('IA-S7c 重复申请幂等复用用户', autoAgain.status === 200 && autoAgain.json?.userCreated === false && String(autoAgain.json?.userId) === String(autoUserId), `${autoAgain.status} ${autoAgain.text.slice(0, 180)}`)
check('IA-S7d 未知渠道 404', (await issue({ appId: APP1, oauthProviderId: crypto.randomUUID(), oauthSub: SYNC_SUB })).status === 404)
check('IA-S7e 参数缺失 400', (await issue({ appId: APP1, oauthProviderId: HID_ID })).status === 400)

// 多匹配：同 unionId 绑到第二个用户后按 unionId 颁发命中两人 → 409
const u2Name = uname('sync2')
const up3 = await api('POST', '/api/subsys/system/users', { token: machine, body: { userName: u2Name, oauthProviderId: HID_ID, oauthSub: SYNC_SUB + '-2', oauthUnionId: SYNC_UNION } })
check('IA-S7f 第二用户同 unionId 绑定 200', up3.status === 200 && up3.json?.oauthBound === true, `${up3.status} ${up3.text.slice(0, 140)}`)
check('IA-S7g unionId 命中多用户 409 拒绝颁发', (await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: 'nobody2-' + TS, oauthUnionId: SYNC_UNION })).status === 409)

// 外部应用 / 未发布 / 访问点关闭
check('IA-S8a 目标为外部应用 400', (await issue({ appId: EXT_APP, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })).status === 400)
check('IA-S8b 应用未发布 403', (await issue({ appId: APP_UNPUBLISHED, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })).status === 403)
const noOrphanSub = 'no-orphan-' + TS
check('IA-S8c 目标未发布时不自动建户', (await issue({ appId: APP_UNPUBLISHED, oauthProviderId: HID_ID, oauthSub: noOrphanSub })).status === 403)
const noOrphanAfter = await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: noOrphanSub })
check('IA-S8d 目标恢复可用后首次申请仍自动建户', noOrphanAfter.status === 200 && noOrphanAfter.json?.userCreated === true, `${noOrphanAfter.status} ${noOrphanAfter.text.slice(0, 160)}`)
const tokApp2 = await issue({ appId: APP2, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })
check('IA-S8e 正常颁发（APP2）200', tokApp2.status === 200 && typeof tokApp2.json?.accessToken === 'string' && tokApp2.json?.userId === SYNC_USER_ID && tokApp2.json?.appId === APP2, `${tokApp2.status} ${tokApp2.text.slice(0, 160)}`)
check('IA-S8f 颁发响应含 userName', tokApp2.json?.userName?.length > 0)

// 访问点关闭即拒（颁发与会话面双侧）
await api('PUT', `/api/app/${APP2}/access-point`, { token: owner.token, body: { title: 'B', position: 'bottomRight', panelWidth: 380, panelHeight: 560, enabled: false } })
check('IA-S9a 访问点关闭后颁发 403', (await issue({ appId: APP2, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })).status === 403)
check('IA-S9b 访问点关闭后会话面 403 access_point_disabled', (await api('POST', `/api/teamres/agent/${APP2}/session`, { token: tokApp2.json.accessToken, body: {} })).status === 403)
await api('PUT', `/api/app/${APP2}/access-point`, { token: owner.token, body: { title: 'B', position: 'bottomRight', panelWidth: 380, panelHeight: 560, enabled: true } })

// 成功颁发 APP1
const tok1 = await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })
check('IA-S10a 颁发 APP1 访问用户 token 200', tok1.status === 200 && typeof tok1.json?.accessToken === 'string' && typeof tok1.json?.refreshToken === 'string' && tok1.json?.expiresIn > 0, `${tok1.status} ${tok1.text.slice(0, 160)}`)
check('IA-S10b 换算出站内用户 id 与用户名', tok1.json?.userId === SYNC_USER_ID && typeof tok1.json?.userName === 'string', JSON.stringify(tok1.json ?? {}).slice(0, 160))
const ACCESS = tok1.json.accessToken
const REFRESH = tok1.json.refreshToken

// ===== 会话与对话全链路（会话落真实 userId）=====
check('IA-S11a 访问用户 token 建会话 200', (await api('POST', `/api/teamres/agent/${APP1}/session`, { token: ACCESS, body: { title: '门户会话' } })).status === 200)
const sess = await api('POST', `/api/teamres/agent/${APP1}/session`, { token: ACCESS, body: {} })
const SESSION1 = sess.json?.value
check('IA-S11b 建会话返回 Guid', sess.status === 200 && isGuid(SESSION1), `${sess.status} ${sess.text.slice(0, 140)}`)
const listExt = await api('GET', `/api/teamres/agent/${APP1}/session/list`, { token: ACCESS })
check('IA-S11c 访问面会话列表含新会话', listExt.status === 200 && (listExt.json?.items ?? []).some((x) => x.sessionId === SESSION1), `${listExt.status} ${listExt.text.slice(0, 160)}`)
const msgs = await api('GET', `/api/teamres/session/${SESSION1}/messages`, { token: ACCESS })
check('IA-S11d 访问面读会话消息 200', msgs.status === 200 && Array.isArray(msgs.json?.items), `${msgs.status}`)

// AG-UI 对话（派发器会话归属校验：会话 CreateUserId=真实 userId，派发链路通即 200）
const chat = await api('POST', `/api/teamres/agent/${APP1}/chat`, {
  token: ACCESS,
  body: { threadId: SESSION1, runId: 'run-ia-1', messages: [{ id: 'm1', role: 'user', content: '你好' }] },
})
check('IA-S12 访问用户 token AG-UI 对话 200（归属校验通过）', chat.status === 200, `${chat.status} ${chat.text.slice(0, 200)}`)

// 站内会话合并（聊天记录对上）：同步用户加入团队后站内列表可见同一会话
const syncLogin = await api('POST', '/api/auth/login', { body: { userName: (await api('GET', `/api/subsys/system/users/${SYNC_USER_ID}`, { token: machine })).json?.userName, password: rsa(SYNC_INIT_PWD) } })
check('IA-S13a 同步用户凭初始密码登录 200', syncLogin.status === 200 && String(syncLogin.json?.userId) === String(SYNC_USER_ID), `${syncLogin.status} ${syncLogin.text.slice(0, 160)}`)
const SYNC_TOKEN = syncLogin.json.accessToken
check('IA-S13b 加入团队 200', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: SYNC_USER_ID, role: 0 } })).status === 200)
const listWeb = await api('GET', `/api/app/${APP1}/session/list`, { token: SYNC_TOKEN })
check('IA-S13c 站内会话列表含访问点会话（聊天记录对上）', listWeb.status === 200 && (listWeb.json?.items ?? []).some((x) => x.sessionId === SESSION1), `${listWeb.status} ${listWeb.text.slice(0, 200)}`)

// 个人中心绑定列表：隐藏连接器绑定不露出（系统托管防误解绑）
const bound = ((await api('GET', '/api/account/bound_accounts', { token: SYNC_TOKEN })).json?.items ?? []).map((x) => x.name)
check('IA-S14 隐藏连接器绑定不在个人中心露出', !bound.includes(HID_NAME), JSON.stringify(bound))

// ===== 隔离与越权 =====
check('IA-S15a 访问用户 token 调站内接口 401（audience 隔离）', (await api('GET', `/api/app/${APP1}/session/list`, { token: ACCESS })).status === 401)
check('IA-S15b 站内 JWT 调访问面 401', (await api('POST', `/api/teamres/agent/${APP1}/session`, { token: owner.token, body: {} })).status === 401)
check('IA-S15c token(APP1) 建 APP2 会话 403（绑定单应用）', (await api('POST', `/api/teamres/agent/${APP2}/session`, { token: ACCESS, body: {} })).status === 403)
const sess2 = await api('POST', `/api/teamres/agent/${APP2}/session`, { token: tokApp2.json.accessToken, body: {} })
const SESSION2 = sess2.json?.value
check('IA-S15d token(APP2) 建 APP2 会话 200', sess2.status === 200 && isGuid(SESSION2), `${sess2.status}`)
check('IA-S15e token(APP1) 读 APP2 会话消息 404（跨应用兜底）', (await api('GET', `/api/teamres/session/${SESSION2}/messages`, { token: ACCESS })).status === 404)
check('IA-S15f 无效 token 对话 401', (await api('POST', `/api/teamres/agent/${APP1}/chat`, { token: 'invalid-token', body: {} })).status === 401)
check('IA-S15g 无 token 建会话 401', (await api('POST', `/api/teamres/agent/${APP1}/session`, { body: {} })).status === 401)

// ===== refresh 轮换与禁用吊销 =====
const ref1 = await api('POST', '/api/teamres/token/refresh', { body: { refreshToken: REFRESH } })
check('IA-S16a 刷新 200 新 token 对', ref1.status === 200 && typeof ref1.json?.accessToken === 'string' && ref1.json?.userId === SYNC_USER_ID, `${ref1.status} ${ref1.text.slice(0, 160)}`)
check('IA-S16b 刷新后 token 可用', (await api('GET', `/api/teamres/agent/${APP1}/session/list`, { token: ref1.json.accessToken })).status === 200)

check('IA-S17a 禁用同步用户 200', (await api('PUT', `/api/subsys/system/users/${SYNC_USER_ID}/disable`, { token: machine, body: { isDisable: true } })).status === 200)
check('IA-S17b 禁用后颁发 403', (await issue({ appId: APP1, oauthProviderId: HID_ID, oauthSub: SYNC_SUB })).status === 403)
check('IA-S17c 禁用后刷新 403（吊销）', (await api('POST', '/api/teamres/token/refresh', { body: { refreshToken: ref1.json.refreshToken } })).status === 403)
check('IA-S17d 禁用后同步用户站内登录 403/401', [401, 403].includes((await api('POST', '/api/auth/login', { body: { userName: (await api('GET', `/api/subsys/system/users/${SYNC_USER_ID}`, { token: machine })).json?.userName, password: rsa(SYNC_INIT_PWD) } })).status))
check('IA-S17e 重新启用 200', (await api('PUT', `/api/subsys/system/users/${SYNC_USER_ID}/disable`, { token: machine, body: { isDisable: false } })).status === 200)
check('IA-S17f 启用后刷新恢复 200', (await api('POST', '/api/teamres/token/refresh', { body: { refreshToken: ref1.json.refreshToken } })).status === 200)

console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
if (FAIL > 0) process.exit(1)
