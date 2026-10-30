// 系统应用接入 · E2E（场景 @SAA-S1..）
// 覆盖：root 门禁 / 创建与一次性 key / 范围校验 / 列表回显（仅前缀）/ key 直连自省 /
//       范围与状态更新即时生效 / 停用 403 / 删除即失效 / 搜索。
// 前置：后端运行中；种子账号 admin / abcd123456（root）。全部用例零真实外部依赖。
// 用法：node local-dev/system-access-app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5210，可用 SAA_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.SAA_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5210'
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

const si = await (await fetch(BASE + '/api/common/serverinfo')).json()
RSA_KEY = si.rsaPublic

const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
if (rootLogin.status !== 200) {
  console.error('root 登录失败，终止:', rootLogin.status, rootLogin.text.slice(0, 200))
  process.exit(1)
}
const root = rootLogin.json.accessToken

const user0 = uname('saa')
await api('POST', '/api/auth/register', { body: { userName: user0, email: `${user0}@test.local`, nickName: user0, phone: phone(), password: rsa('Test1234') } })
const userLogin = await api('POST', '/api/auth/login', { body: { userName: user0, password: rsa('Test1234') } })
const user = userLogin.json.accessToken

console.log(`目标：${BASE}\n`)

// ---- 管理面门禁 ----
check('SAA-S1 未登录查询列表 401', (await api('GET', '/api/subsystem/system-app/list')).status === 401)
check('SAA-S2 非 root 查询列表 403', (await api('GET', '/api/subsystem/system-app/list', { token: user })).status === 403)
check('SAA-S3 非 root 创建 403', (await api('POST', '/api/subsystem/system-app/create', { token: user, body: { name: 'x', scopes: ['org_read'] } })).status === 403)

// ---- 创建与 key ----
const badScopes = await api('POST', '/api/subsystem/system-app/create', { token: root, body: { name: 'x', scopes: ['not_a_scope'] } })
check('SAA-S4 非法范围代码 400', badScopes.status === 400, `${badScopes.status}`)
const emptyScopes = await api('POST', '/api/subsystem/system-app/create', { token: root, body: { name: 'x', scopes: [] } })
check('SAA-S4a 空范围 400', emptyScopes.status === 400, `${emptyScopes.status}`)

const created = await api('POST', '/api/subsystem/system-app/create', {
  token: root,
  body: { name: `ERP 同步 ${TS}`, description: '同步 ERP 组织角色', scopes: ['org_read', 'org_write'] },
})
check('SAA-S5 root 创建返回一次性 key', created.status === 200 && typeof created.json.key === 'string' && created.json.key.startsWith('moai-sa-') && created.json.key.length === 40,
  created.text.slice(0, 160))
const APP_KEY = created.json.key
const APP_ID = created.json.id

const list1 = await api('GET', '/api/subsystem/system-app/list', { token: root })
const item1 = (list1.json?.items ?? []).find((x) => x.id === APP_ID)
check('SAA-S6 列表回显前缀而非原文', list1.status === 200 && item1 && item1.keyPrefix === APP_KEY.slice(0, 12) && !JSON.stringify(item1).includes(APP_KEY),
  JSON.stringify(item1 ?? null).slice(0, 160))
check('SAA-S6a 列表回显范围代码', item1 && JSON.stringify(item1.scopes.sort()) === JSON.stringify(['org_read', 'org_write']), JSON.stringify(item1?.scopes))

// ---- key 直连自省（协作面 /api/subsys/system/info）----
check('SAA-S7 无 key 自省 401', (await api('GET', '/api/subsys/system/info')).status === 401)
check('SAA-S8 伪造 key 自省 401', (await api('GET', '/api/subsys/system/info', { token: 'moai-sa-forged' })).status === 401)
check('SAA-S8a 非法前缀（团队 key）自省 401', (await api('GET', '/api/subsys/system/info', { token: 'moai-ac-xxxx' })).status === 401)

