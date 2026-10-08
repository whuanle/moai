// 飞书通知模块 E2E（场景 @FS-Sn）：飞书应用连接 CRUD + 应用渠道「创建即绑定」
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

  // FS-04 Owner 创建成功（不带渠道，纯连接）+ 列表可见
  const cr = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-o-' + TS, appId: 'cli_o' + TS, appSecret: 'sec' + TS } })
  check('FS-04a Owner 创建 200 + guid', cr.status === 200 && isGuid(cr.json?.value), JSON.stringify(cr.json).slice(0, 120))
  const FID0 = cr.json.value
  const lst = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: member.token })
  const item = (lst.json?.items ?? []).find(x => x.feishuAppId === FID0)
  check('FS-04b 列表可见且不回显 secret', lst.status === 200 && item && item.appSecret === undefined && item.bindChannelType == null, JSON.stringify(item).slice(0, 200))
  check('FS-04c 假凭证在线状态为 false', item && item.isOnline === false && item.isDisable === false)

  // FS-05 重复 AppID 409 / 重复名称 409
  check('FS-05a 重复 AppID 409', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-x-' + TS, appId: 'cli_o' + TS, appSecret: 's' } })).status === 409)
  check('FS-05b 重复名称 409', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-o-' + TS, appId: 'cli_y' + TS, appSecret: 's' } })).status === 409)

  // FS-06 创建即绑定的渠道校验：渠道不存在 404 / 跨团队 403 / 渠道 id 非法 400 / 不支持的渠道类型 400 / 只传 channelId 400
  check('FS-06a 绑定不存在的应用渠道 404', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelType: 'app', channelId: crypto.randomUUID() } })).status === 404)
  check('FS-06b 绑定跨团队应用渠道 403', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelType: 'app', channelId: appC } })).status === 403)
  check('FS-06c 渠道 id 格式错误 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelType: 'app', channelId: 'not-a-guid' } })).status === 400)
  check('FS-06d 非法渠道类型枚举 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelType: 'wiki', channelId: '1' } })).status === 400)
  check('FS-06e 不支持的渠道类型 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelType: 'wikiSource', channelId: crypto.randomUUID() } })).status === 400)
  check('FS-06f 只传渠道 id 不传渠道类型 400', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-e-' + TS, appId: 'cli_e' + TS, appSecret: 's', channelId: appA } })).status === 400)

  // FS-07 创建即绑定应用渠道成功，列表回显绑定信息
  const crb = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-b-' + TS, appId: 'cli_b' + TS, appSecret: 'sec' + TS, channelType: 'app', channelId: appA } })
  check('FS-07a 创建即绑定 200 + guid', crb.status === 200 && isGuid(crb.json?.value), JSON.stringify(crb.json).slice(0, 120))
  const FID = crb.json.value
  const lst2 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  const item2 = (lst2.json?.items ?? []).find(x => x.feishuAppId === FID)
  check('FS-07b 列表回显绑定渠道', item2 && item2.bindChannelType === 'app' && item2.bindChannelId === appA && item2.bindTime != null, JSON.stringify(item2).slice(0, 200))

  // FS-08 同一飞书 AppID 已接入后不可重复创建（无绑定入口，AppID 全局唯一兜底互斥）
  check('FS-08 同一 AppID 再创建（带渠道）409', (await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-b2-' + TS, appId: 'cli_b' + TS, appSecret: 's', channelType: 'app', channelId: appB } })).status === 409)

  // FS-09 第二个飞书应用连接可接入另一应用（不同飞书应用互不影响）
  const cr2 = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-p-' + TS, appId: 'cli_p' + TS, appSecret: 's', channelType: 'app', channelId: appB } })
  check('FS-09a 第二个飞书应用接入另一应用 200', cr2.status === 200 && isGuid(cr2.json?.value), cr2.text.slice(0, 120))
  const FID2 = cr2.json?.value
  const lst9 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  const item9 = (lst9.json?.items ?? []).find(x => x.feishuAppId === FID2)
  check('FS-09b 列表回显应用 B 绑定', item9 && item9.bindChannelType === 'app' && item9.bindChannelId === appB)

  // FS-10 Member 创建即绑定 403
  check('FS-10 Member 创建即绑定 403', (await api('POST', '/api/feishu_app', { token: member.token, body: { teamId: TID, name: 'm2', appId: 'cli_m2' + TS, appSecret: 's', channelType: 'app', channelId: appA } })).status === 403)

  // FS-11 绑定/解绑 HTTP 端点已下线（路由前缀仍命中故返回 405 而非 404；绑定仅随创建发生，解绑随删除发生）
  check('FS-11a bind 端点 405', (await api('POST', `/api/feishu_app/${FID}/bind`, { token: owner.token, body: { channelType: 'app', channelId: appA } })).status === 405)
  check('FS-11b unbind 端点 405', (await api('POST', `/api/feishu_app/${FID}/unbind`, { token: owner.token })).status === 405)

  // FS-12 更新：改名 + 禁用
  const upd = await api('PUT', `/api/feishu_app/${FID}`, { token: owner.token, body: { name: 'fs-o2-' + TS, description: 'd', isDisable: true } })
  check('FS-12a 更新并禁用 200', upd.status === 200, upd.text.slice(0, 120))
  const lst3 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  const item3 = (lst3.json?.items ?? []).find(x => x.feishuAppId === FID)
  check('FS-12b 禁用生效且禁用时离线', item3 && item3.isDisable === true && item3.isOnline === false)
  check('FS-12c 禁用后 AppSecret 为空保持不变（仍能改回）', (await api('PUT', `/api/feishu_app/${FID}`, { token: owner.token, body: { name: 'fs-o2-' + TS, isDisable: false } })).status === 200)

  // FS-13 删除即彻底移除：删除后 AppID 可重新创建并接入同一渠道（不保留可复用连接）
  check('FS-13a 删除 200', (await api('DELETE', `/api/feishu_app/${FID2}`, { token: owner.token })).status === 200)
  check('FS-13b 重复删除 404', (await api('DELETE', `/api/feishu_app/${FID2}`, { token: owner.token })).status === 404)
  const recre = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-p2-' + TS, appId: 'cli_p' + TS, appSecret: 's2', channelType: 'app', channelId: appB } })
  check('FS-13c 删除后同 AppID 可重建并接入同渠道', recre.status === 200, recre.text.slice(0, 120))

  // FS-14 删除带绑定的连接：绑定随删解除，渠道立即可被新连接接入，列表不再可见
  check('FS-14a 删除带绑定的连接 200', (await api('DELETE', `/api/feishu_app/${FID}`, { token: owner.token })).status === 200)
  const lst14 = await api('GET', `/api/feishu_app/list?teamId=${TID}`, { token: owner.token })
  check('FS-14b 列表不再包含已删连接', (lst14.json?.items ?? []).every(x => x.feishuAppId !== FID))
  const bind3 = await api('POST', '/api/feishu_app', { token: owner.token, body: { teamId: TID, name: 'fs-q-' + TS, appId: 'cli_q' + TS, appSecret: 's', channelType: 'app', channelId: appA } })
  check('FS-14c 应用 A 渠道可被新连接接入', bind3.status === 200, bind3.text.slice(0, 120))

  console.log(`\n== feishu e2e: ${PASS} pass, ${FAIL} fail ==`)
  if (FAIL > 0) process.exit(1)
}

main().catch(ex => { console.error(ex); process.exit(1) })
