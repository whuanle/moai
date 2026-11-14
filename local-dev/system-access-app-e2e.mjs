// 应用接入（系统级接入 key）· E2E（场景 @SAA-S1..，合并模型后）
// 覆盖：管理面门禁 / 创建与一次性 appid+密钥 / 列表回显（仅前缀）/ appid 直连与 token 双轨自省 /
//       更新与状态即时生效 / 停用 403 / 删除即失效 / 搜索 /
//       资源协作面全量访问（scope 分档已下线，原 SAA-S4/S4a/S6a/S10a/S18/S30~S33 退役）/
//       子系统 token 互通（两类接入同一套接口）/ 用户 upsert 推送主链路 / 组织角色 / 团队治理。
// 前置：后端运行中；种子账号 admin / abcd123456（root）。全部用例零真实外部依赖。
// 用法：node local-dev/system-access-app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 SAA_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.SAA_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

// 密钥回显（RSA+AES 信封）：随机 AES-256 key 经 RSA 公钥加密上送，响应 GCM 密文本地解出原文
async function revealSecret(rootToken, id) {
  const { publicEncrypt, createPublicKey, createDecipheriv, randomBytes } = await import('node:crypto')
  const pub = createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  const envelopeKey = randomBytes(32)
  const encryptedKey = publicEncrypt({ key: pub, padding: 1 /* RSA_PKCS1_PADDING */ }, Buffer.from(envelopeKey.toString('base64'), 'utf8')).toString('base64')
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

// ---- 管理面门禁（合并后管理入口 /api/subsystem/*）----
check('SAA-S1 未登录查询列表 401', (await api('GET', '/api/subsystem/list')).status === 401)
check('SAA-S2 非 root 查询列表 403', (await api('GET', '/api/subsystem/list', { token: user })).status === 403)
check('SAA-S3 非 root 创建 403', (await api('POST', '/api/subsystem/create', { token: user, body: { appType: 'app', name: 'x' } })).status === 403)

// ---- 创建与一次性 appid（密钥 AES 可逆存储，仅回掩码；原文信封可回显）----
const created = await api('POST', '/api/subsystem/create', {
  token: root,
  body: { appType: 'app', name: `ERP 同步 ${TS}`, description: '同步 ERP 组织角色' },
})
check('SAA-S5 root 创建返回 appid 与密钥掩码（不回原文）', created.status === 200 && created.json?.appType === 'app' && typeof created.json.key === 'string' && created.json.key.startsWith('moai-sa-') && created.json.key.length === 35 && created.json.secretMask?.includes('****') && !('secret' in (created.json ?? {})),
  created.text.slice(0, 200))
const APP_KEY = created.json.key
const APP_ID = created.json.id
const APP_SECRET = await revealSecret(root, APP_ID)

const list1 = await api('GET', '/api/subsystem/list', { token: root })
const item1 = (list1.json?.items ?? []).find((x) => x.id === APP_ID)
check('SAA-S6 列表回显前缀而非原文', list1.status === 200 && item1 && item1.keyPrefix === APP_KEY.slice(0, 12) && !JSON.stringify(item1).includes(APP_KEY),
  JSON.stringify(item1 ?? null).slice(0, 160))

// ---- appid 直连自省（协作面统一入口 GET /api/subsys/info）----
check('SAA-S7 无 key 自省 401', (await api('GET', '/api/subsys/info')).status === 401)
check('SAA-S8 伪造 key 自省 401', (await api('GET', '/api/subsys/info', { token: 'moai-sa-forged' })).status === 401)
check('SAA-S8a 非法前缀（团队 key）自省 401', (await api('GET', '/api/subsys/info', { token: 'moai-ac-xxxx' })).status === 401)

const info1 = await api('GET', '/api/subsys/info', { token: APP_KEY })
check('SAA-S9 正确 key 自省回显身份（类型/状态，无资源范围）', info1.status === 200 && info1.json.id === APP_ID
  && info1.json.appType === 'app' && info1.json.status === 'enabled' && !('scopes' in (info1.json ?? {})),
  info1.text.slice(0, 160))

// ---- 更新即时生效 ----
const upd = await api('PUT', `/api/subsystem/${APP_ID}`, {
  token: root,
  body: { name: `ERP 同步改 ${TS}`, description: 'x', status: 'enabled' },
})
check('SAA-S10 更新名称 200（应用接入行仅名称/描述/状态）', upd.status === 200, `${upd.status}`)
const info2 = await api('GET', '/api/subsys/info', { token: APP_KEY })
check('SAA-S10a 自省即时回显新名称', info2.status === 200 && info2.json.name === `ERP 同步改 ${TS}`,
  info2.text.slice(0, 160))

const updMissing = await api('PUT', `/api/subsystem/00000000-0000-0000-0000-000000000000`, {
  token: root, body: { name: 'x', status: 'enabled' },
})
check('SAA-S11 更新不存在 404', updMissing.status === 404, `${updMissing.status}`)

// ---- 停用 / 启用 ----
await api('PUT', `/api/subsystem/${APP_ID}`, { token: root, body: { name: `ERP 同步改 ${TS}`, status: 'disabled' } })
const info3 = await api('GET', '/api/subsys/info', { token: APP_KEY })
check('SAA-S12 停用后自省 403', info3.status === 403, `${info3.status}`)

await api('PUT', `/api/subsystem/${APP_ID}`, { token: root, body: { name: `ERP 同步改 ${TS}`, status: 'enabled' } })
const info4 = await api('GET', '/api/subsys/info', { token: APP_KEY })
check('SAA-S13 重新启用自省恢复 200', info4.status === 200, `${info4.status}`)

// ================= 统一表 + 密钥/token 双轨 + 资源协作面全量访问（@SAA-S17..）=================

// ---- appid+密钥与 token 双轨（统一 /api/subsys/token）----
const created2 = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'app', name: `全资源-${TS}` } })
const APP2_ID = created2.json?.id
const APP2_KEY = created2.json?.key
const APP2_SECRET = await revealSecret(root, APP2_ID)
check('SAA-S17 创建返回 appid+掩码，信封回显 48 位原文', created2.status === 200 && APP2_KEY?.startsWith('moai-sa-') && typeof APP2_SECRET === 'string' && APP2_SECRET.length === 48, `secret len=${APP2_SECRET?.length}`)
const infoD = await api('GET', '/api/subsys/info', { token: APP2_KEY })
check('SAA-S18 appid 直连自省 200（双轨·直连）', infoD.status === 200 && infoD.json.id === APP2_ID, `${infoD.status}`)

