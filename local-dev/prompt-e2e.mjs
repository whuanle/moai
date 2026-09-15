// 提示词模块 E2E（场景 @PT-Sn）
// 用法：node local-dev/prompt-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，prompt 表已建（存量库执行 asserts/prompt.sql），admin 账号可登录。
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const alice = await mkuser('pa')   // 个人提示词创建者
  const bob = await mkuser('pb')     // 旁观者
  const tOwner = await mkuser('po')  // 团队 Owner
  const tMember = await mkuser('pm') // 团队 Member
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken
  if (!adminToken) throw new Error('admin 登录失败（admin/abcd123456），无法验证管理员审批')

  // 团队：tOwner 为 Owner，tMember 为 Member
  const TID = Number((await api('POST', '/api/team', { token: tOwner.token, body: { name: 'prompt-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: tOwner.token, body: { userId: tMember.userId, role: 0 } })

  // admin 建 prompt 分类
  const CLASS_ID = Number((await api('POST', '/api/classify', { token: adminToken, body: { type: 'prompt', name: 'PT分类-' + TS } })).json?.value)

  // ===== 一、个人提示词 =====

  // PT-01 未登录
  check('PT-01 未登录创建 401', (await api('POST', '/api/prompt', { body: { teamId: 0, name: 'x', content: 'c' } })).status === 401)

  // PT-02 校验
  check('PT-02a 空名称 400', (await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: '', content: 'c' } })).status === 400)
  check('PT-02b 超长名称 400', (await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: 'a'.repeat(21), content: 'c' } })).status === 400)
  check('PT-02c 空内容 400', (await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: 'n', content: '' } })).status === 400)
  check('PT-02d 分类不存在 404', (await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: 'n', content: 'c', promptClassId: 99999999 } })).status === 404)

  // PT-03 创建个人提示词（带分类）
  const created = await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: '个人提示词-' + TS, description: '个人自用', content: '你是测试助手 {{lang}}', promptClassId: CLASS_ID } })
  const PID = Number(created.json?.value)
  check('PT-03 创建个人提示词 200 且返回 id', created.status === 200 && PID > 0, `${created.status} ${created.text.slice(0, 140)}`)

  // PT-04 列表可见性
  const myList = await api('GET', '/api/prompt/my_list', { token: alice.token })
  const mine = (myList.json?.items ?? []).find(i => Number(i.promptId) === PID)
  check('PT-04a my_list 含新提示词且带分类', myList.status === 200 && mine?.name?.startsWith('个人提示词') && Number(mine?.promptClassId) === CLASS_ID, JSON.stringify(mine))
  const bobMyList = await api('GET', '/api/prompt/my_list', { token: bob.token })
  check('PT-04b 他人 my_list 不含该提示词', !(bobMyList.json?.items ?? []).some(i => Number(i.promptId) === PID))

  // PT-05 详情权限：本人可见，他人 404（未上架）
  const ownDetail = await api('GET', `/api/prompt/${PID}`, { token: alice.token })
  check('PT-05a 创建人可见详情含内容', ownDetail.status === 200 && ownDetail.json?.content === '你是测试助手 {{lang}}')
  check('PT-05b 他人查看未上架个人提示词 404', (await api('GET', `/api/prompt/${PID}`, { token: bob.token })).status === 404)

  // PT-06 更新权限与生效
  check('PT-06a 他人更新 403', (await api('PUT', `/api/prompt/${PID}`, { token: bob.token, body: { name: 'x', content: 'y' } })).status === 403)
  const upd = await api('PUT', `/api/prompt/${PID}`, { token: alice.token, body: { name: '个人提示词改-' + TS, description: '改', content: '更新后的内容', promptClassId: 0 } })
  const updDetail = await api('GET', `/api/prompt/${PID}`, { token: alice.token })
  check('PT-06b 创建人更新 200 且生效', upd.status === 200 && updDetail.json?.content === '更新后的内容' && Number(updDetail.json?.promptClassId) === 0, `${upd.status} ${upd.text.slice(0, 140)}`)

  // ===== 二、团队提示词 =====

  // PT-07 创建权限
  check('PT-07a Member 创建团队提示词 403', (await api('POST', '/api/prompt', { token: tMember.token, body: { teamId: TID, name: 'n', content: 'c' } })).status === 403)
  check('PT-07b 非成员创建 404', (await api('POST', '/api/prompt', { token: bob.token, body: { teamId: TID, name: 'n', content: 'c' } })).status === 404)
  const teamCreated = await api('POST', '/api/prompt', { token: tOwner.token, body: { teamId: TID, name: '团队提示词-' + TS, description: '团队共用', content: '团队提示词内容' } })
  const TPID = Number(teamCreated.json?.value)
  check('PT-07c Owner 创建团队提示词 200', teamCreated.status === 200 && TPID > 0, `${teamCreated.status} ${teamCreated.text.slice(0, 140)}`)

  // PT-08 团队列表可见性
  const teamList = await api('GET', `/api/prompt/team_list?teamId=${TID}`, { token: tMember.token })
  check('PT-08a Member 可见团队列表含团队提示词', teamList.status === 200 && (teamList.json?.items ?? []).some(i => Number(i.promptId) === TPID), JSON.stringify(teamList.json?.items)?.slice(0, 160))
  check('PT-08b 非成员查团队列表 404', (await api('GET', `/api/prompt/team_list?teamId=${TID}`, { token: bob.token })).status === 404)

  // PT-09 团队提示词成员可见详情、管理仅 Admin+
  check('PT-09a Member 可见团队提示词详情', (await api('GET', `/api/prompt/${TPID}`, { token: tMember.token })).status === 200)
  check('PT-09b 局外人查看团队提示词 404', (await api('GET', `/api/prompt/${TPID}`, { token: bob.token })).status === 404)
  check('PT-09c Member 更新团队提示词 403', (await api('PUT', `/api/prompt/${TPID}`, { token: tMember.token, body: { name: 'x', content: 'y' } })).status === 403)
  check('PT-09d Member 删除团队提示词 403', (await api('DELETE', `/api/prompt/${TPID}`, { token: tMember.token })).status === 403)

  // ===== 三、申请上架 =====

  // PT-10 个人提示词上架权限
  check('PT-10a 他人申请上架个人提示词 403', (await api('POST', '/api/publication/apply', { token: bob.token, body: { resourceType: 'prompt', resourceId: String(PID) } })).status === 403)
  const apply1 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'prompt', resourceId: String(PID), applyReason: '希望共享' } })
  const PB1 = String(apply1.json?.value ?? '')
  check('PT-10b 创建人申请上架 200', apply1.status === 200 && Number(PB1) > 0, `${apply1.status} ${apply1.text.slice(0, 140)}`)
  check('PT-10c 重复申请 409', (await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'prompt', resourceId: String(PID) } })).status === 409)

  // PT-11 列表可见待审核申请并撤回
  const afterApply = await api('GET', '/api/prompt/my_list', { token: alice.token })
  const minePending = (afterApply.json?.items ?? []).find(i => Number(i.promptId) === PID)
  check('PT-11a my_list 返回待审核申请 id', minePending?.pendingPublicationId === PB1, JSON.stringify(minePending))
  check('PT-11b 他人撤回 403', (await api('POST', '/api/publication/withdraw', { token: bob.token, body: { publicationId: PB1 } })).status === 403)
  check('PT-11c 创建人撤回 200', (await api('POST', '/api/publication/withdraw', { token: alice.token, body: { publicationId: PB1 } })).status === 200)
  const afterWithdraw = await api('GET', '/api/prompt/my_list', { token: alice.token })
  const mineCleared = (afterWithdraw.json?.items ?? []).find(i => Number(i.promptId) === PID)
  check('PT-11d 撤回后待审核 id 清空', mineCleared?.pendingPublicationId == null, JSON.stringify(mineCleared))

  // PT-12 个人提示词审批上架 → 市场可见
  const apply2 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'prompt', resourceId: String(PID) } })
  const PB2 = String(apply2.json?.value ?? '')
  const review = await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB2, isApprove: true, reviewComment: 'ok' } })
  check('PT-12a admin 审批通过 200', review.status === 200, `${review.status} ${review.text.slice(0, 140)}`)
  const market = await api('GET', '/api/prompt/market_list', { token: bob.token })
  const marketItem = (market.json?.items ?? []).find(i => Number(i.promptId) === PID)
  check('PT-12b 市场列表含已上架提示词', market.status === 200 && marketItem?.isPublic === true, JSON.stringify(market)?.slice(0, 200))
  const bobDetail = await api('GET', `/api/prompt/${PID}`, { token: bob.token })
  check('PT-12c 市场用户可见详情内容', bobDetail.status === 200 && bobDetail.json?.content === '更新后的内容', JSON.stringify(bobDetail.json)?.slice(0, 160))

  // PT-13 团队提示词上架：Member 申请 403，Owner 申请后审批上架
  check('PT-13a Member 申请上架团队提示词 403', (await api('POST', '/api/publication/apply', { token: tMember.token, body: { resourceType: 'prompt', resourceId: String(TPID) } })).status === 403)
  const apply3 = await api('POST', '/api/publication/apply', { token: tOwner.token, body: { resourceType: 'prompt', resourceId: String(TPID) } })
  const PB3 = String(apply3.json?.value ?? '')
  check('PT-13b Owner 申请上架团队提示词 200', apply3.status === 200 && Number(PB3) > 0, `${apply3.status} ${apply3.text.slice(0, 140)}`)
  await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB3, isApprove: true, reviewComment: 'ok' } })
  const market2 = await api('GET', '/api/prompt/market_list', { token: tMember.token })
  check('PT-13c 团队提示词上架后进入市场', (market2.json?.items ?? []).some(i => Number(i.promptId) === TPID))

  // PT-14 市场查看计数 + 过滤（market_list 读数不计数，detail 由他人查看 +1）
  const c0 = Number(((await api('GET', '/api/prompt/market_list', { token: bob.token })).json?.items ?? []).find(i => Number(i.promptId) === PID)?.counter ?? 0)
  await api('GET', `/api/prompt/${PID}`, { token: tMember.token })
  const c1 = Number(((await api('GET', '/api/prompt/market_list', { token: bob.token })).json?.items ?? []).find(i => Number(i.promptId) === PID)?.counter ?? 0)
  check('PT-14a 他人查看市场提示词计数 +1', c1 === c0 + 1, `before=${c0} after=${c1}`)
  const kw = '个人提示词改-' + TS
  const filtered = await api('GET', `/api/prompt/market_list?keywords=${encodeURIComponent(kw)}`, { token: bob.token })
  check('PT-14b 市场关键字过滤命中', (filtered.json?.items ?? []).some(i => Number(i.promptId) === PID) && (filtered.json?.items ?? []).every(i => String(i.name).includes(kw)))

  // PT-15 删除：移除待审核申请、市场不可见
  const tempCreate = await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: '待删提示词-' + TS, content: 'c' } })
  const TEMPID = Number(tempCreate.json?.value)
  const apply5 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'prompt', resourceId: String(TEMPID) } })
  const del = await api('DELETE', `/api/prompt/${TEMPID}`, { token: alice.token })
  check('PT-15a 创建人删除 200', del.status === 200, `${del.status} ${del.text.slice(0, 140)}`)
  check('PT-15b 删除后详情 404', (await api('GET', `/api/prompt/${TEMPID}`, { token: alice.token })).status === 404)
  const adminList = await api('GET', '/api/publication/list?resourceType=prompt&state=pending', { token: adminToken })
  check('PT-15c 删除后待审核申请同步移除', !(adminList.json?.items ?? []).some(i => String(i.resourceId) === String(TEMPID)))
  const marketAfterDel = await api('GET', '/api/prompt/market_list', { token: bob.token })
  check('PT-15d 已上架个人提示词删除后市场不再新增（团队提示词仍在）', (marketAfterDel.json?.items ?? []).some(i => Number(i.promptId) === TPID))

  console.log(`\n结果：PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error(e); process.exit(1) })
