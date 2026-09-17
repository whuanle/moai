// 技能市场 e2e（SM）
// 用法：node local-dev/skill-market-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 覆盖：my_list/team_list/market_list 可见性、详情/下载可见性、技能上架审批（申请/撤回/审批通过置 is_public）、
//       内置技能上架/下载保护、删除技能联动清理待审核申请
// 前置：后端运行中，MinIO 可用（技能包文件走三段直传），admin 账号可登录。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'

let passed = 0
let failed = 0
const check = (name, cond, extra = '') => {
  if (cond) { passed++ ; console.log(`  ✓ ${name}`) }
  else { failed++ ; console.error(`  ✗ ${name} ${extra}`) }
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

/** 技能包文件三段直传（preupload → PUT → complete），返回 file id */
async function uploadSkillFile(token, fileName) {
  const bytes = Buffer.from(`#!/usr/bin/env python\n# skill-market-e2e ${fileName} ${TS}\n`)
  const sha256 = crypto.createHash('sha256').update(bytes).digest('hex')
  const pre = await api('POST', '/api/skill/file/preupload', {
    token,
    body: { fileName, contentType: 'text/x-python', fileSize: bytes.length, sha256 },
  })
  if (pre.status !== 200 || !pre.json?.fileId) throw new Error(`技能文件预上传失败: ${pre.status} ${pre.text.slice(0, 160)}`)
  if (!pre.json.isExist && pre.json.uploadUrl) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'text/x-python' }, body: bytes })
    if (!put.ok) throw new Error(`技能文件直传失败: ${put.status}`)
    const complete = await api('POST', '/api/skill/file/complete', { token, body: { fileId: pre.json.fileId, isSuccess: true } })
    if (complete.status !== 200) throw new Error(`技能文件完成上传失败: ${complete.status}`)
  }
  return pre.json.fileId
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const alice = await mkuser('sa')    // 个人技能创建者
  const bob = await mkuser('sb')      // 旁观者
  const tOwner = await mkuser('sto')  // 团队 Owner
  const tMember = await mkuser('stm') // 团队 Member

  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken
  if (!adminToken) throw new Error('admin 登录失败（admin/abcd123456），无法验证管理员审批')

  const TID = Number((await api('POST', '/api/team', { token: tOwner.token, body: { name: 'skill-mkt-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: tOwner.token, body: { userId: tMember.userId, role: 0 } })

  console.log('\n== 我的技能（my_list）与可见性 ==')
  const file1 = await uploadSkillFile(alice.token, `gen_${TS}.py`)
  const c1 = await api('POST', '/api/skill', {
    token: alice.token,
    body: {
      key: 'mkt_' + TS, name: '市场技能-' + TS, description: '供市场流转', instructions: '# 使用说明',
      files: [{ path: `gen_${TS}.py`, fileId: file1, fileName: `gen_${TS}.py` }], teamId: 0,
    },
  })
  check('SM-01 创建个人技能（含已上传文件）200', c1.status === 200 && !!c1.json?.value, `${c1.status} ${c1.text.slice(0, 140)}`)
  const SKILL = String(c1.json?.value)

  const myList = await api('GET', '/api/skill/my_list', { token: alice.token })
  const mine = (myList.json?.items ?? []).find((x) => x.id === SKILL)
  check('SM-02 my_list 含个人技能且未公开无待审', myList.status === 200 && mine?.isPublic === false && mine?.pendingPublicationId == null && mine?.fileCount === 1, JSON.stringify(mine))
  const bobList = await api('GET', '/api/skill/my_list', { token: bob.token })
  check('SM-03 他人 my_list 不含该技能', !(bobList.json?.items ?? []).some((x) => x.id === SKILL))

  console.log('\n== 详情/下载可见性 ==')
  check('SM-04 归属人详情 200', (await api('GET', `/api/skill/${SKILL}`, { token: alice.token })).status === 200)
  check('SM-05 旁观者详情 403', (await api('GET', `/api/skill/${SKILL}`, { token: bob.token })).status === 403)
  const dl1 = await api('GET', `/api/skill/${SKILL}/download`, { token: alice.token })
  const dlUrl = String(dl1.json?.items?.[0]?.downloadUrl ?? '')
  check('SM-06 归属人下载地址 200 含预签名 URL 且带附件下载头', dl1.status === 200 && dlUrl.length > 0 && dlUrl.includes('response-content-disposition'), `${dl1.status} ${dlUrl.slice(0, 160)}`)
  check('SM-07 旁观者下载地址 403', (await api('GET', `/api/skill/${SKILL}/download`, { token: bob.token })).status === 403)

  console.log('\n== 个人技能上架审批 ==')
  check('SM-08 他人申请上架 403', (await api('POST', '/api/publication/apply', { token: bob.token, body: { resourceType: 'skill', resourceId: SKILL } })).status === 403)
  const apply1 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'skill', resourceId: SKILL, applyReason: '共享技能' } })
  const PB1 = String(apply1.json?.value ?? '')
  check('SM-09 创建人申请上架 200', apply1.status === 200 && Number(PB1) > 0, `${apply1.status} ${apply1.text.slice(0, 140)}`)
  check('SM-10 重复申请 409', (await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'skill', resourceId: SKILL } })).status === 409)
  const pendingList = await api('GET', '/api/skill/my_list', { token: alice.token })
  const pendingItem = (pendingList.json?.items ?? []).find((x) => x.id === SKILL)
  check('SM-11 my_list 回显待审核申请 id', pendingItem?.pendingPublicationId === PB1, JSON.stringify(pendingItem))
  check('SM-12 他人撤回 403', (await api('POST', '/api/publication/withdraw', { token: bob.token, body: { publicationId: PB1 } })).status === 403)
  check('SM-13 创建人撤回 200', (await api('POST', '/api/publication/withdraw', { token: alice.token, body: { publicationId: PB1 } })).status === 200)
  const afterWithdraw = (await api('GET', '/api/skill/my_list', { token: alice.token })).json?.items?.find((x) => x.id === SKILL)
  check('SM-14 撤回后待审核 id 清空', afterWithdraw?.pendingPublicationId == null, JSON.stringify(afterWithdraw))

  const apply2 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'skill', resourceId: SKILL } })
  const PB2 = String(apply2.json?.value ?? '')
  const review = await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB2, isApprove: true, reviewComment: 'ok' } })
  check('SM-15 admin 审批通过 200', review.status === 200, `${review.status} ${review.text.slice(0, 140)}`)
  check('SM-16 重复审批 409', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB2, isApprove: false } })).status === 409)

  console.log('\n== 技能市场（market_list）==')
  const market = await api('GET', '/api/skill/market_list', { token: bob.token })
  const marketItem = (market.json?.items ?? []).find((x) => x.id === SKILL)
  check('SM-17 市场列表含已上架技能', market.status === 200 && marketItem?.isPublic === true, JSON.stringify(marketItem))
  check('SM-18 已公开后旁观者详情 200', (await api('GET', `/api/skill/${SKILL}`, { token: bob.token })).status === 200)
  check('SM-19 已公开后旁观者下载 200', (await api('GET', `/api/skill/${SKILL}/download`, { token: bob.token })).status === 200)

  console.log('\n== 团队技能上架 ==')
  const file2 = await uploadSkillFile(tOwner.token, `team_${TS}.py`)
  const c2 = await api('POST', '/api/skill', {
    token: tOwner.token,
    body: { key: 'tmkt_' + TS, name: '团队市场技能-' + TS, instructions: '# 团队技能', files: [{ path: `team_${TS}.py`, fileId: file2, fileName: `team_${TS}.py` }], teamId: TID },
  })
  const TSKILL = String(c2.json?.value ?? '')
  check('SM-20 Owner 创建团队技能 200', c2.status === 200 && !!TSKILL, `${c2.status} ${c2.text.slice(0, 140)}`)
  const teamList = await api('GET', `/api/skill/team_list?teamId=${TID}`, { token: tMember.token })
  check('SM-21 团队成员 team_list 含团队技能', (teamList.json?.items ?? []).some((x) => x.id === TSKILL))
  check('SM-22 非成员 team_list 404', (await api('GET', `/api/skill/team_list?teamId=${TID}`, { token: bob.token })).status === 404)
  check('SM-23 Member 申请上架团队技能 403', (await api('POST', '/api/publication/apply', { token: tMember.token, body: { resourceType: 'skill', resourceId: TSKILL } })).status === 403)
  const apply3 = await api('POST', '/api/publication/apply', { token: tOwner.token, body: { resourceType: 'skill', resourceId: TSKILL } })
  const PB3 = String(apply3.json?.value ?? '')
  const review3 = await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB3, isApprove: true } })
  check('SM-24 Owner 申请+admin 通过 200', apply3.status === 200 && review3.status === 200, `${apply3.status}/${review3.status} ${review3.text.slice(0, 140)}`)
  const market2 = await api('GET', '/api/skill/market_list', { token: alice.token })
  check('SM-25 市场列表含团队技能', (market2.json?.items ?? []).some((x) => x.id === TSKILL))

  console.log('\n== 内置技能保护 ==')
  const sysList = await api('GET', '/api/skill/list?pageNo=1&pageSize=50', { token: adminToken })
  const sysSkill = (sysList.json?.items ?? []).find((x) => x.isSystem === true)
  if (sysSkill) {
    check('SM-26 申请上架内置技能 400', (await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'skill', resourceId: sysSkill.id } })).status === 400)
    check('SM-27 内置技能下载 400', (await api('GET', `/api/skill/${sysSkill.id}/download`, { token: adminToken })).status === 400)
    check('SM-28 市场列表不含内置技能', !(market2.json?.items ?? []).some((x) => x.isSystem === true))
  } else {
    // 内置技能由迁移种子（SkillSeed HasData）提供，存量库可能没有；无种子时跳过而非误报
    console.log('  - SM-26~28 SKIP：库中无内置技能种子（SkillSeed），内置保护场景见 docs/skill/bdd.md')
  }

  console.log('\n== 删除联动清理待审核申请 ==')
  const file3 = await uploadSkillFile(alice.token, `del_${TS}.py`)
  const c3 = await api('POST', '/api/skill', {
    token: alice.token,
    body: { key: 'del_' + TS, name: '待删技能-' + TS, files: [{ path: `del_${TS}.py`, fileId: file3, fileName: `del_${TS}.py` }], teamId: 0 },
  })
  const DEL_SKILL = String(c3.json?.value ?? '')
  const apply4 = await api('POST', '/api/publication/apply', { token: alice.token, body: { resourceType: 'skill', resourceId: DEL_SKILL } })
  const PB4 = String(apply4.json?.value ?? '')
  check('SM-29 删除前申请已提交', apply4.status === 200 && Number(PB4) > 0)
  check('SM-30 删除技能 200', (await api('DELETE', `/api/skill/${DEL_SKILL}`, { token: alice.token })).status === 200)
  check('SM-31 待审核申请已联动清理（审批 404）', (await api('POST', '/api/publication/review', { token: adminToken, body: { publicationId: PB4, isApprove: true } })).status === 404)

  console.log(`\n结果：${passed} 通过，${failed} 失败`)
  process.exit(failed > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error(e)
  process.exit(1)
})