const info1 = await api('GET', '/api/subsys/system/info', { token: APP_KEY })
check('SAA-S9 正确 key 自省回显身份与范围', info1.status === 200 && info1.json.id === APP_ID
  && JSON.stringify(info1.json.scopes.sort()) === JSON.stringify(['org_read', 'org_write']) && info1.json.status === 'enabled',
  info1.text.slice(0, 160))

// ---- 更新即时生效 ----
const upd = await api('PUT', `/api/subsystem/system-app/${APP_ID}`, {
  token: root,
  body: { name: `ERP 同步改 ${TS}`, description: 'x', scopes: ['user_read', 'user_write'], status: 'enabled' },
})
check('SAA-S10 更新名称与范围 200', upd.status === 200, `${upd.status}`)
const info2 = await api('GET', '/api/subsys/system/info', { token: APP_KEY })
check('SAA-S10a 自省即时回显新范围', info2.status === 200 && JSON.stringify(info2.json.scopes.sort()) === JSON.stringify(['user_read', 'user_write']),
  info2.text.slice(0, 160))

const updMissing = await api('PUT', `/api/subsystem/system-app/00000000-0000-0000-0000-000000000000`, {
  token: root, body: { name: 'x', scopes: ['org_read'], status: 'enabled' },
})
check('SAA-S11 更新不存在 404', updMissing.status === 404, `${updMissing.status}`)

// ---- 停用 / 启用 ----
await api('PUT', `/api/subsystem/system-app/${APP_ID}`, { token: root, body: { name: `ERP 同步改 ${TS}`, scopes: ['user_read'], status: 'disabled' } })
const info3 = await api('GET', '/api/subsys/system/info', { token: APP_KEY })
check('SAA-S12 停用后自省 403', info3.status === 403, `${info3.status}`)

await api('PUT', `/api/subsystem/system-app/${APP_ID}`, { token: root, body: { name: `ERP 同步改 ${TS}`, scopes: ['user_read'], status: 'enabled' } })
const info4 = await api('GET', '/api/subsys/system/info', { token: APP_KEY })
check('SAA-S13 重新启用自省恢复 200', info4.status === 200, `${info4.status}`)

// ================= 以下为统一表 + 密钥/token + 系统资源接口场景（@SAA-S17..）=================

// ---- 全范围接入：appid+密钥与 token 双轨 ----
const created2 = await api('POST', '/api/subsystem/system-app/create', { token: root, body: { name: `全资源-${TS}`, scopes: ['org_read', 'org_write', 'user_read', 'user_write', 'team_read', 'team_write'] } })
const APP2_ID = created2.json?.id
const APP2_KEY = created2.json?.key
const APP2_SECRET = created2.json?.secret
check('SAA-S17 创建返回一次性 appid+密钥', created2.status === 200 && APP2_KEY?.startsWith('moai-sa-') && typeof APP2_SECRET === 'string' && APP2_SECRET.length === 48, `secret len=${APP2_SECRET?.length}`)
const infoD = await api('GET', '/api/subsys/system/info', { token: APP2_KEY })
check('SAA-S18 appid 直连自省 200（双轨·直连）', infoD.status === 200 && infoD.json.id === APP2_ID, `${infoD.status}`)

