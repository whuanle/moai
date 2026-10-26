// 钉钉渠道模块 E2E（场景 @DT-Sn）：钉钉应用连接 CRUD + 应用渠道「创建即绑定」
// 用法：node local-dev/dingtalk-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 DT_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/dingtalk_app.sql 建表（新库由 EnsureCreated 直接建成）。
import crypto from 'node:crypto'

const BASE = process.env.DT_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

  const owner = await mkuser('do')
  const member = await mkuser('dm')
  const outsider = await mkuser('dx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'dingtalk-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const OTHER_TID = Number((await api('POST', '/api/team', { token: outsider.token, body: { name: 'dingtalk-other-' + TS } })).json.value)

  // 三个团队应用：本团队 A、本团队 B、外部团队 C
  const appA = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'da-' + TS, appType: 'agent' } })).json.value
  const appB = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'db-' + TS, appType: 'agent' } })).json.value
  const appC = (await api('POST', '/api/app', { token: outsider.token, body: { teamId: OTHER_TID, name: 'dc-' + TS, appType: 'agent' } })).json.value

  // DT-01 未登录
  check('DT-01 未登录查列表 401', (await api('GET', `/api/dingtalk_app/list?teamId=${TID}`)).status === 401)

  // DT-02 校验
  check('DT-02a 空名称 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: '', appKey: 'ding_x', appSecret: 's' } })).status === 400)
  check('DT-02b 空 AppKey 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'n', appKey: '', appSecret: 's' } })).status === 400)
  check('DT-02c 空 AppSecret 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'n', appKey: 'ding_k' + TS, appSecret: '' } })).status === 400)
  check('DT-02d teamId 非法 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: 0, name: 'n', appKey: 'ding_k' + TS, appSecret: 's' } })).status === 400)

  // DT-03 Member 创建 403
  check('DT-03 Member 创建 403', (await api('POST', '/api/dingtalk_app', { token: member.token, body: { teamId: TID, name: 'm', appKey: 'ding_m' + TS, appSecret: 's' } })).status === 403)

  // DT-04 Owner 创建成功（不带渠道，纯连接）+ 列表可见
  const cr = await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-o-' + TS, appKey: 'ding_o' + TS, appSecret: 'sec' + TS } })
  check('DT-04a Owner 创建 200 + guid', cr.status === 200 && isGuid(cr.json?.value), JSON.stringify(cr.json).slice(0, 120))
  const DID0 = cr.json.value
  const lst = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: member.token })
  const item = (lst.json?.items ?? []).find(x => x.dingTalkAppId === DID0)
  check('DT-04b 列表可见且不回显 secret', lst.status === 200 && item && item.appSecret === undefined && item.bindChannelType == null, JSON.stringify(item).slice(0, 200))
  check('DT-04c 假凭证在线状态为 false', item && item.isOnline === false && item.isDisable === false)

  // DT-05 重复 AppKey 409 / 重复名称 409
  check('DT-05a 重复 AppKey 409', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-x-' + TS, appKey: 'ding_o' + TS, appSecret: 's' } })).status === 409)
  check('DT-05b 重复名称 409', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-o-' + TS, appKey: 'ding_y' + TS, appSecret: 's' } })).status === 409)

  // DT-06 创建即绑定的渠道校验：渠道不存在 404 / 跨团队 403 / 渠道 id 非法 400 / 不支持的渠道类型 400 / 只传 channelId 400
  check('DT-06a 绑定不存在的应用渠道 404', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-e-' + TS, appKey: 'ding_e' + TS, appSecret: 's', channelType: 'app', channelId: crypto.randomUUID() } })).status === 404)
  check('DT-06b 绑定跨团队应用渠道 403', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-e-' + TS, appKey: 'ding_e' + TS, appSecret: 's', channelType: 'app', channelId: appC } })).status === 403)
  check('DT-06c 渠道 id 格式错误 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-e-' + TS, appKey: 'ding_e' + TS, appSecret: 's', channelType: 'app', channelId: 'not-a-guid' } })).status === 400)
  check('DT-06d 非法渠道类型枚举 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-e-' + TS, appKey: 'ding_e' + TS, appSecret: 's', channelType: 'wiki', channelId: '1' } })).status === 400)
  check('DT-06e 只传渠道 id 不传渠道类型 400', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-e-' + TS, appKey: 'ding_e' + TS, appSecret: 's', channelId: appA } })).status === 400)

  // DT-07 创建即绑定应用渠道成功，列表回显绑定信息
  const crb = await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-b-' + TS, appKey: 'ding_b' + TS, appSecret: 'sec' + TS, channelType: 'app', channelId: appA } })
  check('DT-07a 创建即绑定 200 + guid', crb.status === 200 && isGuid(crb.json?.value), JSON.stringify(crb.json).slice(0, 120))
  const DID = crb.json.value
  const lst2 = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })
  const item2 = (lst2.json?.items ?? []).find(x => x.dingTalkAppId === DID)
  check('DT-07b 列表回显绑定渠道', item2 && item2.bindChannelType === 'app' && item2.bindChannelId === appA && item2.bindTime != null, JSON.stringify(item2).slice(0, 200))

  // DT-08 同一钉钉 AppKey 已接入后不可重复创建（无绑定入口，AppKey 全局唯一兜底互斥）
  check('DT-08 同一 AppKey 再创建（带渠道）409', (await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-b2-' + TS, appKey: 'ding_b' + TS, appSecret: 's', channelType: 'app', channelId: appB } })).status === 409)

  // DT-09 第二个钉钉应用连接可接入另一应用（不同钉钉应用互不影响）
  const cr2 = await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-p-' + TS, appKey: 'ding_p' + TS, appSecret: 's', channelType: 'app', channelId: appB } })
  check('DT-09a 第二个钉钉应用接入另一应用 200', cr2.status === 200 && isGuid(cr2.json?.value), cr2.text.slice(0, 120))
  const DID2 = cr2.json?.value
  const lst9 = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })
  const item9 = (lst9.json?.items ?? []).find(x => x.dingTalkAppId === DID2)
  check('DT-09b 列表回显应用 B 绑定', item9 && item9.bindChannelType === 'app' && item9.bindChannelId === appB)

  // DT-10 Member 创建即绑定 403
  check('DT-10 Member 创建即绑定 403', (await api('POST', '/api/dingtalk_app', { token: member.token, body: { teamId: TID, name: 'm2', appKey: 'ding_m2' + TS, appSecret: 's', channelType: 'app', channelId: appA } })).status === 403)

  // DT-11 其它团队 Admin 访问本团队连接 404（非成员视角不存在）
  check('DT-11 其它团队更新本团队连接 404', (await api('PUT', `/api/dingtalk_app/${DID}`, { token: outsider.token, body: { name: 'hack', isDisable: false } })).status === 404)

  // DT-12 更新：改名 + 禁用
  const upd = await api('PUT', `/api/dingtalk_app/${DID}`, { token: owner.token, body: { name: 'dt-o2-' + TS, description: 'd', isDisable: true } })
  check('DT-12a 更新并禁用 200', upd.status === 200, upd.text.slice(0, 120))
  const lst3 = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })
  const item3 = (lst3.json?.items ?? []).find(x => x.dingTalkAppId === DID)
  check('DT-12b 禁用生效且禁用时离线', item3 && item3.isDisable === true && item3.isOnline === false)
  check('DT-12c 禁用后 AppSecret 为空保持不变（仍能改回）', (await api('PUT', `/api/dingtalk_app/${DID}`, { token: owner.token, body: { name: 'dt-o2-' + TS, isDisable: false } })).status === 200)
  check('DT-12d Member 更新 403', (await api('PUT', `/api/dingtalk_app/${DID}`, { token: member.token, body: { name: 'dt-o2-' + TS, isDisable: false } })).status === 403)

  // DT-12 编辑：换 AppKey（填错凭证无需删除重建）
  const editKey = await api('PUT', `/api/dingtalk_app/${DID}`, { token: owner.token, body: { name: 'dt-o2-' + TS, appKey: 'ding_edit' + TS, isDisable: false } })
  check('DT-12e 编辑换 AppKey 200', editKey.status === 200, editKey.text.slice(0, 120))
  const lst4 = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })
  check('DT-12f 列表回显新 AppKey', (lst4.json?.items ?? []).find(x => x.dingTalkAppId === DID)?.appKey === 'ding_edit' + TS)
  check('DT-12g 换成已存在的 AppKey 409', (await api('PUT', `/api/dingtalk_app/${DID}`, { token: owner.token, body: { name: 'dt-o2-' + TS, appKey: 'ding_p' + TS, isDisable: false } })).status === 409)
  const editKeep = await api('PUT', `/api/dingtalk_app/${DID}`, { token: owner.token, body: { name: 'dt-o2-' + TS, isDisable: false } })
  check('DT-12h AppKey/AppSecret 留空保持不变', editKeep.status === 200 && (await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })).json?.items?.find(x => x.dingTalkAppId === DID)?.appKey === 'ding_edit' + TS)

  // DT-13 删除即彻底移除：删除后 AppKey 可重新创建并接入同一渠道（不保留可复用连接）
  check('DT-13a 删除 200', (await api('DELETE', `/api/dingtalk_app/${DID2}`, { token: owner.token })).status === 200)
  check('DT-13b 重复删除 404', (await api('DELETE', `/api/dingtalk_app/${DID2}`, { token: owner.token })).status === 404)
  const recre = await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-p2-' + TS, appKey: 'ding_p' + TS, appSecret: 's2', channelType: 'app', channelId: appB } })
  check('DT-13c 删除后同 AppKey 可重建并接入同渠道', recre.status === 200, recre.text.slice(0, 120))

  // DT-14 删除带绑定的连接：绑定随删解除，渠道立即可被新连接接入，列表不再可见
  check('DT-14a 删除带绑定的连接 200', (await api('DELETE', `/api/dingtalk_app/${DID}`, { token: owner.token })).status === 200)
  const lst14 = await api('GET', `/api/dingtalk_app/list?teamId=${TID}`, { token: owner.token })
  check('DT-14b 列表不再包含已删连接', (lst14.json?.items ?? []).every(x => x.dingTalkAppId !== DID))
  const bind3 = await api('POST', '/api/dingtalk_app', { token: owner.token, body: { teamId: TID, name: 'dt-q-' + TS, appKey: 'ding_q' + TS, appSecret: 's', channelType: 'app', channelId: appA } })
  check('DT-14c 应用 A 渠道可被新连接接入', bind3.status === 200, bind3.text.slice(0, 120))
  const DID3 = bind3.json?.value

  // DT-16 应用列表外部渠道：钉钉渠道未回填 dingtalk id 前不露出（钉钉无按 AppKey 拉起机器人单聊的深链，
  // jumprobot 需回调 chatbotUserId，由事件转发器在首条机器人消息后回填并自动露出精确链接，需真实钉钉机器人）
  const appLst = await api('GET', `/api/app/list?teamId=${TID}`, { token: member.token })
  const appItemA = (appLst.json?.items ?? []).find(x => x.appId === appA)
  const appItemB = (appLst.json?.items ?? []).find(x => x.appId === appB)
  check('DT-16a 未回填 dingtalk id 不露出入口', appItemA && (appItemA.externalChannels ?? []).every(x => x.channelType !== 'dingtalk'), JSON.stringify(appItemA?.externalChannels).slice(0, 200))
  check('DT-16b 应用 B 同样不露出', appItemB && (appItemB.externalChannels ?? []).every(x => x.channelType !== 'dingtalk'))

  // DT-15 收尾清理（保留库干净；不校验失败）
  for (const id of [DID3, recre.json?.value, DID0]) {
    if (id) await api('DELETE', `/api/dingtalk_app/${id}`, { token: owner.token })
  }

  console.log(`\n== dingtalk e2e: ${PASS} pass, ${FAIL} fail ==`)
  if (FAIL > 0) process.exit(1)
}

main().catch(ex => { console.error(ex); process.exit(1) })