check('SAA-S19 错误密钥换取 token 401', (await api('POST', '/api/subsys/token', { body: { appId: APP2_KEY, secret: 'wrong-secret' } })).status === 401)
const tok1 = await api('POST', '/api/subsys/token', { body: { appId: APP2_KEY, secret: APP2_SECRET } })
check(
  'SAA-S20 正确密钥换取 access+refresh',
  tok1.status === 200 && !!tok1.json?.accessToken && !!tok1.json?.refreshToken && tok1.json.expiresIn > 0,
  `${tok1.status} ${tok1.text.slice(0, 120)}`,
)
const infoT = await api('GET', '/api/subsys/info', { token: tok1.json?.accessToken })
check('SAA-S21 token 轨自省 200（双轨·token）', infoT.status === 200 && infoT.json.id === APP2_ID, `${infoT.status}`)
const usersByToken = await api('GET', '/api/subsys/system/users/list?pageNo=1&pageSize=5', { token: tok1.json?.accessToken })
check('SAA-S22 token 轨访问系统资源接口 200', usersByToken.status === 200 && Array.isArray(usersByToken.json?.items), `${usersByToken.status} ${usersByToken.text.slice(0, 120)}`)
const tokForge = await api('POST', '/api/subsys/token/refresh', { body: { refreshToken: 'not-a-jwt' } })
check('SAA-S23 伪造 refresh token 401', tokForge.status === 401, `${tokForge.status}`)
const tok2 = await api('POST', '/api/subsys/token/refresh', { body: { refreshToken: tok1.json?.refreshToken } })
check(
  'SAA-S24 refresh 轮换新对且新 access 可用',
  tok2.status === 200 && !!tok2.json?.accessToken && tok2.json.accessToken !== tok1.json.accessToken
    && (await api('GET', '/api/subsys/info', { token: tok2.json.accessToken })).status === 200,
  `${tok2.status} ${tok2.text.slice(0, 120)}`,
)

// ---- 停用即时拒绝（双轨）----
await api('PUT', `/api/subsystem/${APP2_ID}`, { token: root, body: { name: `全资源-${TS}`, status: 'disabled' } })
const infoDisK = await api('GET', '/api/subsys/info', { token: APP2_KEY })
const infoDisT = await api('GET', '/api/subsys/info', { token: tok2.json?.accessToken })
check('SAA-S25 停用后直连与 token 双轨均 403', infoDisK.status === 403 && infoDisT.status === 403, `key=${infoDisK.status} token=${infoDisT.status}`)
await api('PUT', `/api/subsystem/${APP2_ID}`, { token: root, body: { name: `全资源-${TS}`, status: 'enabled' } })
check('SAA-S26 重新启用后 token 轨恢复 200', (await api('GET', '/api/subsys/info', { token: tok2.json?.accessToken })).status === 200)

// ---- 重置密钥：旧密钥换取立即 401 ----
const reset2 = await api('POST', `/api/subsystem/${APP2_ID}/reset-secret`, { token: root })
const NEW_SECRET2 = await revealSecret(root, APP2_ID)
check('SAA-S27 重置密钥返回新掩码且信封回显新原文', reset2.status === 200 && reset2.json?.secretMask?.includes('****') && NEW_SECRET2.length === 48 && NEW_SECRET2 !== APP2_SECRET, `${reset2.status}`)
check('SAA-S28 旧密钥换取 token 401', (await api('POST', '/api/subsys/token', { body: { appId: APP2_KEY, secret: APP2_SECRET } })).status === 401)
check('SAA-S29 新密钥换取 token 200', (await api('POST', '/api/subsys/token', { body: { appId: APP2_KEY, secret: NEW_SECRET2 } })).status === 200)