check('SAA-S19 错误密钥换取 token 401', (await api('POST', '/api/subsys/system/token', { body: { appId: APP2_KEY, secret: 'wrong-secret' } })).status === 401)
const tok1 = await api('POST', '/api/subsys/system/token', { body: { appId: APP2_KEY, secret: APP2_SECRET } })
check(
  'SAA-S20 正确密钥换取 access+refresh',
  tok1.status === 200 && !!tok1.json?.accessToken && !!tok1.json?.refreshToken && tok1.json.expiresIn > 0,
  `${tok1.status} ${tok1.text.slice(0, 120)}`,
)
const infoT = await api('GET', '/api/subsys/system/info', { token: tok1.json?.accessToken })
check('SAA-S21 token 轨自省 200（双轨·token）', infoT.status === 200 && infoT.json.id === APP2_ID, `${infoT.status}`)
const usersByToken = await api('GET', '/api/subsys/system/users/list?pageNo=1&pageSize=5', { token: tok1.json?.accessToken })
check('SAA-S22 token 轨访问系统资源接口 200', usersByToken.status === 200 && Array.isArray(usersByToken.json?.items), `${usersByToken.status} ${usersByToken.text.slice(0, 120)}`)
const tokForge = await api('POST', '/api/subsys/system/token/refresh', { body: { refreshToken: 'not-a-jwt' } })
check('SAA-S23 伪造 refresh token 401', tokForge.status === 401, `${tokForge.status}`)
const tok2 = await api('POST', '/api/subsys/system/token/refresh', { body: { refreshToken: tok1.json?.refreshToken } })
check(
  'SAA-S24 refresh 轮换新对且新 access 可用',
  tok2.status === 200 && !!tok2.json?.accessToken && tok2.json.accessToken !== tok1.json.accessToken
    && (await api('GET', '/api/subsys/system/info', { token: tok2.json.accessToken })).status === 200,
  `${tok2.status} ${tok2.text.slice(0, 120)}`,
)

// ---- 停用即时拒绝（双轨）----
await api('PUT', `/api/subsystem/system-app/${APP2_ID}`, { token: root, body: { name: `全资源-${TS}`, scopes: ['org_read', 'org_write', 'user_read', 'user_write', 'team_read', 'team_write'], status: 'disabled' } })
const infoDisK = await api('GET', '/api/subsys/system/info', { token: APP2_KEY })
const infoDisT = await api('GET', '/api/subsys/system/info', { token: tok2.json?.accessToken })
check('SAA-S25 停用后直连与 token 双轨均 403', infoDisK.status === 403 && infoDisT.status === 403, `key=${infoDisK.status} token=${infoDisT.status}`)
await api('PUT', `/api/subsystem/system-app/${APP2_ID}`, { token: root, body: { name: `全资源-${TS}`, scopes: ['org_read', 'org_write', 'user_read', 'user_write', 'team_read', 'team_write'], status: 'enabled' } })
check('SAA-S26 重新启用后 token 轨恢复 200', (await api('GET', '/api/subsys/system/info', { token: tok2.json?.accessToken })).status === 200)

// ---- 重置密钥：旧密钥换取立即 401 ----
const reset2 = await api('POST', `/api/subsystem/system-app/${APP2_ID}/reset-secret`, { token: root })
check('SAA-S27 重置密钥返回新密钥', reset2.status === 200 && !!reset2.json?.secret && reset2.json.secret !== APP2_SECRET && reset2.json.secret.length === 48, `${reset2.status}`)
check('SAA-S28 旧密钥换取 token 401', (await api('POST', '/api/subsys/system/token', { body: { appId: APP2_KEY, secret: APP2_SECRET } })).status === 401)
check('SAA-S29 新密钥换取 token 200', (await api('POST', '/api/subsys/system/token', { body: { appId: APP2_KEY, secret: reset2.json?.secret } })).status === 200)

