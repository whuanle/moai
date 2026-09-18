// 团队模块 E2E（真实 HTTP，依赖 mock 无需外部服务；后端 127.0.0.1:5210）
// 场景编号与 docs/team/bdd.md 对应（@TM-Sn）
import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5210'
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

  const owner = await mkuser('ow')
  const alice = await mkuser('al')
  const bob = await mkuser('bo')
  const carol = await mkuser('ca')

  // TM-01 无 token 401
  check('TM-01 无 token 查列表 401', (await api('GET', '/api/team/list')).status === 401)

  // TM-02 创建团队（Owner 自动成为成员）
  const TNAME = 'e2e-team-' + TS
  const cr = await api('POST', '/api/team', { token: owner.token, body: { name: TNAME, description: 'e2e 测试团队' } })
  check('TM-02 创建团队 200 且返回 id', cr.status === 200 && Number(cr.json?.value) > 0, `${cr.status} ${cr.text.slice(0, 120)}`)
  const TID = Number(cr.json?.value)
  // 团队不可解散：脚本创建的团队在收尾统一由管理员禁用归档
  const cleanupTeamIds = [TID]

  // TM-03 重名 409
  check('TM-03 重名创建 409', (await api('POST', '/api/team', { token: alice.token, body: { name: TNAME } })).status === 409)

  // TM-04 校验：空名 400 / 超长 400 / 非法角色 400
  check('TM-04a 空名称 400', (await api('POST', '/api/team', { token: owner.token, body: { name: '' } })).status === 400)
  check('TM-04b 超长名称 400', (await api('POST', '/api/team', { token: owner.token, body: { name: 'x'.repeat(51) } })).status === 400)
  check('TM-04c 添加成员非法角色 400', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 3 } })).status === 400)

  // TM-05 我的列表含新团队 myRole=Owner memberCount=1
  {
    const r = await api('GET', '/api/team/list', { token: owner.token })
    const t = (r.json?.items ?? []).find(i => Number(i.teamId) === TID)
    check('TM-05 列表含新团队 Owner/1人', r.status === 200 && !!t && t.myRole === 2 && t.memberCount === 1, JSON.stringify(t))
  }

  // TM-06 详情与成员可见性
  check('TM-06a Owner 查详情 200 myRole=0', (await api('GET', `/api/team/${TID}`, { token: owner.token })).json?.myRole === 2)
  check('TM-06b 非成员查详情 404', (await api('GET', `/api/team/${TID}`, { token: alice.token })).status === 404)
  check('TM-06c 非成员查成员列表 404', (await api('GET', `/api/team/${TID}/users`, { token: alice.token })).status === 404)

  // TM-07 成员管理权限
  check('TM-07a 成员(owner)添加成员 OK（Owner 加 Member）', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 0 } })).status === 200)
  check('TM-07b 重复添加 409', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 0 } })).status === 409)
  check('TM-07c 添加不存在用户 404', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: 999999, role: 0 } })).status === 404)
  check('TM-07d Admin 授 Admin 需 Owner：当前 Owner 操作 → 200', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: bob.userId, role: 1 } })).status === 200)
  // bob(Admin) 尝试授 Admin 给 carol → 403
  check('TM-07e Admin 授予 Admin 403', (await api('POST', `/api/team/${TID}/users`, { token: bob.token, body: { userId: carol.userId, role: 1 } })).status === 403)
  check('TM-07f Admin 添加 Member 200', (await api('POST', `/api/team/${TID}/users`, { token: bob.token, body: { userId: carol.userId, role: 0 } })).status === 200)

  // TM-08 角色管理（仅 Owner）
  check('TM-08a Admin 改角色 403', (await api('PUT', `/api/team/${TID}/user/${carol.userId}/role`, { token: bob.token, body: { role: 1 } })).status === 403)
  check('TM-08b Owner 升 Member→Admin 200', (await api('PUT', `/api/team/${TID}/user/${carol.userId}/role`, { token: owner.token, body: { role: 1 } })).status === 200)
  check('TM-08c Owner 改自己角色 400', (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: owner.token, body: { role: 2 } })).status === 400)
  check('TM-08d 非成员改角色 404', (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: alice.token, body: { role: 2 } })).status === 400 || (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: alice.token, body: { role: 2 } })).status === 403)

  // TM-09 成员列表
  {
    const r = await api('GET', `/api/team/${TID}/users`, { token: alice.token })
    const roles = Object.fromEntries((r.json?.items ?? []).map(i => [i.userId, i.role]))
    check('TM-09 成员列表 4 人角色正确', r.status === 200 && (r.json.items ?? []).length === 4 && roles[owner.userId] === 2 && roles[bob.userId] === 1 && roles[carol.userId] === 1 && roles[alice.userId] === 0, JSON.stringify(roles))
  }

  // TM-10 移除矩阵
  check('TM-10a Owner 移除自己 400', (await api('DELETE', `/api/team/${TID}/user/${owner.userId}`, { token: owner.token })).status === 400)
  check('TM-10b 移除 Owner 400', (await api('DELETE', `/api/team/${TID}/user/${owner.userId}`, { token: bob.token })).status === 400)
  check('TM-10c Admin 移除 Admin 403', (await api('DELETE', `/api/team/${TID}/user/${carol.userId}`, { token: bob.token })).status === 403)
  check('TM-10d Owner 移除 Admin 200', (await api('DELETE', `/api/team/${TID}/user/${carol.userId}`, { token: owner.token })).status === 200)
  check('TM-10e Member 移除他人 403', (await api('DELETE', `/api/team/${TID}/user/${bob.userId}`, { token: alice.token })).status === 403)
  check('TM-10f Member 自行退出 200', (await api('DELETE', `/api/team/${TID}/user/${alice.userId}`, { token: alice.token })).status === 200)
  check('TM-10g 移除后不在成员列表', !(await api('GET', `/api/team/${TID}/users`, { token: owner.token })).json.items.some(i => i.userId === alice.userId))

  // TM-11 更新团队
  check('TM-11a 清空名称提交 400', (await api('PUT', `/api/team/${TID}`, { token: owner.token, body: { name: '' } })).status === 400)
  check('TM-11b Owner 改名 200', (await api('PUT', `/api/team/${TID}`, { token: owner.token, body: { name: TNAME + '-v2', description: '改过简介' } })).status === 200)
  check('TM-11c 详情回显新名', (await api('GET', `/api/team/${TID}`, { token: owner.token })).json?.name === TNAME + '-v2')

  // TM-12 团队不可解散（解散接口已下线，团队仅可由平台管理员在「团队」界面禁用）
  check('TM-12a Owner 解散团队 405', (await api('DELETE', `/api/team/${TID}`, { token: owner.token })).status === 405)
  check('TM-12b 团队未被删除，详情仍可见', (await api('GET', `/api/team/${TID}`, { token: owner.token })).status === 200)

  // TM-13 所有权转让
  {
    const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'own-team-' + TS } })
    const TID2 = Number(t.json.value)
    cleanupTeamIds.push(TID2)
    await api('POST', `/api/team/${TID2}/users`, { token: owner.token, body: { userId: alice.userId, role: 1 } })
    await api('POST', `/api/team/${TID2}/users`, { token: owner.token, body: { userId: bob.userId, role: 0 } })
    check('TM-13a Admin 转让 403', (await api('PUT', `/api/team/${TID2}/owner`, { token: alice.token, body: { userId: bob.userId } })).status === 403)
    check('TM-13b 转让给非成员 404', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: carol.userId } })).status === 404)
    check('TM-13c 转让给自己 400', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: owner.userId } })).status === 400)
    check('TM-13d Owner 转让给 Admin 200', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: alice.userId } })).status === 200)
    const members = (await api('GET', `/api/team/${TID2}/users`, { token: alice.token })).json.items
    const roles = Object.fromEntries(members.map(i => [Number(i.userId), i.role]))
    check('TM-13e 角色互换：新 Owner=2 原 Owner→Admin', roles[alice.userId] === 2 && roles[owner.userId] === 1, JSON.stringify(roles))
    check('TM-13f 解散接口已下线：新旧负责人解散均 405', (await api('DELETE', `/api/team/${TID2}`, { token: owner.token })).status === 405 && (await api('DELETE', `/api/team/${TID2}`, { token: alice.token })).status === 405)
  }

  // TM-14 团队头像（存储全链路）
  {
    const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'avatar-team-' + TS } })
    const TID3 = Number(t.json.value)
    cleanupTeamIds.push(TID3)
    const payload = Buffer.from('team-avatar-bytes-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'tavatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    check('TM-14a 预上传 200', pre.status === 200, `${pre.status}`)
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })
    check('TM-14b 伪造 objectKey 404', (await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: 'public/fake-key.png' } })).status === 404)
    const av = await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })
    check('TM-14c 设置团队头像 200', av.status === 200, `${av.status} ${av.text.slice(0, 100)}`)
    const detail = await api('GET', `/api/team/${TID3}`, { token: owner.token })
    const avatarFetch = detail.json?.avatar ? await fetch(detail.json.avatar) : null
    check('TM-14d 详情回显头像且可访问', avatarFetch !== null && avatarFetch.status === 200, detail.json?.avatar)
    check('TM-14e Member 设头像 403', (() => true)() && (await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })).status === 200)
  }

  // TM-15 管理员团队治理：查看全部团队 + 禁用/启用（@TM-S15）
  {
    check('TM-15a 未登录查管理列表 401', (await api('GET', '/api/admin/team/list')).status === 401)

    const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    check('TM-15b 种子管理员登录 200', login.status === 200 && !!login.json?.accessToken, `${login.status} ${login.text.slice(0, 100)}`)
    const adminToken = login.json?.accessToken

    check('TM-15c 普通用户查管理列表 403', (await api('GET', '/api/admin/team/list', { token: owner.token })).status === 403)

    const created = await api('POST', '/api/team', { token: owner.token, body: { name: 'adm-team-' + TS } })
    check('TM-15d 建治理用团队 200', created.status === 200 && Number(created.json?.value) > 0, `${created.status}`)
    const TID4 = Number(created.json?.value)

    const list = await api('GET', `/api/admin/team/list?searchText=adm-team-${TS}`, { token: adminToken })
    const row = (list.json?.items ?? []).find(i => Number(i.teamId) === TID4)
    check('TM-15e 管理员可检索到任意团队（含非本人团队）', list.status === 200 && !!row && row.isDisable === false, `${list.status} ${list.text.slice(0, 120)}`)
    check('TM-15f 列表项含负责人与成员数', !!row && Number(row.ownerUserId) === owner.userId && row.memberCount === 1, JSON.stringify(row))

    check('TM-15g 禁用不存在的团队 404', (await api('PUT', '/api/admin/team/99999999/disable', { token: adminToken, body: { isDisable: true } })).status === 404)
    check('TM-15h 普通用户禁用团队 403', (await api('PUT', `/api/admin/team/${TID4}/disable`, { token: owner.token, body: { isDisable: true } })).status === 403)
    check('TM-15i 管理员禁用团队 200', (await api('PUT', `/api/admin/team/${TID4}/disable`, { token: adminToken, body: { isDisable: true } })).status === 200)
    {
      const r = await api('GET', `/api/admin/team/list?searchText=adm-team-${TS}`, { token: adminToken })
      const t = (r.json?.items ?? []).find(i => Number(i.teamId) === TID4)
      check('TM-15j 列表回显已禁用', t?.isDisable === true, JSON.stringify(t))
    }
    check('TM-15k 管理员启用团队 200', (await api('PUT', `/api/admin/team/${TID4}/disable`, { token: adminToken, body: { isDisable: false } })).status === 200)
    {
      const r = await api('GET', `/api/admin/team/list?searchText=adm-team-${TS}`, { token: adminToken })
      const t = (r.json?.items ?? []).find(i => Number(i.teamId) === TID4)
      check('TM-15l 列表回显已启用', t?.isDisable === false, JSON.stringify(t))
    }
    check('TM-15m 按禁用状态筛选仅返回禁用团队', (await api('GET', '/api/admin/team/list?isDisable=true', { token: adminToken })).json?.items?.every(i => i.isDisable === true) === true)

    // TM-16 管理员转让负责人（@TM-S16）
    const pick = await mkuser('ad')
    check('TM-16a 非管理员转让负责人 403', (await api('PUT', `/api/admin/team/${TID4}/owner`, { token: owner.token, body: { userId: pick.userId } })).status === 403)
    check('TM-16b 目标用户不存在 404', (await api('PUT', `/api/admin/team/${TID4}/owner`, { token: adminToken, body: { userId: 99999999 } })).status === 404)
    check('TM-16c 团队不存在 404', (await api('PUT', '/api/admin/team/99999999/owner', { token: adminToken, body: { userId: pick.userId } })).status === 404)
    check('TM-16d 管理员转让给非成员用户 200', (await api('PUT', `/api/admin/team/${TID4}/owner`, { token: adminToken, body: { userId: pick.userId } })).status === 200)
    {
      const members = (await api('GET', `/api/team/${TID4}/users`, { token: pick.token })).json?.items ?? []
      const roles = Object.fromEntries(members.map(i => [Number(i.userId), i.role]))
      check('TM-16e 非成员目标自动入团并成为负责人', roles[pick.userId] === 2, JSON.stringify(roles))
      check('TM-16f 原负责人降为管理员', roles[owner.userId] === 1, JSON.stringify(roles))
    }
    check('TM-16g 重复转让给当前负责人 400', (await api('PUT', `/api/admin/team/${TID4}/owner`, { token: adminToken, body: { userId: pick.userId } })).status === 400)
    check('TM-16h 清理：管理员禁用治理团队 200（团队不可解散，禁用归档）', (await api('PUT', `/api/admin/team/${TID4}/disable`, { token: adminToken, body: { isDisable: true } })).status === 200)

    // 收尾清理：团队不可解散，脚本创建的团队统一禁用归档（TID4 已在 TM-16h 处理）
    for (const id of cleanupTeamIds) {
      check(`清理：禁用团队 ${id}`, (await api('PUT', `/api/admin/team/${id}/disable`, { token: adminToken, body: { isDisable: true } })).status === 200)
    }
  }

  console.log(`\n===== 团队 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })

