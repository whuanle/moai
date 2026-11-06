// 技能三级权限 + 应用默认技能与可选专家绑定 + 用户级应用配置 e2e（SK/UC）
// 用法：node local-dev/skill-userconfig-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 覆盖：个人/团队技能维护权限、options 可见范围、管理员配置默认技能与绑定专家、
//       用户侧候选池=应用绑定∪市场公开∪本人个人（不含团队全量）、专家/技能保存与会话校验同口径
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

  // 对话模型必填（保存强校验）：造一个私有模型授权本团队，供应用配置保存用（无对话流量）
  const eaAdminToken = (await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })).json?.accessToken ?? ''
  let SKL_MODEL_ID = ''
  if (eaAdminToken) {
    const chName = `skl-cfg渠道-${TS}`
    await api('POST', '/api/ai/channel', { token: eaAdminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: 'http://127.0.0.1:9/v1', apiKey: 'skl-cfg-key', enabled: true, description: 'skill-userconfig 配置保存桩渠道' } })
    const channels = await api('GET', '/api/ai/channel', { token: eaAdminToken })
    const chId = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
    const modelName = `skl-cfg-chat-${TS}`
    await api('POST', '/api/ai/model', { token: eaAdminToken, body: { channelId: chId, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'skill-userconfig 配置保存桩模型' }, enabled: true, isPublic: false } })
    const models = await api('GET', `/api/ai/model?channelId=${chId}`, { token: eaAdminToken })
    SKL_MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
    const cur = await api('GET', `/api/ai/model/${SKL_MODEL_ID}/authorization`, { token: eaAdminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${SKL_MODEL_ID}/authorization`, { token: eaAdminToken, body: { teamIds } })
  }

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

  console.log('\n== 应用默认技能与可选专家绑定（app_agent_config）==')
  // 团队提示词：作为绑定专家
  const p1 = await api('POST', '/api/prompt', { token: tOwner.token, body: { teamId: TID, name: '团队专家', description: '', content: '你是团队专家', promptClassId: 0 } })
  check('UC-01 Owner 创建团队提示词 200', p1.status === 200 && !!p1.json?.value)
  const TEAM_PROMPT = Number(p1.json?.value ?? 0)

  // 他人个人提示词：对团队成员不可用
  const alicePrompt = await api('POST', '/api/prompt', { token: alice.token, body: { teamId: 0, name: '他人个人专家', description: '', content: 'x', promptClassId: 0 } })
  check('UC-02 个人提示词创建 200', alicePrompt.status === 200 && !!alicePrompt.json?.value)
  const OTHER_PROMPT = Number(alicePrompt.json?.value ?? 0)

  // 未绑定的第二个团队提示词/技能：验证候选池不读团队全量，仅绑定/市场/个人三来源
  const p2 = await api('POST', '/api/prompt', { token: tOwner.token, body: { teamId: TID, name: '未绑定团队专家', description: '', content: 'x', promptClassId: 0 } })
  check('UC-02b 未绑定团队提示词创建 200', p2.status === 200 && !!p2.json?.value)
  const UNBOUND_PROMPT = Number(p2.json?.value ?? 0)
  const c4 = await api('POST', '/api/skill', { token: tOwner.token, body: { key: 'team2_' + TS, name: '未绑定团队技能', files: [], teamId: TID } })
  check('UC-02c 未绑定团队技能创建 200', c4.status === 200 && !!c4.json?.value)
  const UNBOUND_SKILL = String(c4.json?.value)

  const acBad = await api('PUT', `/api/app/${APP_ID}/agent-config`, {
    token: tOwner.token,
    body: { modelId: SKL_MODEL_ID, prompt: '系统提示词', wikiIds: [], plugins: [], skills: [TEAM_SKILL], prompts: [OTHER_PROMPT], openingStatement: '', openingStatementEnabled: false },
  })
  check('UC-03a 绑定他人个人提示词（非市场/非本团队）400', acBad.status === 400)

  const ac1 = await api('PUT', `/api/app/${APP_ID}/agent-config`, {
    token: tOwner.token,
    body: { modelId: SKL_MODEL_ID, prompt: '系统提示词', wikiIds: [], plugins: [], skills: [TEAM_SKILL], prompts: [TEAM_PROMPT], openingStatement: '', openingStatementEnabled: false },
  })
  check('UC-03 管理员保存默认技能与绑定专家 200', ac1.status === 200)
  const acq = await api('GET', `/api/app/${APP_ID}/agent-config`, { token: tOwner.token })
  check('UC-04 应用配置回显 skills 与 prompts', acq.status === 200
    && JSON.stringify(acq.json.skills ?? []) === JSON.stringify([TEAM_SKILL])
    && JSON.stringify(acq.json.prompts ?? []) === JSON.stringify([TEAM_PROMPT]))

  console.log('\n== 用户级应用配置（候选池=绑定∪市场公开∪本人个人）==')
  const q1 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-05 未配置过：默认技能全部启用且目录回显', q1.status === 200
    && JSON.stringify(q1.json.skills ?? []) === JSON.stringify([TEAM_SKILL])
    && (q1.json.defaultSkills ?? []).some((x) => String(x.id) === TEAM_SKILL)
    && Number(q1.json.promptId ?? -1) === 0)
  check('UC-05b 技能候选池含绑定技能、不含未绑定团队技能', q1.status === 200
    && (q1.json.skillOptions ?? []).some((x) => String(x.id) === TEAM_SKILL)
    && !(q1.json.skillOptions ?? []).some((x) => String(x.id) === UNBOUND_SKILL))
  check('UC-05c 专家候选池含绑定专家、不含未绑定团队提示词', q1.status === 200
    && (q1.json.promptOptions ?? []).some((x) => Number(x.promptId) === TEAM_PROMPT)
    && !(q1.json.promptOptions ?? []).some((x) => Number(x.promptId) === UNBOUND_PROMPT))

  const uc1 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: TEAM_PROMPT, skills: [TEAM_SKILL] } })
  check('UC-06 保存绑定专家与绑定技能 200', uc1.status === 200)
  const q2 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-07 保存后回显 promptId 与 skills', Number(q2.json.promptId) === TEAM_PROMPT && JSON.stringify(q2.json.skills ?? []) === JSON.stringify([TEAM_SKILL]))

  const uc2 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [MY_SKILL] } })
  check('UC-08 保存他人个人技能 400', uc2.status === 400)
  const uc3 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: OTHER_PROMPT, skills: [] } })
  check('UC-09 保存不可用专家（他人个人提示词）404', uc3.status === 404)
  const uc5 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [UNBOUND_SKILL] } })
  check('UC-14 保存未绑定团队技能 400', uc5.status === 400)
  const uc6 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: UNBOUND_PROMPT, skills: [] } })
  check('UC-15 保存未绑定团队提示词 404', uc6.status === 404)

  // 本人个人腿：成员自建个人技能/提示词后即可自选
  const mSkill = await api('POST', '/api/skill', { token: tMember.token, body: { key: 'member_' + TS, name: '成员个人技能', description: '个人', instructions: '# 用法', files: [], teamId: 0 } })
  check('UC-16 成员创建个人技能 200', mSkill.status === 200 && !!mSkill.json?.value)
  const MY_MEMBER_SKILL = String(mSkill.json?.value)
  const mPrompt = await api('POST', '/api/prompt', { token: tMember.token, body: { teamId: 0, name: '成员个人专家', description: '', content: 'x', promptClassId: 0 } })
  check('UC-17 成员创建个人提示词 200', mPrompt.status === 200 && !!mPrompt.json?.value)
  const MY_MEMBER_PROMPT = Number(mPrompt.json?.value ?? 0)

  const q5 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-18 候选池含本人个人技能与提示词', q5.status === 200
    && (q5.json.skillOptions ?? []).some((x) => String(x.id) === MY_MEMBER_SKILL)
    && (q5.json.promptOptions ?? []).some((x) => Number(x.promptId) === MY_MEMBER_PROMPT))
  const uc7 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: MY_MEMBER_PROMPT, skills: [MY_MEMBER_SKILL] } })
  const q6 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-19 保存本人个人技能与提示词 200 并回显', uc7.status === 200
    && (q6.json.skills ?? []).includes(MY_MEMBER_SKILL)
    && Number(q6.json.promptId) === MY_MEMBER_PROMPT)

  // 市场公开腿：未绑定的团队技能/提示词经上架审批公开后进入候选池
  const apSkill = await api('POST', '/api/publication/apply', { token: tOwner.token, body: { resourceType: 'skill', resourceId: UNBOUND_SKILL } })
  check('UC-20 技能上架申请 200', apSkill.status === 200 && !!apSkill.json?.value)
  const apPrompt = await api('POST', '/api/publication/apply', { token: tOwner.token, body: { resourceType: 'prompt', resourceId: String(UNBOUND_PROMPT) } })
  check('UC-21 提示词上架申请 200', apPrompt.status === 200 && !!apPrompt.json?.value)
  if (eaAdminToken && apSkill.json?.value && apPrompt.json?.value) {
    const rv1 = await api('POST', '/api/publication/review', { token: eaAdminToken, body: { publicationId: apSkill.json.value, isApprove: true } })
    const rv2 = await api('POST', '/api/publication/review', { token: eaAdminToken, body: { publicationId: apPrompt.json.value, isApprove: true } })
    check('UC-22 管理员审批通过 200', rv1.status === 200 && rv2.status === 200)
    const q7 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
    check('UC-23 上架公开后进入候选池（市场腿）', q7.status === 200
      && (q7.json.skillOptions ?? []).some((x) => String(x.id) === UNBOUND_SKILL)
      && (q7.json.promptOptions ?? []).some((x) => Number(x.promptId) === UNBOUND_PROMPT))
    const uc8 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: UNBOUND_PROMPT, skills: [UNBOUND_SKILL] } })
    check('UC-24 保存市场公开技能与提示词 200', uc8.status === 200)
  }

  // 覆盖保存（upsert 幂等）：全部取消后查询为空
  const uc4 = await api('PUT', `/api/app/${APP_ID}/userconfig`, { token: tMember.token, body: { promptId: 0, skills: [] } })
  const q3 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: tMember.token })
  check('UC-10 重复保存为覆盖语义（可取消全部默认技能）', uc4.status === 200 && (q3.json.skills ?? []).length === 0 && Number(q3.json.promptId) === 0)

  const q4 = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: bob.token })
  check('UC-11 非团队成员查询 404', q4.status === 404)

  console.log('\n== 会话专家绑定（个人/团队可用范围）==')
  // Member 发起会话要求应用已发布
  const pub = await api('POST', `/api/app/${APP_ID}/publish`, { token: tOwner.token })
  check('UC-12 发布应用 200', pub.status === 200)
  const s2 = await api('POST', `/api/app/${APP_ID}/session`, { token: tMember.token, body: { promptId: TEAM_PROMPT } })
  check('UC-13 创建会话绑定团队提示词 200', s2.status === 200 && !!s2.json?.value)
  if (s2.json?.value) await api('DELETE', `/api/app/session/${s2.json.value}`, { token: tMember.token })

  console.log('\n== 清理 ==')
  const d2 = await api('DELETE', `/api/skill/${MY_SKILL}`, { token: alice.token })
  check('SK-12 归属人删除个人技能 200', d2.status === 200)
  await api('DELETE', `/api/prompt/${OTHER_PROMPT}`, { token: alice.token })
  await api('DELETE', `/api/prompt/${TEAM_PROMPT}`, { token: tOwner.token })
  // 上架公开的资源先下架再删除（删除联动市场可见性，直接删除兜底）
  await api('POST', '/api/publication/takedown', { token: tOwner.token, body: { resourceType: 'skill', resourceId: UNBOUND_SKILL } })
  await api('POST', '/api/publication/takedown', { token: tOwner.token, body: { resourceType: 'prompt', resourceId: String(UNBOUND_PROMPT) } })
  await api('DELETE', `/api/skill/${UNBOUND_SKILL}`, { token: tOwner.token })
  await api('DELETE', `/api/prompt/${UNBOUND_PROMPT}`, { token: tOwner.token })
  await api('DELETE', `/api/skill/${MY_MEMBER_SKILL}`, { token: tMember.token })
  await api('DELETE', `/api/prompt/${MY_MEMBER_PROMPT}`, { token: tMember.token })

  console.log(`\n结果：${passed} 通过 / ${failed} 失败`)
  if (failed > 0) process.exit(1)
}

main().catch((e) => { console.error(e); process.exit(1) })
