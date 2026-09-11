// 应用管理 E2E（场景 @AP-Sn）
// 用法：node local-dev/app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/app.sql 建表。
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

  const owner = await mkuser('ao')
  const member = await mkuser('am')
  const outsider = await mkuser('ax')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'app-team-' + TS } })).json.value)
  // 0=Member 1=Admin 2=Owner（TeamRole 枚举）
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })

  // AP-01 未登录
  check('AP-01 未登录查应用列表 401', (await api('GET', `/api/app/list?teamId=${TID}`)).status === 401)

  // AP-02 校验
  check('AP-02a 空名称 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '', appType: 'agent' } })).status === 400)
  check('AP-02b 名称超 20 字 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'x'.repeat(21), appType: 'agent' } })).status === 400)
  check('AP-02c 非法应用类型 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'bad-type', appType: 'unknown' } })).status === 400)
  check('AP-02d 非法 teamId 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: 0, name: 'bad-team', appType: 'agent' } })).status === 400)

  // AP-03 权限：应用是团队下的产物，仅团队管理员可创建
  check('AP-03a Member 创建 403', (await api('POST', '/api/app', { token: member.token, body: { teamId: TID, name: 'member-app', appType: 'agent' } })).status === 403)
  check('AP-03b 非成员创建 404', (await api('POST', '/api/app', { token: outsider.token, body: { teamId: TID, name: 'outsider-app', appType: 'agent' } })).status === 404)
  check('AP-03c 非成员查列表 404', (await api('GET', `/api/app/list?teamId=${TID}`, { token: outsider.token })).status === 404)

  // AP-04 创建 Agent 应用
  const c1 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '客服助手', description: '售前售后问答', appType: 'agent' } })
  check('AP-04 Agent 应用创建 200 且返回 Guid', c1.status === 200 && isGuid(c1.json?.value), `${c1.status} ${c1.text.slice(0, 140)}`)
  const AGENT_ID = String(c1.json?.value ?? '')

  // AP-05 创建流程应用
  const c2 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '审批流', appType: 'workflow' } })
  check('AP-05 流程应用创建 200', c2.status === 200 && isGuid(c2.json?.value), `${c2.status} ${c2.text.slice(0, 140)}`)
  const WORKFLOW_ID = String(c2.json?.value ?? '')

  // AP-06 团队内名称唯一
  check('AP-06 同团队重名 409', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '客服助手', appType: 'agent' } })).status === 409)

  // AP-07 列表（Member 可见 + myRole）
  {
    const r = await api('GET', `/api/app/list?teamId=${TID}`, { token: member.token })
    const items = r.json?.items ?? []
    const agent = items.find(i => i.name === '客服助手')
    const flow = items.find(i => i.name === '审批流')
    check('AP-07a Member 查列表 200 且 myRole=0', r.status === 200 && r.json?.myRole === 0, `${r.status} ${r.text.slice(0, 120)}`)
    check('AP-07b 返回 2 条且类型正确', items.length === 2 && agent?.appType === 'agent' && flow?.appType === 'workflow', JSON.stringify(items))
    check('AP-07c 列表按创建时间倒序（流程应用在前）', items[0]?.name === '审批流', JSON.stringify(items.map(i => i.name)))
  }

  // AP-08 详情
  {
    const r = await api('GET', `/api/app/${AGENT_ID}`, { token: member.token })
    const d = r.json ?? {}
    check('AP-08a 详情 200 且字段齐全', r.status === 200 && d.name === '客服助手' && d.appType === 'agent' && Number(d.teamId) === TID && !!d.createTime && typeof d.enableForeign === 'boolean',
      `${r.status} name=${d.name} appType=${d.appType} teamId=${d.teamId}(${typeof d.teamId}) TID=${TID} createTime=${d.createTime} enableForeign=${d.enableForeign}(${typeof d.enableForeign})`)
    check('AP-08b 非成员查详情 404', (await api('GET', `/api/app/${AGENT_ID}`, { token: outsider.token })).status === 404)
    check('AP-08c 不存在的应用详情 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff', { token: owner.token })).status === 404)
  }

  // AP-09 更新基础信息
  check('AP-09a Member 更新 403', (await api('PUT', `/api/app/${AGENT_ID}`, { token: member.token, body: { name: 'member-edit' } })).status === 403)
  check('AP-09b Admin 更新 200', (await api('PUT', `/api/app/${AGENT_ID}`, { token: owner.token, body: { name: '智能客服', description: '改名后' } })).status === 200)
  {
    const r = await api('GET', `/api/app/${AGENT_ID}`, { token: owner.token })
    check('AP-09c 更新后名称/描述生效', r.json?.name === '智能客服' && r.json?.description === '改名后', r.text.slice(0, 140))
    check('AP-09d 应用类型不可修改（服务端忽略 appType）', r.json?.appType === 'agent', String(r.json?.appType))
  }
  check('AP-09e 更新撞团队内已有名称 409', (await api('PUT', `/api/app/${AGENT_ID}`, { token: owner.token, body: { name: '审批流' } })).status === 409)

  // AP-10 头像（存储全链路）
  {
    const payload = Buffer.from('app-avatar-bytes-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'appavatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    check('AP-10a 预上传 200', pre.status === 200, `${pre.status}`)
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })
    check('AP-10b 伪造 objectKey 404', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: owner.token, body: { objectKey: 'public/fake-app-key.png' } })).status === 404)
    check('AP-10c Member 设头像 403', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: member.token, body: { objectKey: comp.json.objectKey } })).status === 403)
    check('AP-10d 设置头像 200', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })).status === 200)
    const d = await api('GET', `/api/app/${AGENT_ID}`, { token: owner.token })
    check('AP-10e 详情回填头像 objectKey', d.json?.avatarPath === comp.json.objectKey, `${d.json?.avatarPath} vs ${comp.json.objectKey}`)
  }

  // AP-11 不存在的团队
  check('AP-11 不存在的团队查列表 404', (await api('GET', '/api/app/list?teamId=99999999', { token: owner.token })).status === 404)

  // AP-12 创建时可设置头像 + AP-13 允许外部使用开关
  {
    const payload = Buffer.from('app-create-avatar-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'create-avatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })

    const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '带头像应用', appType: 'agent', avatar: comp.json.objectKey, enableForeign: true } })
    check('AP-12a 创建时带头像 200', c.status === 200 && isGuid(c.json?.value), `${c.status} ${c.text.slice(0, 140)}`)
    const NEW_ID = String(c.json?.value ?? '')

    const d = await api('GET', `/api/app/${NEW_ID}`, { token: owner.token })
    check('AP-12b 详情回填创建时提交的头像', d.json?.avatarPath === comp.json.objectKey, `${d.json?.avatarPath} vs ${comp.json.objectKey}`)

    check('AP-13a 创建时 enableForeign=true 已落库', d.json?.enableForeign === true, String(d.json?.enableForeign))
    const list = await api('GET', `/api/app/list?teamId=${TID}`, { token: owner.token })
    const created = (list.json?.items ?? []).find(i => i.name === '带头像应用')
    check('AP-13b 列表返回 enableForeign', created?.enableForeign === true, JSON.stringify(created))

    check('AP-13c 更新关闭 enableForeign 200', (await api('PUT', `/api/app/${NEW_ID}`, { token: owner.token, body: { name: '带头像应用', enableForeign: false } })).status === 200)
    const d2 = await api('GET', `/api/app/${NEW_ID}`, { token: owner.token })
    check('AP-13d 关闭后详情 enableForeign=false', d2.json?.enableForeign === false, String(d2.json?.enableForeign))

    const fake = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '伪造头像应用', appType: 'agent', avatar: 'public/fake-create-avatar.png' } })
    check('AP-14 创建时伪造 objectKey 404', fake.status === 404, `${fake.status} ${fake.text.slice(0, 120)}`)
  }

  // AP-15 ~ AP-19 Agent 应用配置（允许使用的插件/知识库 + 提示词）
  {
    const cfgUrl = `/api/app/${AGENT_ID}/agent-config`

    check('AP-15a 未登录查配置 401', (await api('GET', cfgUrl)).status === 401)
    check('AP-15b 非成员查配置 404', (await api('GET', cfgUrl, { token: outsider.token })).status === 404)
    {
      const r = await api('GET', cfgUrl, { token: member.token })
      check('AP-15c Member 可读配置 200 且为新应用空配置',
        r.status === 200 && r.json?.prompt === '' && (r.json?.wikiIds ?? []).length === 0 && (r.json?.plugins ?? []).length === 0 && r.json?.myRole === 0,
        `${r.status} ${r.text.slice(0, 160)}`)
    }

    // 绑定范围：本团队知识库 + 团队可访问插件
    const wk = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: '配置用知识库' } })
    const WID = Number(wk.json?.value ?? 0)
    check('AP-16a 创建团队知识库 200', wk.status === 200 && WID > 0, `${wk.status} ${wk.text.slice(0, 120)}`)

    const pl = await api('GET', `/api/team/${TID}/plugin/list`, { token: owner.token })
    const bindable = (pl.json?.items ?? []).find(i => isGuid(i.pluginId) && i.pluginId !== '00000000-0000-0000-0000-000000000000')

    check('AP-16b Member 保存配置 403', (await api('PUT', cfgUrl, { token: member.token, body: { prompt: 'x', wikiIds: [], plugins: [] } })).status === 403)

    const plugins = bindable ? [String(bindable.pluginId)] : []
    const save = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '你是售前客服助手', wikiIds: [WID], plugins } })
    check('AP-16c Owner 保存配置 200', save.status === 200, `${save.status} ${save.text.slice(0, 140)}`)

    const back = await api('GET', cfgUrl, { token: owner.token })
    check('AP-16d 保存后回读一致（提示词 + 知识库）',
      back.json?.prompt === '你是售前客服助手' && (back.json?.wikiIds ?? []).map(Number).includes(WID),
      back.text.slice(0, 200))
    if (bindable) {
      check('AP-16e 可绑定团队可访问插件', (back.json?.plugins ?? []).map(String).includes(String(bindable.pluginId)), JSON.stringify(back.json?.plugins))
    }

    // 越权/非法绑定一律 400
    check('AP-17a 绑定非本团队知识库 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [999999999], plugins: [] } })).status === 400)
    check('AP-17b 绑定无权限插件 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: ['01924f5e-0000-7000-8000-00000000aaaa'] } })).status === 400)
    check('AP-17c 提示词超 4000 字 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'x'.repeat(4001), wikiIds: [], plugins: [] } })).status === 400)

    const after = await api('GET', cfgUrl, { token: owner.token })
    check('AP-17d 校验失败不写入配置', after.json?.prompt === '你是售前客服助手', String(after.json?.prompt))

    // 流程应用不支持该配置
    check('AP-18 流程应用保存配置 400', (await api('PUT', `/api/app/${WORKFLOW_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [] } })).status === 400)
    check('AP-19a 不存在的应用查配置 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff/agent-config', { token: owner.token })).status === 404)
    check('AP-19b 不存在的应用存配置 404', (await api('PUT', '/api/app/01924f5e-0000-7000-8000-00000000ffff/agent-config', { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [] } })).status === 404)

    // AP-20 对话模型：取值必须落在该团队可用的模型列表内（= 公开模型 + 已授权给该团队的私有模型）
    // 本地开发库不一定有公开模型，这里用 root 管理员把一个私有模型授权给本次 E2E 团队，制造可用取值；
    // 拿不到管理员账号时跳过「绑定可用模型」用例，仍校验「无权模型一律 400」这条不变量。
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken
    let usableModelId = ''
    if (adminToken) {
      const all = await api('GET', '/api/ai/model', { token: adminToken })
      const priv = (all.json?.items ?? []).find((m) => !m.isPublic)
      if (priv) {
        const cur = await api('GET', `/api/ai/model/${priv.id}/authorization`, { token: adminToken })
        const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
        const grant = await api('PUT', `/api/ai/model/${priv.id}/authorization`, { token: adminToken, body: { teamIds } })
        if (grant.status === 200) usableModelId = String(priv.id)
      }
    } else {
      console.log('SKIP | AP-20 无 admin 账号（admin/abcd123456），仅校验无权模型被拒')
    }

    const gm = await api('GET', `/api/team/${TID}/gateway/models`, { token: owner.token })
    check('AP-20a 团队可用模型列表与授权一致',
      gm.status === 200 && (usableModelId ? (gm.json?.items ?? []).map((i) => String(i.aiModelId)).includes(usableModelId) : (gm.json?.items ?? []).length === 0),
      `${gm.status} ${gm.text.slice(0, 160)}`)

    if (usableModelId) {
      const saveModel = await api('PUT', cfgUrl, { token: owner.token, body: { modelId: usableModelId, prompt: '你是售前客服助手', wikiIds: [WID], plugins } })
      check('AP-20b Owner 绑定团队可用模型 200', saveModel.status === 200, `${saveModel.status} ${saveModel.text.slice(0, 140)}`)

      const withModel = await api('GET', cfgUrl, { token: owner.token })
      check('AP-20c 回读 modelId 一致', String(withModel.json?.modelId) === usableModelId, String(withModel.json?.modelId))
    }

    check('AP-20d 绑定团队无权模型 400', (await api('PUT', cfgUrl, { token: owner.token, body: { modelId: '01924f5e-0000-7000-8000-00000000bbbb', prompt: 'x', wikiIds: [], plugins: [] } })).status === 400)
    const afterModel = await api('GET', cfgUrl, { token: owner.token })
    check('AP-20e 模型校验失败不写入',
      String(afterModel.json?.modelId) === (usableModelId || '00000000-0000-0000-0000-000000000000'),
      String(afterModel.json?.modelId))
  }

  console.log(`\n===== 应用管理 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