// ---- 只读范围接入：scope 分档门禁 ----
const createdRo = await api('POST', '/api/subsystem/system-app/create', { token: root, body: { name: `只读-${TS}`, scopes: ['org_read'] } })
const RO_KEY = createdRo.json?.key
check('SAA-S30 只读接入读组织角色 200', (await api('GET', '/api/subsys/system/org/roles', { token: RO_KEY })).status === 200)
const roWrite = await api('POST', '/api/subsys/system/users', { token: RO_KEY, body: { userName: uname('denied') } })
check('SAA-S31 只读接入写用户 403', roWrite.status === 403, `${roWrite.status} ${roWrite.text.slice(0, 100)}`)
const roToken = await api('POST', '/api/subsys/system/token', { body: { appId: RO_KEY, secret: createdRo.json?.secret } })
const roTeamByToken = await api('POST', '/api/subsys/system/teams', { token: roToken.json?.accessToken, body: { name: uname('deniedteam') } })
check('SAA-S32 只读接入 token 轨写团队 403', roTeamByToken.status === 403, `${roTeamByToken.status}`)
const roNoScope = await api('POST', '/api/subsys/system/org/roles', { token: RO_KEY, body: { name: 'x' } })
check('SAA-S33 只读接入写组织 403', roNoScope.status === 403, `${roNoScope.status}`)

// ---- 用户 upsert（ERP 推送主链路）----
const erpName = uname('erp')
const up1 = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: erpName, nickName: 'ERP张三', email: `${erpName}@test.local` } })
check(
  'SAA-S34 upsert 创建返回初始密码（未提供密码时自动生成）',
  up1.status === 200 && up1.json?.created === true && Number(up1.json?.userId) > 0 && typeof up1.json?.initialPassword === 'string' && up1.json.initialPassword.length === 12,
  `${up1.status} ${up1.text.slice(0, 140)}`,
)
const ERP_UID = Number(up1.json?.userId)
const erpLogin = await api('POST', '/api/auth/login', { body: { userName: erpName, password: rsa(up1.json?.initialPassword) } })
check('SAA-S35 初始密码可登录', erpLogin.status === 200 && !!erpLogin.json?.accessToken, `${erpLogin.status}`)

const up2 = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: erpName, nickName: 'ERP张三改' } })
check(
  'SAA-S36 同 userName 重复推送幂等更新',
  up2.status === 200 && up2.json?.created === false && Number(up2.json?.userId) === ERP_UID && up2.json?.initialPassword == null,
  `${up2.status} ${up2.text.slice(0, 120)}`,
)
const upBad = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: uname('erpx'), email: 'not-an-email' } })
check('SAA-S37 非法邮箱 400', upBad.status === 400, `${upBad.status}`)
const conflictEmail = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: uname('erpy'), email: `${erpName}@test.local` } })
check('SAA-S38 邮箱与他用户冲突 409', conflictEmail.status === 409, `${conflictEmail.status} ${conflictEmail.text.slice(0, 100)}`)
const badPwd = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: uname('erpz'), password: 'short' } })
check('SAA-S39 弱密码 400', badPwd.status === 400, `${badPwd.status}`)
const rootPush = await api('POST', '/api/subsys/system/users', { token: APP2_KEY, body: { userName: 'admin', isDisable: true } })
check('SAA-S40 upsert 禁用 root 受保护 400', rootPush.status === 400, `${rootPush.status} ${rootPush.text.slice(0, 100)}`)

const dis = await api('PUT', `/api/subsys/system/users/${ERP_UID}/disable`, { token: APP2_KEY, body: { isDisable: true } })
const disLogin = await api('POST', '/api/auth/login', { body: { userName: erpName, password: rsa(up1.json?.initialPassword) } })
check('SAA-S41 禁用后登录被拒', dis.status === 200 && disLogin.status !== 200, `dis=${dis.status} login=${disLogin.status}`)
const en = await api('PUT', `/api/subsys/system/users/${ERP_UID}/disable`, { token: APP2_KEY, body: { isDisable: false } })
check('SAA-S42 重新启用 200', en.status === 200, `${en.status}`)

