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
  // PB-02c 上架把最新的团队发布上架：不携带版本号申请成功，版本号自动记录应用当前版本（1.0.0=10000），撤回后继续主流程
  const applyNoVer = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })
  const noVerItem = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? [])
    .find(i => String(i.publicationId) === String(applyNoVer.json?.value ?? ''))
  check('PB-02c 不携带版本号申请 200 且版本自动取应用当前版本', applyNoVer.status === 200 && noVerItem?.publishVersion === 10000,
    `${applyNoVer.status} ${JSON.stringify(noVerItem)}`)
  check('PB-02c2 自动版本申请可撤回', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: applyNoVer.json?.value } })).status === 200)
  check('PB-02d 版本号格式非法 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.2' } })).status === 400)
  check('PB-02e 非应用类型携带版本号 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'prompt', resourceId: '1', version: '1.0.0' } })).status === 400)
  check('PB-02f 申请版本低于当前版本 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '0.0.9' } })).status === 400)

  // PB-03 资源不存在
  check('PB-03a 不存在的应用 404', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: '01924f5e-0000-7000-8000-00000000ffff', version: '1.0.0' } })).status === 404)
  check('PB-03b 不存在的提示词 404', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'prompt', resourceId: '99999999' } })).status === 404)

  // PB-04 角色门禁：Member 不能申请，非成员 404
  check('PB-04a Member 申请 403', (await api('POST', '/api/publication/apply', { token: member.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.0.0' } })).status === 403)
  check('PB-04b 非成员申请 404', (await api('POST', '/api/publication/apply', { token: outsider.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.0.0' } })).status === 404)

  // PB-05 Owner 申请成功
  const apply = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, applyReason: '希望公开到应用广场', version: '1.2.0' } })
  check('PB-05 Owner 申请上架 200 且返回记录 id', apply.status === 200 && Number(apply.json?.value) > 0, `${apply.status} ${apply.text.slice(0, 140)}`)
  const PB_ID = String(apply.json?.value ?? '')

  // PB-06 重复申请
  check('PB-06 同资源重复申请 409', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.2.0' } })).status === 409)

  // PB-07 团队侧查看
  const teamList = await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })
  const teamItem = (teamList.json?.items ?? []).find(i => String(i.publicationId) === PB_ID)
  check('PB-07a 团队列表含待审核记录且带版本号', teamList.status === 200 && teamItem?.state === 'pending' && teamItem?.resourceType === 'app' && teamItem?.resourceName?.startsWith('上架应用') && teamItem?.publishVersion === 10200, JSON.stringify(teamItem))
  check('PB-07b Member 可看团队列表', (await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: member.token })).status === 200)
  check('PB-07c 非成员查团队列表 404', (await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: outsider.token })).status === 404)
  check('PB-07d 按状态过滤 pending 生效', ((await api('GET', `/api/publication/team_list?teamId=${TID}&state=pending`, { token: owner.token })).json?.items ?? []).some(i => String(i.publicationId) === PB_ID))

  // PB-08 管理员门禁
  check('PB-08a Member 查全平台列表 403', (await api('GET', '/api/publication/list', { token: member.token })).status === 403)
  check('PB-08b Member 审批 403', (await api('POST', '/api/publication/review', { token: member.token, body: { publicationId: PB_ID, isApprove: true } })).status === 403)

  // PB-09 管理员列表可见
  const adminList = await api('GET', '/api/publication/list?state=pending&resourceType=app', { token: adminToken })
  const adminItem = (adminList.json?.items ?? []).find(i => String(i.publicationId) === PB_ID)
  check('PB-09 管理员列表含申请且带团队名/申请人/头像与版本号', adminList.status === 200 && !!adminItem && adminItem.teamName?.startsWith('pub-team') && typeof adminItem.createUserName === 'string' && adminItem.resourceAvatar === '' && adminItem.publishVersion === 10200, JSON.stringify(adminItem))

  // PB-10 撤回
  check('PB-10a Member 撤回 403', (await api('POST', '/api/publication/withdraw', { token: member.token, body: { publicationId: PB_ID } })).status === 403)
  check('PB-10b Owner 撤回待审核申请 200', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: PB_ID } })).status === 200)
  check('PB-10c 撤回后重复撤回 404', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: PB_ID } })).status === 404)
  check('PB-10d 撤回后团队列表不再可见', !((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).some(i => String(i.publicationId) === PB_ID))
  check('PB-10e 撤回后应用 isPublic 仍为 false', (await api('GET', `/api/app/${APP_ID}`, { token: owner.token })).json?.isPublic === false)

  // PB-11 撤回后可再次申请
  const apply2 = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.2.1' } })
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
  const apply3 = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.3.0' } })
  check('PB-13a 驳回后可重新申请 200', apply3.status === 200, `${apply3.status} ${apply3.text.slice(0, 140)}`)
  const PB3_ID = String(apply3.json?.value ?? '')
  check('PB-13b 审批通过 200', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB3_ID, isApprove: true } })).status === 200)
  const approvedItem = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).find(i => String(i.publicationId) === PB3_ID)
  check('PB-13c 团队列表状态 approved 且记录审批人', approvedItem?.state === 'approved' && typeof approvedItem?.updateUserName === 'string', JSON.stringify(approvedItem))
  const appDetail = await api('GET', `/api/app/${APP_ID}`, { token: owner.token })
  check('PB-13d 通过后应用 is_public=true', appDetail.json?.isPublic === true, String(appDetail.json?.isPublic))
  check('PB-13e 通过后应用出现在公开列表', ((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APP_ID))

  // PB-14 已公开后重复申请
  check('PB-14 已公开资源重复申请 409', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.3.1' } })).status === 409)

  // PB-15 外部应用不可申请公开
  const ext = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '外部应用-' + TS, appType: 'agent', isExternal: true } })
  check('PB-15 外部应用申请公开 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: String(ext.json?.value ?? '') } })).status === 400)

  // PB-16 排序：待审核恒排最前，组内按申请时间倒序
  const appS = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '排序应用-' + TS, appType: 'agent' } })
  const APPS_ID = String(appS.json?.value ?? '')
  await api('POST', `/api/app/${APPS_ID}/publish`, { token: owner.token })
  const applyS = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPS_ID, version: '2.0.0' } })
  const PBS_ID = String(applyS.json?.value ?? '')
  const allItems = (await api('GET', '/api/publication/list', { token: adminToken })).json?.items ?? []
  const firstNonPendingIdx = allItems.findIndex(i => i.state !== 'pending')
  const lastPendingIdx = allItems.map(i => i.state).lastIndexOf('pending')
  check('PB-16a 管理员列表待审核全部排在已审批之前', applyS.status === 200 && (lastPendingIdx < firstNonPendingIdx || firstNonPendingIdx === -1), `lastPending=${lastPendingIdx} firstNonPending=${firstNonPendingIdx}`)
  check('PB-16b 管理员列表同组内按申请时间倒序', (() => {
    const pend = allItems.filter(i => i.state === 'pending').map(i => String(i.publicationId))
    return pend.indexOf(PBS_ID) === 0
  })(), `首条待审核=${allItems.find(i => i.state === 'pending')?.publicationId} 本条=${PBS_ID}`)
  const teamFirst = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? [])[0]
  check('PB-16c 团队列表首条为最新待审核', teamFirst?.publicationId == PBS_ID && teamFirst?.state === 'pending', JSON.stringify(teamFirst))

  // PB-17 下架（取消公开）：清 is_public、不改变团队发布状态、下架后可重新申请
  check('PB-17a Member 下架 403', (await api('POST', '/api/publication/takedown', { token: member.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 403)
  const tk1 = await api('POST', '/api/publication/takedown', { token: outsider.token, body: { resourceType: 'app', resourceId: APP_ID } })
  check('PB-17b 非成员下架 404', tk1.status === 404, `got ${tk1.status} ${tk1.text.slice(0, 120)}`)
  const tk2 = await api('POST', '/api/publication/takedown', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })
  check('PB-17c Owner 下架已公开应用 200', tk2.status === 200, `got ${tk2.status} ${tk2.text.slice(0, 120)}`)
  check('PB-17d 下架后 is_public=false', (await api('GET', `/api/app/${APP_ID}`, { token: owner.token })).json?.isPublic === false)
  check('PB-17e 下架后不再出现在公开列表', !((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APP_ID))
  check('PB-17f 未公开重复下架 409', (await api('POST', '/api/publication/takedown', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID } })).status === 409)
  check('PB-17g 下架后可重新申请 200', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP_ID, version: '1.3.1' } })).status === 200)

  // ===== 可视范围（组织）扩展 =====

  // PB-18 上架前置校验：未发布应用不可申请（@PB-S14）
  const appUnpub = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '未发布应用-' + TS, appType: 'agent' } })
  const APPU_ID = String(appUnpub.json?.value ?? '')
  check('PB-18a 未发布应用申请上架 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID } })).status === 400)
  await api('POST', `/api/app/${APPU_ID}/publish`, { token: owner.token })
  const applyU = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID } })
  check('PB-18b 发布后申请通过前置校验 200', applyU.status === 200, `${applyU.status} ${applyU.text.slice(0, 140)}`)
  check('PB-18c 撤回收尾 200', (await api('POST', '/api/publication/withdraw', { token: owner.token, body: { publicationId: applyU.json?.value } })).status === 200)

  // PB-19 可视范围参数校验（干净应用：已发布未公开无待审）（@PB-S15）
  check('PB-19a 按组织限定但授权清单为空 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'organization', visibleOrgGrants: [] } })).status === 400)
  check('PB-19b 组织不存在 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'organization', visibleOrgGrants: [{ orgRoleId: 999999, scope: 'down' }] } })).status === 400)
  check('PB-19c 非应用类型携带可视范围 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'prompt', resourceId: '1', visibleScope: 'organization', visibleOrgGrants: [{ orgRoleId: 1, scope: 'down' }] } })).status === 400)

  // PB-20 组织范围全链路（@PB-S20 审批物化 + @AP-S131 广场过滤 + @AP-S124 使用门禁）
  const orgRoot = await api('POST', '/api/org/role/create', { token: adminToken, body: { name: '研发中心-' + TS } })
  const ROOT_ORG = Number(orgRoot.json?.orgRoleId ?? 0)
  const orgChild = await api('POST', '/api/org/role/create', { token: adminToken, body: { name: '研发部-' + TS, parentId: ROOT_ORG } })
  const CHILD_ORG = Number(orgChild.json?.orgRoleId ?? 0)
  check('PB-20a root 创建组织树 200', orgRoot.status === 200 && orgChild.status === 200 && ROOT_ORG > 0 && CHILD_ORG > 0, `${orgRoot.status} ${orgChild.status}`)
  check('PB-19d 同组织重复授权 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'organization', visibleOrgGrants: [{ orgRoleId: CHILD_ORG, scope: 'down' }, { orgRoleId: CHILD_ORG, scope: 'self' }] } })).status === 400)

  // member（团队成员）划入研发部；outsider 不在组织内
  check('PB-20b 用户分配组织 200', (await api('PUT', `/api/org/user/${member.userId}/roles`, { token: adminToken, body: { roleIds: [CHILD_ORG] } })).status === 200)

  const applyOrg = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'organization', visibleOrgGrants: [{ orgRoleId: CHILD_ORG, scope: 'down' }] } })
  check('PB-20c 按组织限定申请 200', applyOrg.status === 200, `${applyOrg.status} ${applyOrg.text.slice(0, 140)}`)
  const orgItem = ((await api('GET', `/api/publication/team_list?teamId=${TID}`, { token: owner.token })).json?.items ?? [])
    .find(i => String(i.publicationId) === String(applyOrg.json?.value ?? ''))
  check('PB-20d 列表回显可视范围与组织名', orgItem?.visibleScope === 'organization' && orgItem?.visibleOrgGrants?.[0]?.orgRoleId === CHILD_ORG && orgItem?.visibleOrgGrants?.[0]?.roleName?.startsWith('研发部'), JSON.stringify(orgItem))
  check('PB-20e 审批通过 200', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: String(applyOrg.json?.value), isApprove: true } })).status === 200)
  check('PB-20f 通过后 is_public=true', (await api('GET', `/api/app/${APPU_ID}`, { token: owner.token })).json?.isPublic === true)

  // 广场可见性：组织内成员可见，组织外用户不可见
  check('PB-20g 组织内用户广场可见', ((await api('GET', '/api/app/public/list', { token: member.token })).json?.items ?? []).some(i => i.appId === APPU_ID))
  check('PB-20h 组织外用户广场不可见', !((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APPU_ID))
  // 使用门禁：组织外用户直链建会话 404，组织内成员 200
  const outsiderSession = await api('POST', `/api/app/${APPU_ID}/session`, { token: outsider.token, body: {} })
  check('PB-20i 组织外用户直链建会话 404', outsiderSession.status === 404, `${outsiderSession.status} ${outsiderSession.text.slice(0, 140)}`)
  check('PB-20j 组织内成员建会话 200', (await api('POST', `/api/app/${APPU_ID}/session`, { token: member.token, body: {} })).status === 200)

  // PB-21 全员重申覆盖：下架 → 按全员重新申请 → 通过 → 组织外用户也可见（@PB-S21）
  check('PB-21a 下架清可视范围 200', (await api('POST', '/api/publication/takedown', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID } })).status === 200)
  check('PB-21b 下架后组织内用户也不可见', !((await api('GET', '/api/app/public/list', { token: member.token })).json?.items ?? []).some(i => i.appId === APPU_ID))
  const applyAll = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'all' } })
  check('PB-21c 全员范围重新申请 200', applyAll.status === 200, `${applyAll.status} ${applyAll.text.slice(0, 140)}`)
  check('PB-21d 审批通过 200', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: String(applyAll.json?.value), isApprove: true } })).status === 200)
  check('PB-21e 全员范围组织外用户广场可见', ((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APPU_ID))
  check('PB-21f 全员范围组织外用户可建会话', (await api('POST', `/api/app/${APPU_ID}/session`, { token: outsider.token, body: {} })).status === 200)

  // PB-22 停用联动：待审申请撤下 + is_public=false + 广场不可见（@PB-S22，后端沿用撤回发布语义）
  const applyP = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID, visibleScope: 'all' } })
  check('PB-22a 已公开重复申请 409（先下架）', applyP.status === 409)
  check('PB-22b 停用（撤回发布）200', (await api('POST', `/api/app/${APPU_ID}/unpublish`, { token: owner.token })).status === 200)
  check('PB-22c 停用后 is_public=false 且 publishStatus=0', (await api('GET', `/api/app/${APPU_ID}`, { token: owner.token })).json?.isPublic === false && (await api('GET', `/api/app/${APPU_ID}`, { token: owner.token })).json?.publishStatus === 0)
  check('PB-22d 停用后广场不可见', !((await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items ?? []).some(i => i.appId === APPU_ID))
  check('PB-22e 停用后未发布不可再申请 400', (await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPU_ID } })).status === 400)

  // PB-23 待审核数量角标接口：门禁 + 申请后 +1 + 审批通过后回落（@PB-S23）
  check('PB-23a 未登录查待审核数量 401', (await api('GET', '/api/publication/pending-count')).status === 401)
  check('PB-23b 非管理员查待审核数量 403', (await api('GET', '/api/publication/pending-count', { token: owner.token })).status === 403)
  const appBadge = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '角标应用-' + TS, appType: 'agent' } })
  const APPB_ID = String(appBadge.json?.value ?? '')
  await api('POST', `/api/app/${APPB_ID}/publish`, { token: owner.token })
  const pendBefore = (await api('GET', '/api/publication/pending-count', { token: adminToken })).json?.count ?? -1
  const applyB = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APPB_ID, version: '1.0.0' } })
  check('PB-23c 角标应用申请上架 200', applyB.status === 200, `${applyB.status} ${applyB.text.slice(0, 140)}`)
  const pendAfter = (await api('GET', '/api/publication/pending-count', { token: adminToken })).json?.count ?? -1
  check('PB-23d 申请后待审核数量 +1', pendAfter === pendBefore + 1, `before=${pendBefore} after=${pendAfter}`)
  check('PB-23e 审批通过后数量回落', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: String(applyB.json?.value), isApprove: true } })).status === 200
    && (await api('GET', '/api/publication/pending-count', { token: adminToken })).json?.count === pendBefore)

  // ===== 应用收藏扩展（@AP-S132 收藏/取消 + @AP-S133 门禁 + @AP-S134 收藏置顶） =====

  // 三个夹具应用：F1/F3 全员范围、F2 组织范围（研发部，member 在范围内、outsider 在范围外），发布次序 F1→F2→F3（发布时间倒序基线 F3>F2>F1）
  const mkFav = async (name, orgScoped) => {
    const r = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `${name}-${TS}`, appType: 'agent' } })
    const id = String(r.json?.value ?? '')
    await api('POST', `/api/app/${id}/publish`, { token: owner.token })
    const body = orgScoped ? { resourceType: 'app', resourceId: id, visibleScope: 'organization', visibleOrgGrants: [{ orgRoleId: CHILD_ORG, scope: 'down' }] } : { resourceType: 'app', resourceId: id, visibleScope: 'all' }
    const a = await api('POST', '/api/publication/apply', { token: owner.token, body })
    if (a.status !== 200) throw new Error(`夹具 ${name} 申请失败: ${a.status} ${a.text.slice(0, 120)}`)
    const rv = await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: String(a.json?.value), isApprove: true } })
    if (rv.status !== 200) throw new Error(`夹具 ${name} 审批失败: ${rv.status}`)
    return id
  }
  const FAV1 = await mkFav('收藏全员甲', false)
  const FAV2 = await mkFav('收藏组织乙', true)
  const FAV3 = await mkFav('收藏全员丙', false)

  check('PB-24a 未登录收藏 401', (await api('POST', `/api/app/${FAV1}/favorite`)).status === 401)
  check('PB-24b 组织外用户收藏组织范围应用 404', (await api('POST', `/api/app/${FAV2}/favorite`, { token: outsider.token })).status === 404)
  check('PB-24c 停用未公开应用收藏 404', (await api('POST', `/api/app/${APPU_ID}/favorite`, { token: outsider.token })).status === 404)
  check('PB-24d 组织外用户收藏全员应用 200', (await api('POST', `/api/app/${FAV1}/favorite`, { token: outsider.token })).status === 200)
  check('PB-24e 组织内用户收藏组织范围应用 200', (await api('POST', `/api/app/${FAV2}/favorite`, { token: member.token })).status === 200)
  check('PB-24f 重复收藏幂等 200', (await api('POST', `/api/app/${FAV2}/favorite`, { token: member.token })).status === 200)

  // 收藏置顶：member（组织范围内）收藏 FAV2 后必居首位，非收藏组内保持发布时间倒序（FAV3>F2>F1 基线的相对序）；
  // 断言用相对序，避免开发库存量公开应用（热度更高）挤占绝对位置
  const memberIds = (await api('GET', '/api/app/public/list', { token: member.token })).json?.items?.map(i => i.appId) ?? []
  check('PB-24g 收藏应用置顶', memberIds[0] === FAV2 && memberIds.indexOf(FAV3) < memberIds.indexOf(FAV1), JSON.stringify(memberIds.slice(0, 6)))
  const memberFavItem = (await api('GET', '/api/app/public/list', { token: member.token })).json?.items?.find(i => i.appId === FAV2)
  check('PB-24h 列表回显 isFavorite=true', memberFavItem?.isFavorite === true, JSON.stringify(memberFavItem))
  const outsiderIds = (await api('GET', '/api/app/public/list', { token: outsider.token })).json?.items?.map(i => i.appId) ?? []
  check('PB-24i 收藏按用户隔离', outsiderIds[0] === FAV1 && !outsiderIds.includes(FAV2), JSON.stringify(outsiderIds.slice(0, 6)))

  check('PB-24j 取消收藏 200', (await api('DELETE', `/api/app/${FAV2}/favorite`, { token: member.token })).status === 200)
  check('PB-24k 未收藏重复取消幂等 200', (await api('DELETE', `/api/app/${FAV2}/favorite`, { token: member.token })).status === 200)
  const memberIdsAfter = (await api('GET', '/api/app/public/list', { token: member.token })).json?.items?.map(i => i.appId) ?? []
  check('PB-24l 取消后恢复热度（发布时间）序', memberIdsAfter.indexOf(FAV3) < memberIdsAfter.indexOf(FAV2) && memberIdsAfter.indexOf(FAV2) < memberIdsAfter.indexOf(FAV1), JSON.stringify(memberIdsAfter.slice(0, 6)))

  // 下架后遗留收藏仍可取消（不校验应用状态，清理不阻塞）
  check('PB-24m 下架应用仍可取消收藏 200', (await api('POST', '/api/publication/takedown', { token: owner.token, body: { resourceType: 'app', resourceId: FAV1 } })).status === 200
    && (await api('DELETE', `/api/app/${FAV1}/favorite`, { token: outsider.token })).status === 200)

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error(e); process.exit(1) })
