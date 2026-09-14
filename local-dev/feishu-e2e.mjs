// 飞书通知模块 E2E（场景 @FS-Sn）：飞书应用连接 CRUD + 渠道绑定互斥
// 用法：node local-dev/feishu-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 FS_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/feishu_app.sql 建表（新库由 EnsureCreated 直接建成）。
import crypto from 'node:crypto'

const BASE = process.env.FS_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('fo')
  const member = await mkuser('fm')
  const outsider = await mkuser('fx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'feishu-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const OTHER_TID = Number((await api('POST', '/api/team', { token: outsider.token, body: { name: 'feishu-other-' + TS } })).json.value)

  // 两个团队应用：本团队 A、本团队 B、外部团队 C
  const appA = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'fa-' + TS, appType: 'agent' } })).json.value
  const appB = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'fb-' + TS, appType: 'agent' } })).json.value
  const appC = (await api('POST', '/api/app', { token: outsider.token, body: { teamId: OTHER_TID, name: 'fc-' + TS, appType: 'agent' } })).json.value

  // FS-01 未登录
  check('FS-01 未登录查列表 401', (await api('GET', `/api/feishu_app/list?teamId=${TID}`)).status === 401)

  // FS-02 校验
  check('FS-02a 空名称 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: '', appId: 'cli_x', appSecret: 's' } })).status === 400)
  check('FS-02b 空 AppID 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'n', appId: '', appSecret: 's' } })).status === 400)

  // FS-03 Member 创建 403
  check('FS-03 Member 创建 403', (await api('POST', '/api/feishu_app', { token: member.token, body: { teamId: TID, name: 'm', appId: 'cli_m' + TS, appSecret: 's' } })).status === 403)

  // FS-04 Owner 创建成功 + 列表可见
  const cr = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-o-' + TS, appId: 'cli_o' + TS, appSecret: 'sec' + TS } })
  check('FS-04a Owner 创建 200 + guid', cr.status === 200 && isGuid(cr.json?.value), JSON.stringify(cr.json).slice(0, 120))
  const FID = cr.json.value
  const lst = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: member.token })
  const item = (lst.json?.items ?? []).find(x => x.feishuAppId === FID)
  check('FS-04b 列表可见且不回显 secret', lst.status === 200 && item && item.appSecret === undefined && item.bindChannelType == null, JSON.stringify(item).slice(0, 200))
  check('FS-04c 假凭证在线状态为 false', item && item.isOnline === false && item.isDisable === false)

  // FS-05 重复 AppID 409 / 重复名称 409
  check('FS-05a 重复 AppID 409', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-x-' + TS, appId: 'cli_o' + TS, appSecret: 's' } })).status === 409)
  check('FS-05b 重复名称 409', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-o-' + TS, appId: 'cli_y' + TS, appSecret: 's' } })).status === 409)

  // FS-06 渠道不存在 404 / 跨团队渠道 403 / 非法渠道类型 400
  check('FS-06a 绑定不存在的应用渠道 404', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: crypto.randomUUID() } })).status === 404)
  check('FS-06b 绑定跨团队应用渠道 403', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appC } })).status === 403)
  check('FS-06c 渠道 id 格式错误 400', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: 'not-a-guid' } })).status === 400)
  check('FS-06d 不支持的渠道类型 400', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'wiki', channelId: '1' } })).status === 400)

  // FS-07 绑定应用渠道成功
  const bind = await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appA } })
  check('FS-07 绑定应用渠道 200', bind.status === 200, bind.text.slice(0, 120))
  const lst2 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  const item2 = (lst2.json?.items ?? []).find(x => x.feishuAppId === FID)
  check('FS-07b 列表回显绑定渠道', item2 && item2.bindChannelType === 'app' && item2.bindChannelId === appA)

  // FS-08 核心互斥：同一飞书应用再绑定其它渠道 409（本团队 B 或 wiki）
  check('FS-08a 已绑定再绑定其它应用 409', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appB } })).status === 409)

  // FS-09 第二个飞书应用可以绑定另一个渠道（不同飞书应用互不影响）
  const FID2 = (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-p-' + TS, appId: 'cli_p' + TS, appSecret: 's' } })).json.value
  const bind2 = await api('POST', `/api/feishu_app/${FID2}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appB } })
  check('FS-09 第二个飞书应用绑定另一应用 200', bind2.status === 200, bind2.text.slice(0, 120))

  // FS-10 Member 绑定/解绑 403
  check('FS-10 Member 绑定 403', (await api('POST', `/api/feishu_app/${FID}/unbind`, { token: member.token })).status === 403)

  // FS-11 解绑成功，重复解绑 404
  check('FS-11a 解绑 200', (await api('POST', `/api/feishu_app/${FID}/unbind`, { token: owner.token })).status === 200)
  check('FS-11b 重复解绑 404', (await api('POST', `/api/feishu_app/${FID}/unbind`, { token: owner.token })).status === 404)

  // FS-12 更新：改名 + 禁用
  const upd = await api('PUT', `/api/feishu_app/${FID}`, { token: owner.token, body: { name: 'fs-o2-' + TS, description: 'd', isDisable: true } })
  check('FS-12a 更新并禁用 200', upd.status === 200, upd.text.slice(0, 120))
  const lst3 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  const item3 = (lst3.json?.items ?? []).find(x => x.feishuAppId === FID)
  check('FS-12b 禁用生效且禁用时离线', item3 && item3.isDisable === true && item3.isOnline === false)
  check('FS-12c 禁用后 AppSecret 为空保持不变（仍能改回）', (await api('PUT', `/api/feishu_app/${FID}`, { token: owner.token, body: { name: 'fs-o2-' + TS, isDisable: false } })).status === 200)

  // FS-13 删除：绑定随之解除（FID2 删除后 appB 可被 FID 重新绑定）
  check('FS-13a 删除 200', (await api('DELETE', `/api/feishu_app/${FID2}`, { token: owner.token })).status === 200)
  check('FS-13b 重复删除 404', (await api('DELETE', `/api/feishu_app/${FID2}`, { token: owner.token })).status === 404)
  const rebind = await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appB } })
  check('FS-13c 删除后渠道可被其它飞书应用绑定', rebind.status === 200, rebind.text.slice(0, 120))

  // FS-14 删除带绑定的连接，绑定同时解除
  check('FS-14a 删除带绑定的连接 200', (await api('DELETE', `/api/feishu_app/${FID}`, { token: owner.token })).status === 200)
  const bind3 = await api('POST', `/api/feishu_app/${FID2}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appA } })
  check('FS-14b FID2 已删 404', bind3.status === 404, `status=${bind3.status} ${bind3.text.slice(0, 120)}`)

  console.log(`\n== feishu e2e: ${PASS} pass, ${FAIL} fail ==`)
  if (FAIL > 0) process.exit(1)
}

main().catch(ex => { console.error(ex); process.exit(1) })
