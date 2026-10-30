// 子系统注册与接入 · E2E（场景 @SUB-S1..）
// 覆盖：root 门禁 / 创建与一次性密钥 / token 换取 / 心跳与在线状态 / 团队授权（全员与指定团队）/
//       停用即拒 / 密钥重置 / 删除释放 appid / 团队可用列表门禁。
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
  check('SUB-01 非 root 查询子系统列表 403', (await api('GET', '/api/subsystem/list', { token: user.token })).status === 403)
  check('SUB-02 非 root 创建子系统 403', (await api('POST', '/api/subsystem/create', { token: user.token, body: { key: `denied${TS}`, name: 'x', teamScope: 'allTeams' } })).status === 403)

  // ---- 创建与一次性密钥 ----
  const KEY = `report${TS}`
  const invalidKey = await api('POST', '/api/subsystem/create', { token: root, body: { key: '1bad-key', name: 'x', teamScope: 'allTeams' } })
  check('SUB-03 非法 appid 格式 400', invalidKey.status === 400, `${invalidKey.status} ${invalidKey.text.slice(0, 120)}`)

  const created = await api('POST', '/api/subsystem/create', { token: root, body: { key: KEY, name: '智能报表', description: 'e2e 子系统', teamScope: 'allTeams' } })
  check('SUB-04 root 创建成功且返回一次性密钥', created.status === 200 && !!created.json?.secret && created.json.secret.length === 48 && !!created.json?.subsystemId, `${created.status} ${created.text.slice(0, 160)}`)
  const SUB_ID = created.json?.subsystemId
  const SECRET = created.json?.secret

  const dup = await api('POST', '/api/subsystem/create', { token: root, body: { key: KEY, name: 'x', teamScope: 'allTeams' } })
  check('SUB-05 重复 appid 409', dup.status === 409)

  const listHas = async () => {
    const l = await api('GET', '/api/subsystem/list', { token: root })
    return { status: l.status, item: (l.json?.items ?? []).find((x) => x.subsystemId === SUB_ID) }
  }

  // ---- token 换取（机器面）----
  check('SUB-06 错误密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { key: KEY, secret: 'wrong-secret' } })).status === 401)
  check('SUB-07 不存在的 appid 换 token 401', (await api('POST', '/api/subsys/token', { body: { key: `ghost${TS}`, secret: 'whatever' } })).status === 401)

  const tokenOk = await api('POST', '/api/subsys/token', { body: { key: KEY, secret: SECRET } })
  check('SUB-08 正确密钥换 token 200', tokenOk.status === 200 && !!tokenOk.json?.accessToken && tokenOk.json.tokenType === 'Bearer', `${tokenOk.status} ${tokenOk.text.slice(0, 120)}`)
  const SUB_TOKEN = tokenOk.json?.accessToken

  // ---- 心跳与在线状态 ----
  check('SUB-09 无 token 心跳 401', (await api('POST', '/api/subsys/heartbeat', { body: { instanceId: 'i-1' } })).status === 401)
  check('SUB-10 伪造 token 心跳 401', (await api('POST', '/api/subsys/heartbeat', { token: 'not-a-jwt', body: {} })).status === 401)

  let l1 = await listHas()
  check('SUB-11 初始在线状态为离线', l1.status === 200 && l1.item?.online === false, JSON.stringify(l1.item ?? {}).slice(0, 120))

  const hb = await api('POST', '/api/subsys/heartbeat', { token: SUB_TOKEN, body: { instanceId: `inst-${TS}`, version: '1.0.0' } })
  check('SUB-12 子系统 token 心跳 200', hb.status === 200, `${hb.status} ${hb.text.slice(0, 120)}`)

  const l2 = await listHas()
  check('SUB-13 心跳后列表在线', l2.item?.online === true)

  const info = await api('GET', '/api/subsys/info', { token: SUB_TOKEN })
  check('SUB-14 info 自省返回身份与授权概况', info.status === 200 && info.json?.key === KEY && info.json?.name === '智能报表' && info.json?.teamScope === 'allTeams' && info.json?.online === true, `${info.status} ${info.text.slice(0, 160)}`)
  check('SUB-15 无 token 访问 info 401', (await api('GET', '/api/subsys/info')).status === 401)

  // ---- 停用即时生效 ----
  const disabled = await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', description: 'e2e 子系统', status: 'disabled', teamScope: 'allTeams', teamIds: [] } })
  check('SUB-16 root 停用子系统 200', disabled.status === 200, `${disabled.status} ${disabled.text.slice(0, 120)}`)

  check('SUB-17 停用后密钥换 token 403', (await api('POST', '/api/subsys/token', { body: { key: KEY, secret: SECRET } })).status === 403)
  check('SUB-18 停用后已有 token 心跳 403', (await api('POST', '/api/subsys/heartbeat', { token: SUB_TOKEN, body: {} })).status === 403)

  const l3 = await listHas()
  check('SUB-19 停用清除在线在场键', l3.item?.online === false && l3.item?.status === 'disabled')

  // ---- 重新启用 + 密钥重置 ----
  await api('PUT', `/api/subsystem/${SUB_ID}`, { token: root, body: { name: '智能报表', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [] } })
  const reset = await api('POST', `/api/subsystem/${SUB_ID}/reset-secret`, { token: root })
  check('SUB-20 重置密钥返回新密钥', reset.status === 200 && !!reset.json?.secret && reset.json.secret !== SECRET && reset.json.secret.length === 48)
  check('SUB-21 旧密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { key: KEY, secret: SECRET } })).status === 401)
  const token2 = await api('POST', '/api/subsys/token', { body: { key: KEY, secret: reset.json?.secret } })
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
  check('SUB-34 删除后密钥换 token 401', (await api('POST', '/api/subsys/token', { body: { key: KEY, secret: reset.json?.secret } })).status === 401)

  const recreated = await api('POST', '/api/subsystem/create', { token: root, body: { key: KEY, name: '复用标识', teamScope: 'allTeams' } })
  check('SUB-35 删除后 appid 释放可复用', recreated.status === 200, `${recreated.status} ${recreated.text.slice(0, 120)}`)
  const RECREATED_ID = recreated.json?.subsystemId
  const RECREATED_SECRET = recreated.json?.secret

  // ---- 委托 SSO：登录用户签发一次性委托码，子系统机器面兑换用户委托 token ----
  const tokenR = await api('POST', '/api/subsys/token', { body: { key: KEY, secret: RECREATED_SECRET } })
  check('SUB-36 重建后换取子系统 token 200', tokenR.status === 200, `${tokenR.status} ${tokenR.text.slice(0, 120)}`)
  const R_TOKEN = tokenR.json?.accessToken

  check('SUB-37 未登录请求委托码 401', (await api('POST', '/api/subsystem/sso/code', { body: { subsystemKey: KEY } })).status === 401)

  const noCb = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  check('SUB-38 未配置回调地址时签发委托码 403', noCb.status === 403, `${noCb.status} ${noCb.text.slice(0, 120)}`)

  const CALLBACK = `https://report.local/cb?ts=${TS}`
  const setCb = await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', description: 'e2e 子系统', status: 'enabled', teamScope: 'allTeams', teamIds: [], callbackUrl: CALLBACK } })
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
  const created2 = await api('POST', '/api/subsystem/create', { token: root, body: { key: KEY2, name: '别的子系统', teamScope: 'allTeams', callbackUrl: 'https://other.local/cb' } })
  const token2nd = await api('POST', '/api/subsys/token', { body: { key: KEY2, secret: created2.json?.secret } })
  const authorize2 = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  const crossExchange = await api('POST', '/api/subsys/sso/exchange', { token: token2nd.json?.accessToken, body: { code: authorize2.json?.code } })
  check('SUB-45 委托码绑定子系统跨子系统兑换 401', crossExchange.status === 401, `${crossExchange.status} ${crossExchange.text.slice(0, 120)}`)

  const refreshOk = await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: USER_REFRESH } })
  check('SUB-46 用户委托 token 刷新 200 返回新 token', refreshOk.status === 200 && !!refreshOk.json?.accessToken && Number(refreshOk.json?.userId) === ownerA.userId, `${refreshOk.status} ${refreshOk.text.slice(0, 160)}`)
  check('SUB-47 无效 refresh 刷新 401', (await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: 'not-a-token' } })).status === 401)

  // 指定团队范围：未授权团队用户不能签发委托码
  await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', status: 'enabled', teamScope: 'specifiedTeams', teamIds: [TID_B], callbackUrl: CALLBACK } })
  const deniedA = await api('POST', '/api/subsystem/sso/code', { token: ownerA.token, body: { subsystemKey: KEY } })
  check('SUB-48 未授权团队用户签发委托码 403', deniedA.status === 403, `${deniedA.status} ${deniedA.text.slice(0, 120)}`)

  // 停用后：签发与刷新一并拒绝
  await api('PUT', `/api/subsystem/${RECREATED_ID}`, { token: root, body: { name: '复用标识', status: 'disabled', teamScope: 'allTeams', teamIds: [], callbackUrl: CALLBACK } })
  check('SUB-49 停用后签发委托码 403', (await api('POST', '/api/subsystem/sso/code', { token: ownerB.token, body: { subsystemKey: KEY } })).status === 403)
  check('SUB-50 停用后用户委托 refresh 403', (await api('POST', '/api/subsys/sso/refresh', { token: R_TOKEN, body: { refreshToken: USER_REFRESH } })).status === 403)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => {
  console.error('E2E 执行失败:', e.message)
  process.exit(1)
})