// ---- 资源协作面全量访问（scope 分档下线，原只读 403 用例退役）----
const fullOrgCreate = await api('POST', '/api/subsys/system/org/roles', { token: APP2_KEY, body: { name: `全量组织-${TS}` } })
check('SAA-S58 直连写组织角色 200（无范围限制）', fullOrgCreate.status === 200 && Number(fullOrgCreate.json?.orgRoleId) > 0, `${fullOrgCreate.status} ${fullOrgCreate.text.slice(0, 100)}`)
const fullOrgDel = fullOrgCreate.status === 200 ? await api('DELETE', `/api/subsys/system/org/roles/${fullOrgCreate.json?.orgRoleId}`, { token: APP2_KEY }) : null
check('SAA-S59 直连删组织角色 200', fullOrgDel?.status === 200, `del=${fullOrgDel?.status}`)

// ---- 子系统 token 互通：两类接入同一套接口访问资源面 ----
const SUB_KEY = `interop${TS}`
const subCreated = await api('POST', '/api/subsystem/create', { token: root, body: { appType: 'subsystem', key: SUB_KEY, name: `互通子系统-${TS}`, teamScope: 'allTeams' } })
const SUB_SECRET = await revealSecret(root, subCreated.json?.id)
const subToken = await api('POST', '/api/subsys/token', { body: { appId: SUB_KEY, secret: SUB_SECRET } })
const subUsers = await api('GET', '/api/subsys/system/users/list?pageNo=1&pageSize=5', { token: subToken.json?.accessToken })
check('SAA-S60 子系统 token 访问同一资源面 200（同一套接口）', subUsers.status === 200 && Array.isArray(subUsers.json?.items), `${subUsers.status} ${subUsers.text.slice(0, 120)}`)

// ---- 应用接入心跳与自省在线（合并模型后同一心跳端点）----
const appHb = await api('POST', '/api/subsys/heartbeat', { token: APP2_KEY, body: { instanceId: `saa-inst-${TS}` } })
const appHbInfo = appHb.status === 200 ? await api('GET', '/api/subsys/info', { token: APP2_KEY }) : null
check('SAA-S61 应用接入心跳 200 且自省在线', appHb.status === 200 && appHbInfo?.json?.online === true && Number(appHbInfo?.json?.onlineInstances) === 1, `hb=${appHb.status} info=${appHbInfo?.status}`)

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
check('SAA-S56 团队列表回显新建团队', teamsList.status === 200 && (teamsList.json?.items ?? []).some((x) => Number(x.teamId ?? x.id) === TID), `${teamsList.status} ${teamsList.text.slice(0, 140)}`)

// ---- 清理：删除接入，双轨与 refresh 全失效 ----
const del2 = await api('DELETE', `/api/subsystem/${APP2_ID}`, { token: root })
const infoDelT = await api('GET', '/api/subsys/info', { token: tok2.json?.accessToken })
const tokDel = await api('POST', '/api/subsys/token', { body: { appId: APP2_KEY, secret: NEW_SECRET2 } })
const refDel = await api('POST', '/api/subsys/token/refresh', { body: { refreshToken: tok2.json?.refreshToken } })
check('SAA-S57 删除后 token 轨 401、换取 401、refresh 401', del2.status === 200 && infoDelT.status === 401 && tokDel.status === 401 && refDel.status === 401, `del=${del2.status} info=${infoDelT.status} tok=${tokDel.status} ref=${refDel.status}`)
await api('DELETE', `/api/subsystem/${subCreated.json?.id}`, { token: root })

// ---- 搜索 ----
const search = await api('GET', `/api/subsystem/list?search=${encodeURIComponent(APP_KEY.slice(0, 12))}`, { token: root })
check('SAA-S14 按前缀搜索命中', search.status === 200 && (search.json?.items ?? []).some((x) => x.id === APP_ID), search.text.slice(0, 160))

// ---- 删除即失效 ----
const del = await api('DELETE', `/api/subsystem/${APP_ID}`, { token: root })
const info5 = await api('GET', '/api/subsys/info', { token: APP_KEY })
check('SAA-S15 删除后 key 立即失效 401', del.status === 200 && info5.status === 401, `del=${del.status} info=${info5.status}`)
const list2 = await api('GET', '/api/subsystem/list', { token: root })
check('SAA-S16 删除后列表不可见', !(list2.json?.items ?? []).some((x) => x.id === APP_ID))

console.log(`\n结果：${PASS} 通过 / ${FAIL} 失败`)
process.exit(FAIL === 0 ? 0 : 1)
