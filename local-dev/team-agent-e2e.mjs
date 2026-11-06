// 团队智能体管理 E2E（场景 @AG-S1~S10；后端 127.0.0.1:5210，可用 AG_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.AG_BASE ?? 'http://127.0.0.1:5210'
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

const mkSub = (key, extra = {}) => ({ key, name: `${key} 的中文名`, description: `${key} 的职责描述：何时派发它`, prompt: `你是${key}专员`, ...extra })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ao')
  const member = await mkuser('am')
  const outsider = await mkuser('ax')

  // 种子管理员（admin/abcd123456）：仅补种端点等平台管理操作使用
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = adminLogin.json?.accessToken

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'ag-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 'member' } })

  // 造一个真实知识库供依赖回读
  const wikiId = Number((await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'ag-wiki-' + TS } })).json.value)
  if (!wikiId) console.log('WARN | 知识库创建失败，依赖回读用例将降级', wikiId)

  // @AG-S1 门禁
  check('AG-01 无 token 查列表 401', (await api('GET', `/api/team-agent/list?teamId=${TID}`)).status === 401)
  check('AG-02 Member 创建 403', (await api('POST', '/api/team-agent', { token: member.token, body: { teamId: TID, key: 'nope_x', name: 'nope', mainPrompt: 'p' } })).status === 403)
  check('AG-03a 非成员创建 404', (await api('POST', '/api/team-agent', { token: outsider.token, body: { teamId: TID, key: 'nope_x', name: 'nope', mainPrompt: 'p' } })).status === 404)

  // @AG-S2 创建参数校验 400
  check('AG-04a 名称空 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n_a', name: '', mainPrompt: 'p' } })).status === 400)
  check('AG-04b 智能体 key 非法 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'Bad_Key', name: 'n1', mainPrompt: 'p' } })).status === 400)
  check('AG-04c key 空 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: '', name: 'n1b', mainPrompt: 'p' } })).status === 400)
  check('AG-04d 子智能体标识非法 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n2', name: 'n2', mainPrompt: 'p', subs: [mkSub('1Bad')] } })).status === 400)
  check('AG-04e 子智能体描述空 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n3', name: 'n3', mainPrompt: 'p', subs: [{ key: 'okkey', name: 'okname', description: '', prompt: 'p' }] } })).status === 400)
  check('AG-04f 子智能体名空 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n3b', name: 'n3b', mainPrompt: 'p', subs: [{ key: 'okkey', name: '', description: 'd', prompt: 'p' }] } })).status === 400)
  check('AG-04g 子智能体超 20 个 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n4', name: 'n4', mainPrompt: 'p', subs: Array.from({ length: 21 }, (_, i) => mkSub(`s${i}`)) } })).status === 400)
  check('AG-04h 子标识组内重复 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'n5', name: 'n5', mainPrompt: 'p', subs: [mkSub('dup'), mkSub('dup')] } })).status === 400)

  // @AG-S5 依赖校验 400
  const fakePlugin = '11111111-1111-1111-1111-111111111111'
  check('AG-05a 插件不可用 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'd1', name: 'd1', mainPrompt: 'p', mainPluginIds: [fakePlugin] } })).status === 400)
  check('AG-05b 子智能体插件不可用 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'd2', name: 'd2', mainPrompt: 'p', subs: [mkSub('s1', { pluginIds: [fakePlugin] })] } })).status === 400)
  check('AG-05c wiki 跨团队 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'd3', name: 'd3', mainPrompt: 'p', mainWikiIds: [999999999] } })).status === 400)
  check('AG-05d 知识图谱跨团队 400', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'd4', name: 'd4', mainPrompt: 'p', mainGraphIds: [999999999] } })).status === 400)
  check('AG-05e 创建带伪造头像 404', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: 'd5', name: 'd5', mainPrompt: 'p', avatar: 'fake/objectkey.png' } })).status === 404)

  // @AG-S3 创建成功 + 回读
  const createBody = {
    teamId: TID,
    key: `ag_main_${TS}`,
    name: 'ag-main-' + TS,
    description: '主智能体：负责拆解任务并派发',
    mainPrompt: '你是团队负责人，按需派发子智能体完成研究。',
    mainWikiIds: wikiId ? [wikiId] : [],
    subs: [mkSub('researcher', wikiId ? { wikiIds: [wikiId] } : {}), mkSub('writer')],
  }
  const created = await api('POST', '/api/team-agent', { token: owner.token, body: createBody })
  const AGID = created.json?.value
  check('AG-06 创建成功 200 返回 guid', created.status === 200 && typeof AGID === 'string' && AGID.length === 36, `${created.status} ${created.text.slice(0, 120)}`)

  {
    const r = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: member.token })
    const items = r.json?.items ?? []
    const mine = items.find((x) => x.id === AGID)
    check('AG-07a Member 列表 200 且 myRole=0', r.status === 200 && r.json?.myRole === 0)
    check('AG-07b 列表回读 名称/子数/审计字段', !!mine && mine.name === createBody.name && mine.subCount === 2 && !!mine.createUserName && !!mine.updateTime, JSON.stringify(mine))
  }

  {
    const r = await api('GET', `/api/team-agent/${AGID}`, { token: owner.token })
    const a = r.json?.agent
    check('AG-08 详情回读 key/提示词/描述/启用', r.status === 200 && a?.key === createBody.key && a?.mainPrompt === createBody.mainPrompt && a?.description === createBody.description && a?.enabled === true, r.text.slice(0, 200))
    check('AG-08b 详情回读 子智能体定义（key+中文名）', a?.subs?.length === 2 && a.subs[0]?.key === 'researcher' && !!a.subs[0]?.name && !!a.subs[0]?.description && !!a.subs[0]?.prompt, JSON.stringify(a?.subs))
    check('AG-08c 详情回读 知识库依赖', !wikiId || (a?.mainWikiIds?.map(Number).includes(wikiId) && a.subs[0]?.wikiIds?.map(Number).includes(wikiId)), JSON.stringify({ main: a?.mainWikiIds, sub: a?.subs?.[0]?.wikiIds }))
  }

  // @AG-S4 团队内重名 409（name 与 key 双唯一）
  check('AG-09a 同团队重名 409', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: `other_${TS}`, name: createBody.name, mainPrompt: 'p' } })).status === 409)
  check('AG-09b 同团队 key 重名 409', (await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: createBody.key, name: `other-${TS}`, mainPrompt: 'p' } })).status === 409)

  // @AG-S7 列表关键字过滤 + 非成员 404
  {
    const hit = await api('GET', `/api/team-agent/list?teamId=${TID}&searchText=${createBody.name}`, { token: owner.token })
    const miss = await api('GET', `/api/team-agent/list?teamId=${TID}&searchText=no-such-agent-${TS}`, { token: owner.token })
    check('AG-10a 关键字命中', hit.status === 200 && (hit.json?.items ?? []).some((x) => x.id === AGID))
    check('AG-10b 关键字不命中为空', miss.status === 200 && (miss.json?.items ?? []).length === 0)
    check('AG-10c 非成员列表 404', (await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: outsider.token })).status === 404)
    check('AG-10d 非成员详情 404', (await api('GET', `/api/team-agent/${AGID}`, { token: outsider.token })).status === 404)
  }

  // @AG-S6 更新：整替换 + 门禁 + 重名
  check('AG-11 Member 更新 403', (await api('PUT', `/api/team-agent/${AGID}`, { token: member.token, body: { name: 'x', mainPrompt: 'p' } })).status === 403)
  check('AG-12 非成员更新 404', (await api('PUT', `/api/team-agent/${AGID}`, { token: outsider.token, body: { name: 'x', mainPrompt: 'p' } })).status === 404)

  const second = await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: `ag_second_${TS}`, name: 'ag-second-' + TS, mainPrompt: '第二个智能体' } })
  const AGID2 = second.json?.value
  check('AG-13a 第二个智能体创建 200', second.status === 200 && typeof AGID2 === 'string', `${second.status}`)
  check('AG-13b 更新撞已存在名称 409', (await api('PUT', `/api/team-agent/${AGID}`, { token: owner.token, body: { name: 'ag-second-' + TS, mainPrompt: 'p' } })).status === 409)

  const updateBody = {
    name: 'ag-main-v2-' + TS,
    description: '改版后',
    enabled: false,
    mainPrompt: '你是团队负责人 v2。',
    subs: [mkSub('writer', { description: '只负责成稿' })],
  }
  const up = await api('PUT', `/api/team-agent/${AGID}`, { token: owner.token, body: updateBody })
  {
    const r = await api('GET', `/api/team-agent/${AGID}`, { token: owner.token })
    const a = r.json?.agent
    check('AG-14 更新整替换生效（名称/停用/子列表仅剩 writer）', up.status === 200 && a?.name === updateBody.name && a?.enabled === false && a?.subs?.length === 1 && a.subs[0]?.key === 'writer', `${up.status} ${JSON.stringify({ name: a?.name, enabled: a?.enabled, subs: a?.subs?.length })}`)
  }
  {
    const r = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: member.token })
    const mine = (r.json?.items ?? []).find((x) => x.id === AGID)
    check('AG-15 列表回显停用状态', mine?.enabled === false, JSON.stringify(mine))
  }

  // @AG-S8 头像
  check('AG-16 Member 设置头像 403', (await api('POST', `/api/team-agent/${AGID}/avatar`, { token: member.token, body: { objectKey: 'fake/x.png' } })).status === 403)
  check('AG-17 伪造 objectKey 404', (await api('POST', `/api/team-agent/${AGID}/avatar`, { token: owner.token, body: { objectKey: 'fake/objectkey.png' } })).status === 404)

  // @AG-S9 删除与软删重名释放
  check('AG-18 Member 删除 403', (await api('DELETE', `/api/team-agent/${AGID2}`, { token: member.token })).status === 403)
  check('AG-19 非成员删除 404', (await api('DELETE', `/api/team-agent/${AGID2}`, { token: outsider.token })).status === 404)
  check('AG-20 Owner 删除 200', (await api('DELETE', `/api/team-agent/${AGID2}`, { token: owner.token })).status === 200)
  check('AG-21 删除后详情 404', (await api('GET', `/api/team-agent/${AGID2}`, { token: owner.token })).status === 404)
  check('AG-22 删除后列表不含', !((await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).some((x) => x.id === AGID2))
  const recreated = await api('POST', '/api/team-agent', { token: owner.token, body: { teamId: TID, key: `ag_second_${TS}`, name: 'ag-second-' + TS, mainPrompt: '重建同名同 key' } })
  check('AG-23 软删后同名同 key 可重建', recreated.status === 200, `${recreated.status} ${recreated.text.slice(0, 120)}`)

  // @AG-S10 内置智能体种子：团队创建时自动种入规划师/构建师（幂等）+ 补种端点
  {
    const r = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: owner.token })
    const items = r.json?.items ?? []
    const plan = items.find((x) => x.isSystem && x.key === 'plan')
    const build = items.find((x) => x.isSystem && x.key === 'build')
    check('AG-24 新团队自动种入内置规划师/构建师（key=plan/build, isSystem=true）', !!plan && !!build, JSON.stringify(items.map((x) => ({ k: x.key, n: x.name, s: x.isSystem }))))
    check('AG-25 内置智能体自带子智能体（subCount>0）', !!plan && (plan.subCount ?? 0) > 0 && !!build && (build.subCount ?? 0) > 0)
    check('AG-26 删除内置智能体 400', !!plan && (await api('DELETE', `/api/team-agent/${plan.id}`, { token: owner.token })).status === 400)

    if (plan) {
      const d = await api('GET', `/api/team-agent/${plan.id}`, { token: owner.token })
      const a = d.json?.agent
      check('AG-27a 内置详情回读 isSystem + 子智能体 key/name', d.status === 200 && a?.isSystem === true && a?.subs?.length > 0 && !!a.subs[0]?.key && !!a.subs[0]?.name, JSON.stringify(a?.subs?.[0]))
      const up = await api('PUT', `/api/team-agent/${plan.id}`, {
        token: owner.token,
        body: {
          name: a.name,
          description: `${a.description}`,
          enabled: a.enabled,
          mainPrompt: a.mainPrompt,
          mainPluginIds: [],
          mainWikiIds: [],
          mainGraphIds: [],
          subs: a.subs,
        },
      })
      const d2 = await api('GET', `/api/team-agent/${plan.id}`, { token: owner.token })
      check('AG-27b 内置可编辑（整替换后子智能体保留）', up.status === 200 && (d2.json?.agent?.subs?.length ?? 0) === (a.subs?.length ?? 0), `up=${up.status}`)
    }

    const memberView = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: member.token })
    check('AG-28 Member 列表同样可见内置智能体', (memberView.json?.items ?? []).some((x) => x.isSystem))

    check('AG-29 Member 调补种端点 403', (await api('POST', '/api/team-agent/seed-builtin', { token: member.token })).status === 403)
    const seed = await api('POST', '/api/team-agent/seed-builtin', { token: adminToken })
    check('AG-30 Admin 补种端点 200（幂等返回数量）', seed.status === 200 && typeof seed.json?.value === 'number' && seed.json.value >= 0, `${seed.status} ${seed.text.slice(0, 120)}`)
    const afterSeed = await api('GET', `/api/team-agent/list?teamId=${TID}`, { token: owner.token })
    const systemCount = (afterSeed.json?.items ?? []).filter((x) => x.isSystem).length
    check('AG-31 补种幂等：内置智能体不重复', systemCount === 2, `systemCount=${systemCount}`)

    // 新建第二个团队验证创建钩子
    const TID2 = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'ag-team2-' + TS } })).json.value)
    await api('POST', `/api/team/${TID2}/users`, { token: owner.token, body: { userId: member.userId, role: 'member' } })
    const r2 = await api('GET', `/api/team-agent/list?teamId=${TID2}`, { token: member.token })
    const items2 = r2.json?.items ?? []
    check(
      'AG-32 第二个团队创建时同样自动种入',
      items2.some((x) => x.isSystem && /Plan/.test(x.name || '')) && items2.some((x) => x.isSystem && /Build/.test(x.name || '')),
      JSON.stringify(items2.map((x) => x.name)),
    )
  }

  console.log(`\n===== 团队智能体 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((err) => {
  console.error('E2E 执行异常:', err)
  process.exit(1)
})
