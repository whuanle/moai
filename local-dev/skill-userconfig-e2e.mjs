// 技能三级权限 + 用户级应用配置 e2e（SK）
// 用法：node local-dev/skill-userconfig-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 覆盖：个人/团队技能维护权限、options 可见范围、app_user_config 保存回显与校验
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'

let passed = 0
let failed = 0
const check = (name, cond) => {
  if (cond) { passed++ ; console.log(`  ✓ ${name}`) }
  else { failed++ ; console.error(`  ✗ ${name}`) }
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

  const alice = await mkuser('sk')    // 个人技能创建者
  const bob = await mkuser('sb')      // 旁观者
  const tOwner = await mkuser('so')   // 团队 Owner
  const tMember = await mkuser('sm')  // 团队 Member

  const TID = Number((await api('POST', '/api/team', { token: tOwner.token, body: { name: 'skill-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: tOwner.token, body: { userId: tMember.userId, role: 0 } })

  const APP_ID = String((await api('POST', '/api/app', { token: tOwner.token, body: { teamId: TID, name: '技能应用', appType: 'agent' } })).json.value)

  console.log('\n== 个人技能（TeamId=0，归属创建人）==')
  const c1 = await api('POST', '/api/skill', { token: alice.token, body: { key: 'mine_' + TS, name: '我的技能', description: '个人', instructions: '# 用法', files: [], teamId: 0 } })
  check('SK-01 普通用户创建个人技能 200', c1.status === 200 && !!c1.json?.value)
  const MY_SKILL = String(c1.json?.value)

  const o1 = await api('GET', '/api/skill/options?teamId=0&includePersonal=true', { token: alice.token })
  check('SK-02 本人 options 含个人技能', o1.status === 200 && (o1.json.items ?? []).some((x) => x.id === MY_SKILL))
  const o2 = await api('GET', '/api/skill/options?teamId=0&includePersonal=true', { token: bob.token })
  check('SK-03 他人 options 不含他人个人技能', !(o2.json.items ?? []).some((x) => x.id === MY_SKILL))

  console.log('\n== 团队技能（TeamId>0，需团队管理员）==')
  const c2 = await api('POST', '/api/skill', { token: tMember.token, body: { key: 'team_' + TS, name: '成员技能', files: [], teamId: TID } })
  check('SK-04 Member 创建团队技能 403', c2.status === 403)
  const c3 = await api('POST', '/api/skill', { token: tOwner.token, body: { key: 'team_' + TS, name: '团队技能', files: [], teamId: TID } })
  check('SK-05 Owner 创建团队技能 200', c3.status === 200)
  const TEAM_SKILL = String(c3.json?.value)

  const o3 = await api('GET', `/api/skill/options?teamId=${TID}`, { token: tMember.token })
  check('SK-06 团队成员 options 含团队技能', (o3.json.items ?? []).some((x) => x.id === TEAM_SKILL))
  const o4 = await api('GET', `/api/skill/options?teamId=${TID}&includePersonal=true`, { token: tMember.token })
  check('SK-07 团队成员 options 不含他人个人技能', !(o4.json.items ?? []).some((x) => x.id === MY_SKILL))

  console.log('\n== 目标保护（归属人/团队管理员之外不可管理）==')
  const u1 = await api('PUT', `/api/skill/${MY_SKILL}`, { token: bob.token, body: { name: '改名', description: '', instructions: '', files: [] } })
  check('SK-08 他人更新他人个人技能 403', u1.status === 403)
  const d1 = await api('DELETE', `/api/skill/${MY_SKILL}`, { token: bob.token })
  check('SK-09 他人删除他人个人技能 403', d1.status === 403)
  const u2 = await api('PUT', `/api/skill/${MY_SKILL}`, { token: alice.token, body: { name: '我的技能2', description: '个人', instructions: '# 用法', files: [] } })
  check('SK-10 归属人更新个人技能 200', u2.status === 200)
  const dis = await api('PUT', `/api/skill/${TEAM_SKILL}/disable`, { token: tMember.token, body: { isDisable: true } })
  check('SK-11 Member 禁用团队技能 403', dis.status === 403)

  console.log('\n== 用户级应用配置（app_user_config）==')
  const uc1 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [TEAM_SKILL] } })
  check('UC-01 团队成员保存用户配置 200', uc1.status === 200)
  const q1 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-02 查询回显 skills', q1.status === 200 && JSON.stringify(q1.json.skills ?? []) === JSON.stringify([TEAM_SKILL]))
  check('UC-03 未绑定应用技能时 lockedSkills 为空', JSON.stringify(q1.json.lockedSkills ?? []) === '[]')
  check('UC-04 未设置专家时 promptId=0', Number(q1.json.promptId ?? 0) === 0)

  const uc2 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [MY_SKILL] } })
  check('UC-05 保存含他人个人技能 400', uc2.status === 400)
  const uc3 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 999999, skills: [] } })
  check('UC-06 保存不可用提示词 404', uc3.status === 404)

  // 覆盖保存（upsert 幂等）：再次保存清空技能
  const uc4 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [] } })
  const q2 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-07 重复保存为覆盖语义', uc4.status === 200 && (q2.json.skills ?? []).length === 0)

  const q3 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: bob.token })
  check('UC-08 非团队成员查询 404', q3.status === 404)

  console.log('\n== 清理 ==')
  const d2 = await api('DELETE', `/api/skill/${MY_SKILL}`, { token: alice.token })
  check('SK-12 归属人删除个人技能 200', d2.status === 200)

  console.log(`\n结果：${passed} 通过 / ${failed} 失败`)
  if (failed > 0) process.exit(1)
}

main().catch((e) => { console.error(e); process.exit(1) })
