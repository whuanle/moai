// 系统级接入（子系统+应用接入合并模型）· E2E（场景 @SUB-S1..）
// 覆盖：root 门禁 / 创建与一次性密钥（子系统自填 appid、应用接入自动生成 moai-sa-；密钥 AES 可逆存储，
//       列表仅回显掩码，原文经 reveal-secret RSA+AES 信封加密随时查看）/ 统一 token 换取 /
//       心跳 v2（按实例 ZSET，多实例在线实例数）/ 团队授权（全员与指定团队）/
//       组织授权（统一组织授权表 org_grant：角色+方向 self/up/down，团队∪组织并集门禁）/
//       停用即拒 / 密钥重置 / 删除释放 appid / 团队可用列表门禁 / 委托 SSO /
//       应用接入同表同接口（appid 直连、全量资源面、子系统专属端点 404）。
// 前置：后端运行中；种子账号 admin / abcd123456（root）。全部用例零真实外部依赖。
// 用法：node local-dev/subsystem-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 SUBSYS_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.SUBSYS_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

// 密钥回显（RSA+AES 信封）：前端随机 AES-256 key 经 RSA 公钥加密上送，响应 GCM 密文本地解出原文
async function revealSecret(rootToken, id) {
  const pub = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  const envelopeKey = crypto.randomBytes(32)
  const encryptedKey = crypto.publicEncrypt({ key: pub, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(envelopeKey.toString('base64'), 'utf8')).toString('base64')
  const r = await api('POST', `/api/subsystem/${id}/reveal-secret`, { token: rootToken, body: { encryptedKey } })
  if (r.status !== 200) throw new Error(`reveal-secret 失败: ${r.status} ${r.text.slice(0, 160)}`)
  const iv = Buffer.from(r.json.iv, 'base64')
  const payload = Buffer.from(r.json.data, 'base64')
  const decipher = crypto.createDecipheriv('aes-256-gcm', envelopeKey, iv)
  decipher.setAuthTag(payload.subarray(-16))
  return Buffer.concat([decipher.update(payload.subarray(0, -16)), decipher.final()]).toString('utf8')
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (rootLogin.status !== 200) throw new Error('种子账号 admin 登录失败，无法执行 root 场景')
  const root = rootLogin.json.accessToken

  const user = await mkuser('su')
  const ownerA = await mkuser('sa') // 团队 A owner（授权团队）
  const ownerB = await mkuser('sb') // 团队 B owner（未授权团队）

  // ---- 管理面门禁 ----
  check('SUB-01 非 root 查询接入列表 403', (await api('GET', '/api/subsystem/list', { token: user.token })).status === 403)
  check('SUB-02 非 root 创建接入 403', (await api('POST', '/api/subsystem/create', { token: user.token, body: { appType: 'subsystem', key: `denied${TS}`, name: 'x', teamScope: 'allTeams' } })).status === 403)

  // ---- 创建与密钥（子系统自填 appid；AES 可逆存储，原文经 reveal 信封查看）----
  const KEY = `report${TS}`
  const invalidKey = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: '1bad-key', name: 'x', teamScope: 'allTeams' } })
  check('SUB-03 非法 appid 格式 400', invalidKey.status === 400, `${invalidKey.status} ${invalidKey.text.slice(0, 120)}`)

  const created = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: KEY, name: '智能报表', description: 'e2e 子系统', teamScope: 'allTeams' } })
  check(
    'SUB-04 root 创建子系统成功且返回密钥掩码（不回原文）',
    created.status === 200 && !!created.json?.id && created.json?.appType === 'subsystem' && created.json?.key === KEY
      && typeof created.json?.secretMask === 'string' && created.json.secretMask.includes('****') && !('secret' in (created.json ?? {})),
    `${created.status} ${created.text.slice(0, 200)}`,
  )
  const SUB_ID = created.json?.id
  const SECRET = await revealSecret(root, SUB_ID)
  check('SUB-04a reveal 信封回显密钥原文（48 位，掩码前后缀一致）', SECRET.length === 48 && SECRET.startsWith(created.json.secretMask.slice(0, 4)) && SECRET.endsWith(created.json.secretMask.slice(-4)), `len=${SECRET.length} mask=${created.json.secretMask}`)

  const dup = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: KEY, name: 'x', teamScope: 'allTeams' } })
  check('SUB-05 重复 appid 409', dup.status === 409)

  const listHas = async () => {
    const l = await api('GET', '/api/subsystem/list', { token: root })
    return { status: l.status, item: (l.json?.items ?? []).find((x) => x.id === SUB_ID) }
  }
  const listHasId = async (id) => {
    const l = await api('GET', '/api/subsystem/list', { token: root })
    return { status: l.status, item: (l.json?.items ?? []).find((x) => x.id === id) }
  }

  // ---- 统一 token 换取（机器面，appId+密钥）----
  check('SUB-06 错误密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: 'wrong-secret' } })).status === 401)
  check('SUB-07 不存在的 appid 换 token 401', (await api('POST', '/api/subsys/token', { body: { appId: `ghost${TS}`, secret: 'whatever' } })).status === 401)

  const tokenOk = await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: SECRET } })
  check('SUB-08 正确密钥换 token 200', tokenOk.status === 200 && !!tokenOk.json?.accessToken && tokenOk.json.tokenType === 'Bearer', `${tokenOk.status} ${tokenOk.text.slice(0, 120)}`)
  const SUB_TOKEN = tokenOk.json?.accessToken

  // ---- 心跳与在线状态 ----
  check('SUB-09 无 token 心跳 401', (await api('POST', '/api/subsys/heartbeat', { body: { instanceId: 'i-1' } })).status === 401)
  check('SUB-10 伪造 token 心跳 401', (await api('POST', '/api/subsys/heartbeat', { token: 'not-a-jwt', body: { instanceId: 'i-1' } })).status === 401)

  let l1 = await listHas()
  check('SUB-11 初始在线状态为离线（实例数 0）', l1.status === 200 && l1.item?.online === false && Number(l1.item?.onlineInstances) === 0, JSON.stringify(l1.item ?? {}).slice(0, 160))

  const hb = await api('POST', '/api/subsys/heartbeat', { token: SUB_TOKEN, body: { instanceId: `inst-${TS}`, version: '1.0.0' } })
  check('SUB-12 接入 token 心跳 200', hb.status === 200, `${hb.status} ${hb.text.slice(0, 120)}`)

  const l2 = await listHas()
  check('SUB-13 心跳后列表在线（单实例实例数 1）', l2.item?.online === true && Number(l2.item?.onlineInstances) === 1, JSON.stringify(l2.item ?? {}).slice(0, 160))

  const info = await api('GET', '/api/subsys/info', { token: SUB_TOKEN })
  check('SUB-14 info 自省返回身份与授权概况', info.status === 200 && info.json?.id === SUB_ID && info.json?.key === KEY && info.json?.appType === 'subsystem' && info.json?.name === '智能报表' && info.json?.teamScope === 'allTeams' && info.json?.online === true, `${info.status} ${info.text.slice(0, 160)}`)
  check('SUB-15 无 token 访问 info 401', (await api('GET', '/api/subsys/info')).status === 401)

  // ---- 停用即时生效 ----
  const disabled = await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', description: 'e2e 子系统', status: 'disabled', teamScope: 'allTeams', teamIds: [] } })
  check('SUB-16 root 停用子系统 200', disabled.status === 200, `${disabled.status} ${disabled.text.slice(0, 120)}`)

  check('SUB-17 停用后密钥换 token 403', (await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: SECRET } })).status === 403)
  check('SUB-18 停用后已有 token 心跳 403', (await api('POST', '/api/subsys/heartbeat', { token: SUB_TOKEN, body: { instanceId: `inst-${TS}` } })).status === 403)

  const l3 = await listHas()
  check('SUB-19 停用清除在线在场集合', l3.item?.online === false && l3.item?.status === 'disabled')

  // ---- 重新启用 + 密钥重置 ----
  await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [] } })
  const reset = await api('POST', `/api/subsystem/${SUB_ID}/reset-secret`, { token: root })
  const NEW_SECRET = await revealSecret(root, SUB_ID)
  check('SUB-20 重置密钥返回新掩码且信封回显新原文', reset.status === 200 && typeof reset.json?.secretMask === 'string' && reset.json.secretMask.includes('****') && NEW_SECRET.length === 48 && NEW_SECRET !== SECRET)
  check('SUB-21 旧密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: SECRET } })).status === 401)
  const token2 = await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: NEW_SECRET } })
  check('SUB-22 新密钥换 token 200', token2.status === 200)

  // ---- 机器 token 刷新（@SUB-S45：refresh 轮换，免重发 appid+密钥）----
  check('SUB-51 换取返回 refresh token', token2.status === 200 && !!token2.json?.refreshToken, `${token2.status} ${(token2.text || '').slice(0, 120)}`)
  const badRefresh = await api('POST', '/api/subsys/token/refresh', { body: { refreshToken: 'not-a-jwt' } })
  check('SUB-52 伪造 refresh token 401', badRefresh.status === 401, `${badRefresh.status}`)
  const refreshed = await api('POST', '/api/subsys/token/refresh', { body: { refreshToken: token2.json?.refreshToken } })
  check(
    'SUB-53 refresh 轮换新对且新 access 可用',
    refreshed.status === 200 && !!refreshed.json?.accessToken && !!refreshed.json?.refreshToken
      && refreshed.json.accessToken !== token2.json.accessToken
      && (await api('GET', '/api/subsys/info', { token: refreshed.json.accessToken })).status === 200,
    `${refreshed.status} ${refreshed.text.slice(0, 120)}`,
  )

  // ---- 团队授权：全员可用 ----
  const tA = await api('POST', '/api/team', { token: ownerA.token, body: { name: `subsys-team-a-${TS}`, description: 'e2e' } })
  const tB = await api('POST', '/api/team', { token: ownerB.token, body: { name: `subsys-team-b-${TS}`, description: 'e2e' } })
  const TID_A = Number(tA.json?.value)
  const TID_B = Number(tB.json?.value)
  check('SUB-23 建立两个测试团队', TID_A > 0 && TID_B > 0, `${tA.status}/${tB.status}`)

  const availAllA = await api('GET', `/api/team/${TID_A}/subsystem/list`, { token: ownerA.token })
  check('SUB-24 全员可用：团队 A 可见该子系统', availAllA.status === 200 && (availAllA.json?.items ?? []).some((x) => x.key === KEY), `${availAllA.status} ${availAllA.text.slice(0, 160)}`)

  // ---- 切换为指定团队：仅授权团队可见 ----
  const upd = await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', description: 'e2e 子系统', status: 'enabled', teamScope: 'specifiedTeams', teamIds: [TID_A] } })
  check('SUB-25 切换为指定团队并授权团队 A 200', upd.status === 200, `${upd.status} ${upd.text.slice(0, 120)}`)

  const authTeams = await api('GET', `/api/subsystem/${SUB_ID}/teams`, { token: root })
  check('SUB-26 授权团队回显正确', authTeams.status === 200 && JSON.stringify(authTeams.json?.teamIds ?? []) === JSON.stringify([TID_A]), `${authTeams.status} ${authTeams.text.slice(0, 120)}`)

  const availA2 = await api('GET', `/api/team/${TID_A}/subsystem/list`, { token: ownerA.token })
  const availB2 = await api('GET', `/api/team/${TID_B}/subsystem/list`, { token: ownerB.token })
  check('SUB-27 授权团队 A 仍可见', availA2.status === 200 && (availA2.json?.items ?? []).some((x) => x.key === KEY))
  check('SUB-28 未授权团队 B 不可见', availB2.status === 200 && !(availB2.json?.items ?? []).some((x) => x.key === KEY))

  const missingTeam = await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', status: 'enabled', teamScope: 'specifiedTeams', teamIds: [99999999] } })
  check('SUB-29 授权不存在的团队 400', missingTeam.status === 400, `${missingTeam.status} ${missingTeam.text.slice(0, 120)}`)

  check('SUB-30 非团队成员查可用列表 404', (await api('GET', `/api/team/${TID_A}/subsystem/list`, { token: user.token })).status === 404)

  // ---- 编辑回显（名称/授权团队清单）与删除 ----
  const l4 = await listHas()
  const teamAName = `subsys-team-a-${TS}`
  check(
    'SUB-31 列表回显指定团队数/完整团队名称与审计信息',
    l4.item?.teamScope === 'specifiedTeams'
      && Number(l4.item?.authorizedTeamCount) === 1
      && Array.isArray(l4.item?.authorizedTeams)
      && l4.item.authorizedTeams.length === 1
      && Number(l4.item.authorizedTeams[0]?.teamId) === TID_A
      && l4.item.authorizedTeams[0]?.teamName === teamAName
      && !!l4.item?.createTime,
    JSON.stringify(l4.item ?? {}).slice(0, 200),
  )

  check('SUB-32 非完整参数更新 400', (await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { teamScope: 'allTeams', teamIds: [] } })).status === 400)

  const del = await api('DELETE', `/api/subsystem/${SUB_ID}`, { token: root })
  check('SUB-33 root 删除子系统 200', del.status === 200, `${del.status} ${del.text.slice(0, 120)}`)
  check('SUB-34 删除后密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: NEW_SECRET } })).status === 401)

  const recreated = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: KEY, name: '复用标识', teamScope: 'allTeams' } })
  check('SUB-35 删除后 appid 释放可复用', recreated.status === 200, `${recreated.status} ${recreated.text.slice(0, 120)}`)
  const RECREATED_ID = recreated.json?.id
  const RECREATED_SECRET = await revealSecret(root, recreated.json?.id)

  // ---- 委托 SSO：登录用户签发一次性委托码，子系统机器面兑换用户委托 token ----
  const tokenR = await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: RECREATED_SECRET } })
  check('SUB-36 重建后换取子系统 token 200', tokenR.status === 200, `${tokenR.status} ${tokenR.text.slice(0, 120)}`)
  const R_TOKEN = tokenR.json?.accessToken

  check('SUB-37 未登录请求委托码 401', (await api('POST', '/api/subsystem/sso/code', { body: { subsystemKey: KEY } })).status === 401)

  const noCb = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  check('SUB-38 未配置回调地址时签发委托码 403', noCb.status === 403, `${noCb.status} ${noCb.text.slice(0, 120)}`)

  const CALLBACK = `https://report.local/cb?ts=${TS}`
  const setCb = await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [], callbackUrls: [CALLBACK] } })
  check('SUB-39 root 配置登录回调地址 200', setCb.status === 200, `${setCb.status} ${setCb.text.slice(0, 120)}`)

  const noTeam = await api('POST', '/api/subsystem/sso/code', { token: user.token, body: { subsystemKey: KEY } })
  check('SUB-40 全员范围下无团队用户签发被拒 403', noTeam.status === 403, `${noTeam.status} ${noTeam.text.slice(0, 120)}`)

  const STATE = `st-${TS}`
  const authorize = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY, state: STATE } })
  check(
    'SUB-41 授权用户签发委托码 200（返回注册回调与名称）',
    authorize.status === 200 && !!authorize.json?.code && authorize.json?.callbackUrl === CALLBACK && authorize.json?.name === '复用标识',
    `${authorize.status} ${authorize.text.slice(0, 160)}`,
  )
  const CODE = authorize.json?.code

  // ---- 多回调地址：redirect_uri 指定入口（须在注册白名单内，缺省取第一个）----
  const CALLBACK_TEST = `https://test.report.local/cb?ts=${TS}`
  const setCb2 = await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [], callbackUrls: [CALLBACK, CALLBACK_TEST] } })
  check('SUB-91 配置多回调地址 200', setCb2.status === 200, `${setCb2.status} ${setCb2.text.slice(0, 120)}`)

  const authorizeTest = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY, redirectUri: CALLBACK_TEST } })
  check('SUB-92 redirect_uri 指定测试入口回跳该地址', authorizeTest.status === 200 && authorizeTest.json?.callbackUrl === CALLBACK_TEST, `${authorizeTest.status} ${authorizeTest.text.slice(0, 160)}`)

  const authorizeBad = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY, redirectUri: 'https://evil.local/cb' } })
  check('SUB-93 未注册的 redirect_uri 403', authorizeBad.status === 403, `${authorizeBad.status} ${authorizeBad.text.slice(0, 120)}`)

  const authorizeFirst = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  check('SUB-94 缺省回跳注册列表第一个', authorizeFirst.status === 200 && authorizeFirst.json?.callbackUrl === CALLBACK, `${authorizeFirst.status} ${authorizeFirst.text.slice(0, 160)}`)

  check('SUB-42 委托码兑换需子系统 token 401', (await api('POST', '/api/subsys/sso/exchange', { body: { code: CODE } })).status === 401)

  const exchange = await api('POST', '/api/subsys/sso/exchange', { token: R_TOKEN, body: { code: CODE } })
  check(
    'SUB-43 兑换返回用户委托 token 与资料',
    exchange.status === 200 && !!exchange.json?.accessToken && !!exchange.json?.refreshToken && Number(exchange.json?.userId) === ownerA.userId && !!exchange.json?.userName,
    `${exchange.status} ${exchange.text.slice(0, 160)}`,
  )
  const USER_REFRESH = exchange.json?.refreshToken

  const replay = await api('POST', '/api/subsys/sso/exchange', { token: R_TOKEN, body: { code: CODE } })
  check('SUB-44 委托码一次性重放 401', replay.status === 401, `${replay.status} ${replay.text.slice(0, 120)}`)

  // 跨子系统兑换：另一个子系统拿不到别人的委托码
  const KEY2 = `other${TS}`
  const created2 = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: KEY2, name: '别的子系统', teamScope: 'allTeams', callbackUrls: ['https://other.local/cb'] } })
  const token2nd = await api('POST', '/api/subsys/token', { body: { appId: KEY2, secret: await revealSecret(root, created2.json?.id) } })
  const authorize2 = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  const crossExchange = await api('POST', '/api/subsys/sso/exchange', { token: token2nd.json?.accessToken, body: { code: authorize2.json?.code } })
  check('SUB-45 委托码绑定子系统跨子系统兑换 401', crossExchange.status === 401, `${crossExchange.status} ${crossExchange.text.slice(0, 120)}`)

  const refreshOk = await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: USER_REFRESH } })
  check('SUB-46 用户委托 token 刷新 200 返回新 token', refreshOk.status === 200 && !!refreshOk.json?.accessToken && Number(refreshOk.json?.userId) === ownerA.userId, `${refreshOk.status} ${refreshOk.text.slice(0, 160)}`)
  check('SUB-47 无效 refresh 刷新 401', (await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: 'not-a-token' } })).status === 401)

  // 指定团队范围：未授权团队用户不能签发委托码
  await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', status: 'enabled', teamScope: 'specifiedTeams', teamIds: [TID_B], callbackUrls: [CALLBACK] } })
  const deniedA = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  check('SUB-48 未授权团队用户签发委托码 403', deniedA.status === 403, `${deniedA.status} ${deniedA.text.slice(0, 120)}`)

  // 停用后：签发与刷新一并拒绝
  await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', status: 'disabled', teamScope: 'allTeams', teamIds: [], callbackUrls: [CALLBACK] } })
  check('SUB-49 停用后签发委托码 403', (await api('POST', '/api/subsystem/sso/code', { token: ownerB.token, body: { subsystemKey: KEY } })).status === 403)
  check('SUB-50 停用后用户委托 refresh 403', (await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: USER_REFRESH } })).status === 403)

  // ---- 组织授权：统一组织授权表（org_grant，资源类型 subsystem），团队与组织任一命中即可使用 ----
  const roleP = await api('POST', '/api/org/role/create', { token: root, body: { name: `e2e-parent-${TS}`, parentId: null } })
  const PID = Number(roleP.json?.orgRoleId)
  const roleC = await api('POST', '/api/org/role/create', { token: root, body: { name: `e2e-child-${TS}`, parentId: PID } })
  const CID = Number(roleC.json?.orgRoleId)
  check('SUB-54 创建组织角色父子两级', PID > 0 && CID > 0, `${roleP.status}/${roleC.status} ${roleP.text.slice(0, 120)}`)

  const memberP = await mkuser('sp')
  const memberC = await mkuser('sc')
  await api('PUT', `/api/org/user/${memberP.userId}/roles`, { token: root, body: { roleIds: [PID] } })
  await api('PUT', `/api/org/user/${memberC.userId}/roles`, { token: root, body: { roleIds: [CID] } })

  const orgKey = `orggrant${TS}`
  const orgSub = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: orgKey, name: '组织授权子系统', teamScope: 'specifiedTeams', teamIds: [], callbackUrls: [CALLBACK] } })
  const ORG_ID = orgSub.json?.id
  check('SUB-55 指定团队空清单创建 200（仅组织授权待配置）', orgSub.status === 200 && !!ORG_ID, `${orgSub.status} ${orgSub.text.slice(0, 120)}`)

  const dupGrant = await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: PID, scope: 'down' }, { orgRoleId: PID, scope: 'up' }] } })
  check('SUB-56 同一组织角色重复授权 400', dupGrant.status === 400, `${dupGrant.status} ${dupGrant.text.slice(0, 120)}`)

  const ghostGrant = await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: 99999999, scope: 'down' }] } })
  check('SUB-57 授权不存在的组织角色 400', ghostGrant.status === 400, `${ghostGrant.status} ${ghostGrant.text.slice(0, 120)}`)

  const setGrant = await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: PID, scope: 'down' }] } })
  check('SUB-58 指定组织授权（父角色 down）200', setGrant.status === 200, `${setGrant.status} ${setGrant.text.slice(0, 120)}`)

  const echoGrants = await api('GET', `/api/subsystem/${ORG_ID}/org-grants`, { token: root })
  check(
    'SUB-59 组织授权回显（角色名/方向）',
    echoGrants.status === 200 && Number(echoGrants.json?.items?.[0]?.orgRoleId) === PID && echoGrants.json.items[0]?.scope === 'down' && echoGrants.json.items[0]?.roleName === `e2e-parent-${TS}`,
    `${echoGrants.status} ${echoGrants.text.slice(0, 160)}`,
  )

  const listOrg = await api('GET', '/api/subsystem/list', { token: root })
  const orgItem = (listOrg.json?.items ?? []).find((x) => x.key === orgKey)
  check(
    'SUB-60 列表 authorizedOrgs 回显',
    !!orgItem && Array.isArray(orgItem.authorizedOrgs) && orgItem.authorizedOrgs.length === 1 && orgItem.authorizedOrgs[0]?.orgName === `e2e-parent-${TS}`,
    JSON.stringify(orgItem?.authorizedOrgs ?? []).slice(0, 160),
  )

  const ssoChild = await api('POST', '/api/subsystem/sso/code', { token: memberC.token, body: { subsystemKey: orgKey } })
  check('SUB-61 子组织角色成员 SSO 放行（down 子树命中）', ssoChild.status === 200, `${ssoChild.status} ${ssoChild.text.slice(0, 120)}`)

  check('SUB-62 无团队无组织用户 SSO 403', (await api('POST', '/api/subsystem/sso/code', { token: user.token, body: { subsystemKey: orgKey } })).status === 403)

  await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: PID, scope: 'self' }] } })
  const selfChild = await api('POST', '/api/subsystem/sso/code', { token: memberC.token, body: { subsystemKey: orgKey } })
  const selfParent = await api('POST', '/api/subsystem/sso/code', { token: memberP.token, body: { subsystemKey: orgKey } })
  check('SUB-63 改 self 后子角色成员 403', selfChild.status === 403, `${selfChild.status}`)
  check('SUB-64 self 下父角色成员放行', selfParent.status === 200, `${selfParent.status}`)

  await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [{ orgRoleId: CID, scope: 'up' }] } })
  const upParent = await api('POST', '/api/subsystem/sso/code', { token: memberP.token, body: { subsystemKey: orgKey } })
  check('SUB-65 up：授权角色及以上（父角色成员）放行', upParent.status === 200, `${upParent.status} ${upParent.text.slice(0, 120)}`)

  await api('PUT', `/api/subsystem/${ORG_ID}`, { token: root, body: { name: '组织授权子系统', description: 'e2e 子系统', status: 'enabled', teamScope: 'specifiedTeams', teamIds: [TID_A], callbackUrls: [CALLBACK] } })
  const unionTeam = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: orgKey } })
  check('SUB-66 团队∪组织并集：团队授权成员（无组织）放行', unionTeam.status === 200, `${unionTeam.status} ${unionTeam.text.slice(0, 120)}`)

  await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: root, body: { grants: [] } })
  const afterClearOrg = await api('POST', '/api/subsystem/sso/code', { token: memberP.token, body: { subsystemKey: orgKey } })
  const afterClearTeam = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: orgKey } })
  check('SUB-67 清空组织授权后组织成员 403、团队授权成员仍放行', afterClearOrg.status === 403 && afterClearTeam.status === 200, `${afterClearOrg.status}/${afterClearTeam.status}`)

  check('SUB-68 非管理员读组织授权 403', (await api('GET', `/api/subsystem/${ORG_ID}/org-grants`, { token: user.token })).status === 403)
  check('SUB-69 非管理员写组织授权 403', (await api('PUT', `/api/subsystem/${ORG_ID}/org-grants`, { token: user.token, body: { grants: [] } })).status === 403)

  // ---- 心跳 v2：按实例记录（多实例存活探针，@SUB-S53+ 新增）----
  await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [], callbackUrls: [CALLBACK] } })
  const hbNoInst = await api('POST', '/api/subsys/heartbeat', { token: R_TOKEN, body: { version: '1.0.0' } })
  check('SUB-70 心跳缺实例标识 400', hbNoInst.status === 400, `${hbNoInst.status} ${hbNoInst.text.slice(0, 120)}`)

  const hbA = await api('POST', '/api/subsys/heartbeat', { token: R_TOKEN, body: { instanceId: `instA-${TS}` } })
  check('SUB-71 实例 A 心跳 200', hbA.status === 200, `${hbA.status}`)
  const l5 = await listHasId(RECREATED_ID)
  check('SUB-72 单实例心跳后在线实例数 1', l5.item?.online === true && Number(l5.item?.onlineInstances) === 1, JSON.stringify(l5.item ?? {}).slice(0, 160))

  const hbB = await api('POST', '/api/subsys/heartbeat', { token: R_TOKEN, body: { instanceId: `instB-${TS}`, version: '2.0.0' } })
  check('SUB-73 实例 B 心跳 200', hbB.status === 200, `${hbB.status}`)
  const l6 = await listHasId(RECREATED_ID)
  check('SUB-74 双实例心跳在线实例数 2', l6.item?.online === true && Number(l6.item?.onlineInstances) === 2, JSON.stringify(l6.item ?? {}).slice(0, 160))

  const infoHb = await api('GET', '/api/subsys/info', { token: R_TOKEN })
  check('SUB-75 自省回显在线实例数 2', infoHb.status === 200 && infoHb.json?.online === true && Number(infoHb.json?.onlineInstances) === 2, `${infoHb.status} ${infoHb.text.slice(0, 160)}`)

  // ---- 合并模型：应用接入（同表同接口，无资源范围选择）----
  check('SUB-76 子系统 appid 使用 moai- 保留前缀 400', (await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: `moai-sa-fake${TS}`, name: 'x' } })).status === 400)

  const appCreated = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'app', name: `ERP 同步 ${TS}`, description: 'e2e 应用接入' } })
  const APP_KEY = appCreated.json?.key
  const APP_ID = appCreated.json?.id
  const APP_SECRET = await revealSecret(root, APP_ID)
  check(
    'SUB-77 创建应用接入自动生成 moai-sa- appid（密钥仅回掩码，信封可回显原文）',
    appCreated.status === 200 && APP_KEY?.startsWith('moai-sa-') && APP_KEY.length === 35 && APP_SECRET.length === 48 && appCreated.json?.appType === 'app' && appCreated.json?.secretMask?.includes('****'),
    `${appCreated.status} ${appCreated.text.slice(0, 200)}`,
  )

  const listApp = await api('GET', '/api/subsystem/list', { token: root })
  const appItem = (listApp.json?.items ?? []).find((x) => x.id === APP_ID)
  check(
    'SUB-78 列表回显应用接入（appType/keyPrefix/密钥掩码，不回显原文）',
    appItem && appItem.appType === 'app' && appItem.keyPrefix === APP_KEY.slice(0, 12) && appItem.secretMask?.includes('****') && !JSON.stringify(appItem).includes(APP_KEY) && !JSON.stringify(appItem).includes(APP_SECRET),
    JSON.stringify(appItem ?? null).slice(0, 200),
  )

  const appOnly = await api('GET', '/api/subsystem/list?appType=app', { token: root })
  check(
    'SUB-79 列表按 appType=app 过滤',
    appOnly.status === 200 && (appOnly.json?.items ?? []).length > 0 && (appOnly.json?.items ?? []).every((x) => x.appType === 'app'),
    `${appOnly.status}`,
  )

  const appToken = await api('POST', '/api/subsys/token', { body: { appId: APP_KEY, secret: APP_SECRET } })
  check('SUB-80 应用接入凭 appid+密钥经同一 /api/subsys/token 换取 200', appToken.status === 200 && !!appToken.json?.accessToken, `${appToken.status} ${appToken.text.slice(0, 120)}`)

  const appInfoDirect = await api('GET', '/api/subsys/info', { token: APP_KEY })
  check('SUB-81 应用接入 appid 直连自省 200', appInfoDirect.status === 200 && appInfoDirect.json?.id === APP_ID && appInfoDirect.json?.appType === 'app', `${appInfoDirect.status} ${appInfoDirect.text.slice(0, 160)}`)
  check('SUB-82 子系统自填 appid 不能直连 401', (await api('GET', '/api/subsys/info', { token: KEY })).status === 401)
  check('SUB-82a 格式非法的 moai-sa- appid 直连 401（本地格式预检，长度不符）', (await api('GET', '/api/subsys/info', { token: 'moai-sa-forged' })).status === 401)
  check('SUB-82b 格式非法的 moai-sa- appid 直连 401（含非法字符）', (await api('GET', '/api/subsys/info', { token: 'moai-sa-!!!!@@@@$$$$%%%%))))' })).status === 401)
  const forgedLen = `moai-sa-${'a'.repeat(31)}`
  check('SUB-82c 长度差一位的合法字符集 appid 直连 401（31 位）', (await api('GET', '/api/subsys/info', { token: forgedLen })).status === 401)
  const tampered = APP_KEY.slice(0, -1) + (APP_KEY.endsWith('A') ? 'B' : 'A')
  check('SUB-82d 篡改 HMAC 的结构化 appid 直连 401（本地校验不过）', (await api('GET', '/api/subsys/info', { token: tampered })).status === 401)

  const appUsers = await api('GET', '/api/subsys/system/users/list?pageNo=1&pageSize=5', { token: APP_KEY })
  check('SUB-83 应用接入直连访问系统资源接口 200（全量无范围）', appUsers.status === 200 && Array.isArray(appUsers.json?.items), `${appUsers.status} ${appUsers.text.slice(0, 120)}`)
  const appOrgWrite = await api('POST', '/api/subsys/system/org/roles', { token: APP_KEY, body: { name: `app写入组织-${TS}` } })
  const appOrgDel = appOrgWrite.status === 200 ? await api('DELETE', `/api/subsys/system/org/roles/${appOrgWrite.json?.orgRoleId}`, { token: APP_KEY }) : null
  check('SUB-84 应用接入写资源面 200（原 scope 分档下线）', appOrgWrite.status === 200 && appOrgDel?.status === 200, `write=${appOrgWrite.status} del=${appOrgDel?.status} ${appOrgWrite.text.slice(0, 100)}`)

  const legacyTok = await api('POST', '/api/subsys/system/token', { token: APP_KEY, body: { appId: APP_KEY, secret: APP_SECRET } })
  check('SUB-85 旧 /api/subsys/system/token 端点已下线（404/405）', legacyTok.status === 404 || legacyTok.status === 405, `${legacyTok.status}`)
  check('SUB-86 旧 /api/subsys/system/info 端点已下线 404', (await api('GET', '/api/subsys/system/info', { token: APP_KEY })).status === 404)

  const appTeams = await api('GET', `/api/subsystem/${APP_ID}/teams`, { token: root })
  const appGrants = await api('GET', `/api/subsystem/${APP_ID}/org-grants`, { token: root })
  const appGrantsPut = await api('PUT', `/api/subsystem/${APP_ID}/org-grants`, { token: root, body: { grants: [] } })
  check('SUB-87 应用接入行访问子系统专属端点 404', appTeams.status === 404 && appGrants.status === 404 && appGrantsPut.status === 404, `teams=${appTeams.status} grants=${appGrants.status} put=${appGrantsPut.status}`)

  const appSso = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: APP_KEY } })
  check('SUB-88 应用接入行签发委托码 404（appType 过滤）', appSso.status === 404, `${appSso.status}`)

  const appHb = await api('POST', '/api/subsys/heartbeat', { token: APP_KEY, body: { instanceId: `appinst-${TS}` } })
  const appHbInfo = appHb.status === 200 ? await api('GET', '/api/subsys/info', { token: APP_KEY }) : null
  check('SUB-89 应用接入同样可心跳并在自省回显在线', appHb.status === 200 && appHbInfo?.json?.online === true && Number(appHbInfo?.json?.onlineInstances) === 1, `hb=${appHb.status} info=${appHbInfo?.status} ${JSON.stringify(appHbInfo?.json ?? {}).slice(0, 120)}`)

  // 清理应用接入
  await api('DELETE', `/api/subsystem/${APP_ID}`, { token: root })
  check('SUB-90 删除应用接入后直连立即失效 401', (await api('GET', '/api/subsys/info', { token: APP_KEY })).status === 401)

  // ---- 公网限流：token 换取失败 (IP, appid) 维度 10 次/60s 触发 429，按维度隔离 ----
  let rlStatus = 0
  for (let i = 0; i < 12; i++) {
    rlStatus = (await api('POST', '/api/subsys/token', { body: { appId: `ratelimit${TS}`, secret: 'wrong-secret' } })).status
  }

  check('SUB-95 同 (IP, appid) 连续失败 10 次后触发限流 429', rlStatus === 429, `last=${rlStatus}`)
  const rlOk = await api('POST', '/api/subsys/token', { body: { appId: KEY, secret: RECREATED_SECRET } })
  check('SUB-96 限流按 (IP, appid) 维度隔离：其他 appid 不受影响', rlOk.status === 200, `${rlOk.status} ${rlOk.text.slice(0, 120)}`)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => {
  console.error('E2E 执行失败:', e.message)
  process.exit(1)
})