// ---- 组织角色（复用 Org 领域命令）----
const roleRoot = await api('POST', '/api/subsys/system/org/roles', { token: APP2_KEY, body: { name: `ERP研发部-${TS}` } })
check('SAA-S43 创建根角色', roleRoot.status === 200 && Number(roleRoot.json?.orgRoleId) > 0, `${roleRoot.status} ${roleRoot.text.slice(0, 100)}`)
const ROLE_ID = Number(roleRoot.json?.orgRoleId)
const roleSub = await api('POST', '/api/subsys/system/org/roles', { token: APP2_KEY, body: { name: `ERP后端组-${TS}`, parentId: ROLE_ID } })
const SUB_ROLE_ID = Number(roleSub.json?.orgRoleId)
const rolesList = await api('GET', '/api/subsys/system/org/roles', { token: APP2_KEY })
const rootItem = (rolesList.json?.items ?? []).find((x) => x.orgRoleId === ROLE_ID)
const subItem = (rolesList.json?.items ?? []).find((x) => x.orgRoleId === SUB_ROLE_ID)
check('SAA-S44 列表回显父子层级（path 物化）', rolesList.status === 200 && !!rootItem && !!subItem && subItem.parentId === ROLE_ID && subItem.path.startsWith(rootItem.path), JSON.stringify({ root: rootItem?.path, sub: subItem?.path }).slice(0, 160))
const roleDup = await api('POST', '/api/subsys/system/org/roles', { token: APP2_KEY, body: { name: `ERP研发部-${TS}` } })
check('SAA-S45 同父重名 409', roleDup.status === 409, `${roleDup.status}`)
const assign = await api('PUT', `/api/subsys/system/org/users/${ERP_UID}/roles`, { token: APP2_KEY, body: { roleIds: [ROLE_ID, SUB_ROLE_ID] } })
const roleUsers = await api('GET', `/api/subsys/system/org/roles/${ROLE_ID}/users`, { token: APP2_KEY })
check(
  'SAA-S46 分配用户角色并回显',
  assign.status === 200 && roleUsers.status === 200 && (roleUsers.json?.items ?? []).some((x) => Number(x.userId) === ERP_UID),
  `assign=${assign.status} roleUsers=${roleUsers.status} ${roleUsers.text.slice(0, 120)}`,
)
const userRoles = await api('GET', `/api/subsys/system/org/users/${ERP_UID}/roles`, { token: APP2_KEY })
const userRoleCount = (userRoles.json?.roles ?? userRoles.json?.items ?? []).length
check('SAA-S47 查询用户持有角色', userRoles.status === 200 && userRoleCount === 2, `${userRoles.status} ${userRoles.text.slice(0, 140)}`)
const delRoleHasChild = await api('DELETE', `/api/subsys/system/org/roles/${ROLE_ID}`, { token: APP2_KEY })
check('SAA-S48 删除有子角色的角色被拒 400', delRoleHasChild.status === 400, `${delRoleHasChild.status}`)
const clearRoles = await api('PUT', `/api/subsys/system/org/users/${ERP_UID}/roles`, { token: APP2_KEY, body: { roleIds: [] } })
const delSubRole = await api('DELETE', `/api/subsys/system/org/roles/${SUB_ROLE_ID}`, { token: APP2_KEY })
check('SAA-S49 清空用户角色后删除子角色 200', clearRoles.status === 200 && delSubRole.status === 200, `clear=${clearRoles.status} del=${delSubRole.status}`)

