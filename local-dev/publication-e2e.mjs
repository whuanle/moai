// 上架审核 E2E（场景 @PB-Sn）
// 用法：node local-dev/publication-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/publication_review.sql 建表。
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('po')
  const member = await mkuser('pm')
  const outsider = await mkuser('px')
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken
  if (!adminToken) throw new Error('admin 登录失败（admin/abcd123456），无法验证管理员审批')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'pub-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })

  const app = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '上架应用-' + TS, appType: 'agent' } })
  const APP_ID = String(app.json?.value ?? '')
  // 公开列表（/api/app/public/list）只展示「已发布 + 已公开」的应用，这里先发布（发布 ≠ 公开）
  await api('POST', `/api/app/${APP_ID}/publish`, { token: owner.token })

  // PB-01 未登录
  check('PB-01 未登录申请上架 401', (await api('POST', '/api/publication/apply', { body: { resourceType: 'app', resourceId: APP_ID } })).status === 401)

  // PB-02 校验
  check('PB-02a resourceType 非法 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'video', resourceId: APP_ID } })).status === 400)
  check('PB-02b 空 resourceId 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: '' } })).status === 400)

  // PB-03 资源不存在
  check('PB-03a 不存在的应用 404', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: '01924f5e-0000-7000-8000-00000000ffff' } })).status === 404)
  check('PB-03b 不存在的提示词 404', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'prompt', resourceId: '99999999' } })).status === 404)

  // PB-04 角色门禁：Member 不能申请，非成员 404
  check('PB-04a Member 申请 403', (await api('POST', '/api/publication/apply', { token: member.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 403)
  check('PB-04b 非成员申请 404', (await api('POST', '/api/publication/apply', { token: outsider.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 404)

  // PB-05 Owner 申请成功
  const apply = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, applyReason: '希望公开到应用广场' } })
  check('PB-05 Owner 申请上架 200 且返回记录 id', apply.status === 200 && Number(apply.json?.value) > 0, `${apply.status} ${apply.text.slice(0, 140)}`)
  const PB_ID = String(apply.json?.value ?? '')

  // PB-06 重复申请
  check('PB-06 同资源重复申请 409', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 409)

  // PB-07 团队侧查看
  const teamList = await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })
  const teamItem = (teamList.json?.items ?? []).find(i => String(i.publicationId) === PB_ID)
  check('PB-07a 团队列表含待审核记录', teamList.status === 200 && teamItem?.state === 'pending' && teamItem?.resourceType === 'app' && teamItem?.resourceName?.startsWith('上架应用'), JSON.stringify(teamItem))
  check('PB-07b Member 可看团队列表', (await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: member.token })).status === 200)
  check('PB-07c 非成员查团队列表 404', (await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: outsider.token })).status === 404)
  check('PB-07d 按状态过滤 pending 生效', ((await api('GET', `/api/publication/team_list?teamId=${TID}&state=pending`, { token: owner.token })).json?.items ?? []).some(i => String(i.publicationId) === PB_ID))

  // PB-08 管理员门禁
  check('PB-08a Member 查全平台列表 403', (await api('GET', '/api/publication/list', { token: member.token })).status === 403)
  check('PB-08b Member 审批 403', (await api('POST', '/api/publication/review', { token: member.token, body: { publicationId: PB_ID, isApprove: true } })).status === 403)

  // PB-09 管理员列表可见
  const adminList = await api('GET', '/api/publication/list?state=pending&resourceType=app', { token: adminToken })
  const adminItem = (adminList.json?.items ?? []).find(i => String(i.publicationId) === PB_ID)
  check('PB-09 管理员列表含申请且带团队名/申请人', adminList.status === 200 && !!adminItem && adminItem.teamName?.startsWith('pub-team') && typeof adminItem.createUserName === 'string', JSON.stringify(adminItem))

  // PB-10 撤回
  check('PB-10a Member 撤回 403', (await api('POST', '/api/publication/withdraw', { token: member.token, body: { publicationId: PB_ID } })).status === 403)
  check('PB-10b Owner 撤回待审核申请 200', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: PB_ID } })).status === 200)
  check('PB-10c 撤回后重复撤回 404', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: PB_ID } })).status === 404)
  check('PB-10d 撤回后团队列表不再可见', !((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).some(i => String(i.publicationId) === PB_ID))
  check('PB-10e 撤回后应用 isPublic 仍为 false', (await api('GET', `/api/app/${APP_ID}`, { token: owner.token })).json?.isPublic === false)

  // PB-11 撤回后可再次申请
  const apply2 = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })
  check('PB-11 撤回后再次申请 200', apply2.status === 200, `${apply2.status} ${apply2.text.slice(0, 140)}`)
  const PB2_ID = String(apply2.json?.value ?? '')

  // PB-12 驳回
  check('PB-12a 驳回并留审批意见 200', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB2_ID, isApprove: false, reviewComment: '信息不完整，请补充' } })).status === 200)
  const rejectedItem = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).find(i => String(i.publicationId) === PB2_ID)
  check('PB-12b 团队列表状态 rejected 且回显审批意见', rejectedItem?.state === 'rejected' && rejectedItem?.reviewComment === '信息不完整，请补充', JSON.stringify(rejectedItem))
  check('PB-12c 驳回后应用 isPublic 仍为 false', (await api('GET', `/api/app/${APP_ID}`, { token: owner.token })).json?.isPublic === false)
  check('PB-12d 已审批记录重复审批 409', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB2_ID, isApprove: true } })).status === 409)
  check('PB-12e 已审批记录不可撤回 409', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: PB2_ID } })).status === 409)

  // PB-13 驳回后重新申请并通过 → is_public 才变 true
  const apply3 = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })
  check('PB-13a 驳回后可重新申请 200', apply3.status === 200, `${apply3.status} ${apply3.text.slice(0, 140)}`)
  const PB3_ID = String(apply3.json?.value ?? '')
  check('PB-13b 审批通过 200', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB3_ID, isApprove: true } })).status === 200)
  const approvedItem = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).find(i => String(i.publicationId) === PB3_ID)
  check('PB-13c 团队列表状态 approved 且记录审批人', approvedItem?.state === 'approved' && typeof approvedItem?.updateUserName === 'string', JSON.stringify(approvedItem))
  const appDetail = await api('GET', `/api/app/${APP_ID}`, { token: owner.token })
  check('PB-13d 通过后应用 is_public=true', appDetail.json?.isPublic === true, String(appDetail.json?.isPublic))
  check('PB-13e 通过后应用出现在公开列表', ((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APP_ID))

  // PB-14 已公开后重复申请
  check('PB-14 已公开资源重复申请 409', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 409)

  // PB-15 外部应用不可申请公开
  const ext = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '外部应用-' + TS, appType: 'agent', isExternal: true } })
  check('PB-15 外部应用申请公开 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: String(ext.json?.value ?? '') } })).status === 400)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error(e); process.exit(1) })
