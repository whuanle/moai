// 终审抽检：轮3 account / 轮4 settings / 轮5 oauthconnect 关键场景实测
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
const crypto = await import('node:crypto')
let pass = 0, fail = 0
const check = (n, c, d = '') => { console.log(`${c ? 'PASS' : 'FAIL'} ${n} ${c ? '' : d}`); c ? pass++ : fail++ }

const { rsaPublic } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())
const enc = (t) => crypto.publicEncrypt(
  { key: crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' }), padding: crypto.constants.RSA_PKCS1_PADDING },
  Buffer.from(t)).toString('base64')

async function login(u, p) {
  const r = await fetch(`${BASE}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: u, password: enc(p) }) })
  return (await r.json())?.accessToken ?? null
}
async function api(t, path, method = 'GET', payload) {
  const r = await fetch(`${BASE}${path}`, { method, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${t}` }, body: payload === undefined ? undefined : JSON.stringify(payload) })
  return { status: r.status, body: await r.json().catch(() => null) }
}

const root = await login('admin', 'abcd123456')
check('root 登录', Boolean(root))

// ===== 轮3 account =====
const info = await api(root, '/api/account/userinfo')
check('3-userinfo 200 含身份字段', info.status === 200 && 'isAdmin' in (info.body ?? {}) && 'avatar' in (info.body ?? {}))
const ts = Date.now().toString().slice(-6)
const upd = await api(root, '/api/account/update_userinfo', 'POST', { nickName: '审计昵称' + ts, phone: '139' + ts + '00' })
const info2 = await api(root, '/api/account/userinfo')
check('3-update_userinfo 生效(缓存已失效)', upd.status === 200 && info2.body?.nickName === '审计昵称' + ts)
await api(root, '/api/account/update_userinfo', 'POST', { nickName: 'admin', phone: '12345678901' })
const unbind404 = await api(root, '/api/account/unbind_account', 'POST', { providerId: '00000000-0000-0000-0000-000000000000' })
check('3-unbind 未绑定 404', unbind404.status === 404)
const bound = await api(root, '/api/account/bound_accounts')
check('3-bound_accounts 200', bound.status === 200 && Array.isArray(bound.body?.items))

// ===== 轮4 settings =====
const st = await api(root, '/api/settings')
check('4-settings GET 200 含 oauth_auto_register', st.status === 200 && (st.body?.items ?? []).some((i) => i.key === 'oauth_auto_register'))
const save = await api(root, '/api/settings', 'PUT', { key: 'oauth_auto_register', value: 'true' })
const st2 = await api(root, '/api/settings')
const nowVal = st2.body?.items?.find((i) => i.key === 'oauth_auto_register')?.value
check('4-settings PUT 生效', save.status === 200 && nowVal === 'true')
await api(root, '/api/settings', 'PUT', { key: 'oauth_auto_register', value: 'false' })
const badKey = await api(root, '/api/settings', 'PUT', { key: 'not_exist_key', value: 'x' })
check('4-非法 key 400', badKey.status === 400)

// ===== 轮5 oauthconnect =====
const list = await api(root, '/api/oauthconnect/connections')
check('5-connections GET 200', list.status === 200 && Array.isArray(list.body?.items))
const ts5 = Date.now().toString().slice(-6)
const create = await api(root, '/api/oauthconnect/connections', 'POST', { name: 'audit' + ts5, provider: 'feishu', key: 'k' + ts5, secret: 's' + ts5, iconUrl: 'https://example.com/i.png' })
check('5-create feishu 200', create.status === 200)
const list2 = await api(root, '/api/oauthconnect/connections')
const item = list2.body?.items?.find((i) => i.name === 'audit' + ts5)
check('5-create 后列表可见且 authorizeUrl=飞书固定地址', Boolean(item) && /accounts\.feishu\.cn/.test(item?.authorizeUrl ?? ''))
const dup = await api(root, '/api/oauthconnect/connections', 'POST', { name: 'audit' + ts5, provider: 'feishu', key: 'k', secret: 's', iconUrl: 'https://x/i.png' })
check('5-重名创建被拒', dup.status === 400 || dup.status === 409)
const put = await api(root, `/api/oauthconnect/connections/${item.id}`, 'PUT', { name: 'audit' + ts5, provider: 'feishu', key: 'k2', iconUrl: 'https://x/i.png' })
check('5-PUT 200（缺陷已修复，2026-09-02 前 400）', put.status === 200)
const del = await api(root, `/api/oauthconnect/connections/${item.id}`, 'DELETE')
const list3 = await api(root, '/api/oauthconnect/connections')
check('5-DELETE 200 且软删除生效', del.status === 200 && !(list3.body?.items ?? []).some((i) => i.id === item.id))

console.log(`\n结果: ${pass} 通过, ${fail} 失败`)
process.exit(fail > 0 ? 1 : 0)