// ---- 团队与成员 ----
const teamName = `ERP项目组-${TS}`
const team1 = await api('POST', '/api/subsys/system/teams', { token: APP2_KEY, body: { name: teamName, description: 'ERP 推送创建' } })
check('SAA-S50 系统创建团队', team1.status === 200 && Number(team1.json?.value) > 0, `${team1.status} ${team1.text.slice(0, 100)}`)
const TID = Number(team1.json?.value)
const teamDup = await api('POST', '/api/subsys/system/teams', { token: APP2_KEY, body: { name: teamName } })
check('SAA-S51 团队重名 409', teamDup.status === 409, `${teamDup.status}`)
const teamUpd = await api('PUT', `/api/subsys/system/teams/${TID}`, { token: APP2_KEY, body: { description: 'ERP 推送创建-已更新' } })
check('SAA-S52 更新团队 200', teamUpd.status === 200, `${teamUpd.status}`)
const add1 = await api('POST', `/api/subsys/system/teams/${TID}/users`, { token: APP2_KEY, body: { userId: ERP_UID, role: 0 } })
const add2 = await api('POST', `/api/subsys/system/teams/${TID}/users`, { token: APP2_KEY, body: { userId: ERP_UID, role: 1 } })
check('SAA-S53 添加成员幂等 upsert（重复推送更新角色）', add1.status === 200 && add2.status === 200, `add1=${add1.status} add2=${add2.status} ${add2.text.slice(0, 100)}`)
const erpTeams = await api('GET', '/api/team/list', { token: erpLogin.json?.accessToken })
check('SAA-S54 成员视角可见系统创建的团队', erpTeams.status === 200 && JSON.stringify(erpTeams.json).includes(teamName), `${erpTeams.status} ${JSON.stringify(erpTeams.json).slice(0, 120)}`)
const rm1 = await api('DELETE', `/api/subsys/system/teams/${TID}/users/${ERP_UID}`, { token: APP2_KEY })
const rm2 = await api('DELETE', `/api/subsys/system/teams/${TID}/users/${ERP_UID}`, { token: APP2_KEY })
check('SAA-S55 移除成员后再移除 404', rm1.status === 200 && rm2.status === 404, `rm1=${rm1.status} rm2=${rm2.status}`)
const teamsList = await api('GET', '/api/subsys/system/teams/list', { token: APP2_KEY })
check('SAA-S56 团队列表（team_read）回显新建团队', teamsList.status === 200 && (teamsList.json?.items ?? []).some((x) => Number(x.teamId ?? x.id) === TID), `${teamsList.status} ${teamsList.text.slice(0, 140)}`)

// ---- 清理：删除全资源与只读接入，双轨与 refresh 全失效 ----
const del2 = await api('DELETE', `/api/subsystem/system-app/${APP2_ID}`, { token: root })
const infoDelT = await api('GET', '/api/subsys/system/info', { token: tok2.json?.accessToken })
const tokDel = await api('POST', '/api/subsys/system/token', { body: { appId: APP2_KEY, secret: reset2.json?.secret } })
const refDel = await api('POST', '/api/subsys/system/token/refresh', { body: { refreshToken: tok2.json?.refreshToken } })
check('SAA-S57 删除后 token 轨 401、换取 401、refresh 401', del2.status === 200 && infoDelT.status === 401 && tokDel.status === 401 && refDel.status === 401, `del=${del2.status} info=${infoDelT.status} tok=${tokDel.status} ref=${refDel.status}`)
await api('DELETE', `/api/subsystem/system-app/${createdRo.json?.id}`, { token: root })

// ---- 搜索 ----
const search = await api('GET', `/api/subsystem/system-app/list?search=${encodeURIComponent(APP_KEY.slice(0, 12))}`, { token: root })
check('SAA-S14 按前缀搜索命中', search.status === 200 && (search.json?.items ?? []).some((x) => x.id === APP_ID), search.text.slice(0, 160))

// ---- 删除即失效 ----
const del = await api('DELETE', `/api/subsystem/system-app/${APP_ID}`, { token: root })
const info5 = await api('GET', '/api/subsys/system/info', { token: APP_KEY })
check('SAA-S15 删除后 key 立即失效 401', del.status === 200 && info5.status === 401, `del=${del.status} info=${info5.status}`)
const list2 = await api('GET', '/api/subsystem/system-app/list', { token: root })
check('SAA-S16 删除后列表不可见', !(list2.json?.items ?? []).some((x) => x.id === APP_ID))

console.log(`\n结果：${PASS} 通过 / ${FAIL} 失败`)
process.exit(FAIL === 0 ? 0 : 1)
