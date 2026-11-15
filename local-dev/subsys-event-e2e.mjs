// 系统级接入事件推送 E2E（@SUB-S76 起）
// 覆盖：/hub/subsys 机器 token 建连 / onReady / 用户事件 / 组织事件 / 团队事件 /
//       grants 对账 / events REST 补发 / 停用最终事件 / 停用后 REST 拒绝。
// 前置：后端运行中（默认 http://127.0.0.1:5210，可用 SUBSYS_BASE 覆盖）。
import crypto from 'node:crypto'
import { createRequire } from 'node:module'

const require = createRequire(new URL('../ui/', import.meta.url))
const signalR = require('@microsoft/signalr')

const BASE = process.env.SUBSYS_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5210'
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

const TS = Date.now().toString().slice(-8)
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (rootLogin.status !== 200) throw new Error(`admin 登录失败: ${rootLogin.status} ${rootLogin.text.slice(0, 160)}`)
  const root = rootLogin.json.accessToken

  const appKey = `evt${TS}`
  const created = await api('POST', '/api/subsystem/create', {
    token: root,
    body: { appType: 'subsystem', key: appKey, name: `事件子系统-${TS}`, teamScope: 'allTeams' },
  })
  check('EVT-01 创建接入', created.status === 200 && !!created.json?.id, `${created.status} ${created.text.slice(0, 160)}`)
  const appId = created.json?.id
  const secret = await revealSecret(root, appId)

  const tokenRes = await api('POST', '/api/subsys/token', { body: { appId: appKey, secret } })
  check('EVT-02 换取机器 token', tokenRes.status === 200 && !!tokenRes.json?.accessToken, `${tokenRes.status} ${tokenRes.text.slice(0, 120)}`)
  const token = tokenRes.json?.accessToken

  const events = []
  let ready = null
  let resync = []
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE}/hub/subsys`, { accessTokenFactory: () => token })
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  connection.on('onEvent', (e) => { events.push(e) })
  connection.on('onReady', (r) => { ready = r })
  connection.on('onEvent', (e) => { if (e.event === 'subsys.resync_required') resync.push(e) })
  await connection.start()
  for (let i = 0; i < 50 && !ready; i++) await sleep(100)
  check('EVT-03 Hub 建连并收到 onReady（首次需全量对账）', !!ready && ready.requiresFullSync === true, JSON.stringify(ready))

  const expectConnectFail = async (accessToken) => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE}/hub/subsys`, { accessTokenFactory: () => accessToken })
      .configureLogging(signalR.LogLevel.None)
      .build()
    try {
      await conn.start()
      await conn.stop()
      return false
    } catch {
      return true
    }
  }

  check('EVT-26 用户 JWT 不能连事件 Hub', await expectConnectFail(root))
  check('EVT-27 团队级 key 不能连事件 Hub', await expectConnectFail('moai-ac-fake-key'))

  const waitEvent = async (name, timeout = 8000) => {
    const start = Date.now()
    while (Date.now() - start < timeout) {
      const found = events.find((x) => x.event === name)
      if (found) return found
      await sleep(100)
    }
    return null
  }

  const userName = `evtuser${TS}`
  const upsert = await api('POST', '/api/subsys/system/users', {
    token,
    body: { userName, nickName: `事件用户-${TS}` },
  })
  check('EVT-04 系统 upsert 建户', upsert.status === 200 && Number(upsert.json?.userId) > 0, `${upsert.status} ${upsert.text.slice(0, 160)}`)
  const userId = Number(upsert.json?.userId)
  const createdEvent = await waitEvent('subsys.user.created')
  check('EVT-05 收到 user.created（只带主体 ID）', !!createdEvent && createdEvent.data?.userId === String(userId) && !('email' in (createdEvent.data ?? {})), JSON.stringify(createdEvent))

  const update = await api('POST', '/api/subsys/system/users', {
    token,
    body: { userName, nickName: `事件用户改-${TS}` },
  })
  const updatedEvent = await waitEvent('subsys.user.updated')
  check('EVT-06 收到 user.updated（fields 元数据）', update.status === 200 && !!updatedEvent && Array.isArray(updatedEvent.data?.fields) && updatedEvent.data.fields.includes('nickName'), JSON.stringify(updatedEvent))

  const disable = await api('PUT', `/api/subsys/system/users/${userId}/disable`, { token, body: { isDisable: true } })
  const statusEvent = await waitEvent('subsys.user.status_changed')
  check('EVT-07 收到 user.status_changed(disabled)', disable.status === 200 && statusEvent?.data?.disabled === true, JSON.stringify(statusEvent))

  const role = await api('POST', '/api/subsys/system/org/roles', { token, body: { name: `事件组织-${TS}` } })
  const roleId = Number(role.json?.orgRoleId)
  check('EVT-08 创建组织角色', role.status === 200 && roleId > 0, `${role.status} ${role.text.slice(0, 160)}`)
  check('EVT-09 收到 org.role_created', !!(await waitEvent('subsys.org.role_created')))

  const assign = await api('PUT', `/api/subsys/system/org/users/${userId}/roles`, { token, body: { roleIds: [roleId] } })
  const orgRolesEvent = await waitEvent('subsys.user.org_roles_changed')
  check('EVT-10 收到 user.org_roles_changed（关联 roleIds）', assign.status === 200 && orgRolesEvent?.data?.roleIds?.includes(String(roleId)), JSON.stringify(orgRolesEvent))

  const team = await api('POST', '/api/subsys/system/teams', { token, body: { name: `事件团队-${TS}`, description: 'event e2e' } })
  const teamId = Number(team.json?.value)
  check('EVT-11 创建团队', team.status === 200 && teamId > 0, `${team.status} ${team.text.slice(0, 160)}`)
  check('EVT-12 收到 team.created', !!(await waitEvent('subsys.team.created')))

  const addMember = await api('POST', `/api/subsys/system/teams/${teamId}/users`, { token, body: { userId, role: 0 } })
  const memberEvent = await waitEvent('subsys.team.member_added')
  check('EVT-13 收到 team.member_added（userIds + role=member）', addMember.status === 200 && memberEvent?.data?.userIds?.includes(String(userId)) && memberEvent?.data?.role === 'member', JSON.stringify(memberEvent))

  const members = await api('GET', `/api/subsys/system/teams/${teamId}/users`, { token })
  check('EVT-14 团队成员对账接口', members.status === 200 && (members.json?.items ?? []).some((x) => Number(x.userId) === userId), `${members.status} ${members.text.slice(0, 160)}`)

  const grants = await api('GET', '/api/subsys/grants', { token })
  check('EVT-15 自身授权快照', grants.status === 200 && grants.json?.teamScope === 'allTeams' && Array.isArray(grants.json?.teamIds) && Array.isArray(grants.json?.orgGrants), `${grants.status} ${grants.text.slice(0, 160)}`)

  const teamGrantUpdate = await api('PUT', `/api/subsystem/${appId}`, {
    token: root,
    body: { name: `事件子系统-${TS}`, status: 'enabled', teamScope: 'specifiedTeams', teamIds: [teamId] },
  })
  for (let i = 0; i < 50 && !events.some((x) => x.event === 'subsys.access_app.grants_changed'); i++) await sleep(100)
  const teamGrantEvent = events.find((x) => x.event === 'subsys.access_app.grants_changed')
  check('EVT-24 授权变更事件携带关联 teamIds', teamGrantUpdate.status === 200 && teamGrantEvent?.data?.teamIds?.includes(String(teamId)), JSON.stringify(teamGrantEvent))

  const orgGrantUpdate = await api('PUT', `/api/org/grant/subsystem/${appId}`, {
    token: root,
    body: { grants: [{ orgRoleId: roleId, scope: 0 }] },
  })
  for (let i = 0; i < 50 && !events.some((x) => x.event === 'subsys.access_app.grants_changed' && x.data?.orgRoleIds?.includes(String(roleId))); i++) await sleep(100)
  const orgGrantEvent = events.find((x) => x.event === 'subsys.access_app.grants_changed' && x.data?.orgRoleIds?.includes(String(roleId)))
  check('EVT-25 组织授权变更事件携带 orgRoleIds', orgGrantUpdate.status === 200 && !!orgGrantEvent, JSON.stringify(orgGrantEvent))

  const page = await api('GET', `/api/subsys/events?limit=50&afterId=${encodeURIComponent(events[0]?.id ?? '')}`, { token })
  check('EVT-16 REST 事件补发', page.status === 200 && (page.json?.items ?? []).length > 0, `${page.status} ${page.text.slice(0, 160)}`)

  const noCursor = await api('GET', '/api/subsys/events', { token })
  check('EVT-17 无游标要求全量对账', noCursor.status === 200 && noCursor.json?.resyncRequired === true, `${noCursor.status} ${noCursor.text.slice(0, 160)}`)

  // 断线补发：停止连接 → 产生新事件 → 带 lastEventId 重连，应从 Redis Stream 补发
  const lastId = events[events.length - 1]?.id
  await connection.stop()
  await api('POST', '/api/subsys/system/users', { token, body: { userName, nickName: `离线更新-${TS}` } })

  const replayEvents = []
  let replayReady = null
  const replayConn = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE}/hub/subsys?lastEventId=${encodeURIComponent(lastId)}`, { accessTokenFactory: () => token })
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  replayConn.on('onEvent', (e) => { replayEvents.push(e) })
  replayConn.on('onReady', (r) => { replayReady = r })
  await replayConn.start()
  for (let i = 0; i < 50 && !replayReady; i++) await sleep(100)
  for (let i = 0; i < 50 && !replayEvents.some((x) => x.event === 'subsys.user.updated'); i++) await sleep(100)
  check('EVT-20 带 lastEventId 重连补发离线事件', replayReady?.requiresFullSync === false && replayEvents.some((x) => x.event === 'subsys.user.updated' && x.data?.fields?.includes('nickName')), JSON.stringify({ ready: replayReady, events: replayEvents.map((x) => x.event) }))

  // 游标早于窗口起点：要求全量对账
  const staleEvents = []
  const staleConn = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE}/hub/subsys?lastEventId=0-0`, { accessTokenFactory: () => token })
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  staleConn.on('onEvent', (e) => { staleEvents.push(e) })
  staleConn.on('onReady', () => { })
  await staleConn.start()
  for (let i = 0; i < 50 && !staleEvents.some((x) => x.event === 'subsys.resync_required'); i++) await sleep(100)
  check('EVT-21 过期游标返回 resync_required', staleEvents.some((x) => x.event === 'subsys.resync_required' && x.data?.reason === 'cursor_expired'), JSON.stringify(staleEvents.map((x) => x.event)))
  await staleConn.stop()

  // 应用接入（moai-sa- appid 直连）同样接入事件面
  const appAccess = await api('POST', '/api/subsystem/create', {
    token: root,
    body: { appType: 'app', name: `事件应用接入-${TS}` },
  })
  const appAccessKey = appAccess.json?.key
  const appAccessEvents = []
  let appAccessReady = null
  const appAccessConn = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE}/hub/subsys`, { accessTokenFactory: () => appAccessKey })
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  appAccessConn.on('onEvent', (e) => { appAccessEvents.push(e) })
  appAccessConn.on('onReady', (r) => { appAccessReady = r })
  await appAccessConn.start()
  for (let i = 0; i < 50 && !appAccessReady; i++) await sleep(100)
  check('EVT-22 应用接入 appid 直连事件 Hub', appAccess.status === 200 && !!appAccessKey && !!appAccessReady, JSON.stringify({ status: appAccess.status, ready: appAccessReady }))

  await api('POST', '/api/subsys/system/users', { token, body: { userName, nickName: `应用接入可见-${TS}` } })
  for (let i = 0; i < 50 && !appAccessEvents.some((x) => x.event === 'subsys.user.updated'); i++) await sleep(100)
  check('EVT-23 应用接入收到用户事件', appAccessEvents.some((x) => x.event === 'subsys.user.updated' && x.data?.fields?.includes('nickName')), JSON.stringify(appAccessEvents.map((x) => x.event)))
  await appAccessConn.stop()
  await api('DELETE', `/api/subsystem/${appAccess.json?.id}`, { token: root })

  const disableApp = await api('PUT', `/api/subsystem/${appId}`, {
    token: root,
    body: { name: `事件子系统-${TS}`, status: 'disabled', teamScope: 'allTeams', teamIds: [] },
  })
  for (let i = 0; i < 50 && !replayEvents.some((x) => x.event === 'subsys.access_app.status_changed'); i++) await sleep(100)
  const appStatusEvent = replayEvents.find((x) => x.event === 'subsys.access_app.status_changed')
  check('EVT-18 停用接入收到最终事件', disableApp.status === 200 && appStatusEvent?.data?.status === 'disabled', JSON.stringify(appStatusEvent))

  const afterDisable = await api('GET', '/api/subsys/info', { token })
  check('EVT-19 停用后 REST 立即拒绝', afterDisable.status === 403, `${afterDisable.status}`)
  check('EVT-28 停用后不能重连事件 Hub', await expectConnectFail(token))

  await connection.stop()
  await replayConn.stop()
  await staleConn.stop()

  // 清理
  await api('PUT', `/api/subsys/system/org/users/${userId}/roles`, { token, body: { roleIds: [] } }).catch(() => {})
  await api('DELETE', `/api/subsys/system/org/roles/${roleId}`, { token }).catch(() => {})
  await api('DELETE', `/api/subsys/system/teams/${teamId}/users/${userId}`, { token }).catch(() => {})
  await api('PUT', `/api/org/grant/subsystem/${appId}`, { token: root, body: { grants: [] } }).catch(() => {})
  await api('DELETE', `/api/subsystem/${appId}`, { token: root }).catch(() => {})

  console.log(`\n${FAIL === 0 ? 'PASS' : 'FAIL'} ${PASS}/${PASS + FAIL}`)
  if (FAIL > 0) process.exitCode = 1
}

main().catch((err) => {
  console.error(err)
  process.exitCode = 1
})
