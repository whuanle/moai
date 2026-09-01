// 终审 B：storage 全链路实测（预上传 → PUT 直传 MinIO → 完成 → /static 公开访问）
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
const crypto = await import('node:crypto')
let pass = 0, fail = 0
const check = (n, c, d = '') => { console.log(`${c ? 'PASS' : 'FAIL'} ${n} ${c ? '' : d}`); c ? pass++ : fail++ }

const { rsaPublic, publicStoreUrl } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())
const enc = (t) => crypto.publicEncrypt(
  { key: crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' }), padding: crypto.constants.RSA_PKCS1_PADDING },
  Buffer.from(t)).toString('base64')

const login = await fetch(`${BASE}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: 'admin', password: enc('abcd123456') }) }).then((r) => r.json())
const auth = { 'Content-Type': 'application/json', Authorization: `Bearer ${login.accessToken}` }

// 1. 预上传（内容随机，避免命中秒传分支）
const rnd = crypto.randomBytes(16).toString('hex')
const png = Buffer.from('89504e470d0a1a0a0000000d4948445200000001000000010806000000' +
  '1f15c4890000000a49444154789c6360000002000100' + '0521f5b4' + rnd + '0000000049454e44ae426082', 'hex')
const sha256 = crypto.createHash('sha256').update(png).digest('hex')
const ts = Date.now()
const pre = await fetch(`${BASE}/api/storage/public/pre_upload_image`, {
  method: 'POST', headers: auth,
  body: JSON.stringify({ fileName: `audit${ts}.png`, contentType: 'image/png', fileSize: png.length, sha256 }),
}).then((r) => r.json())
check('预上传返回 fileId/objectKey/uploadUrl', pre?.fileId > 0 && /public\/images\//.test(pre?.objectKey ?? '') && Boolean(pre?.uploadUrl))

// 2. PUT 直传预签名地址（不带 Authorization）
const put = await fetch(pre.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: png })
check('PUT 预签名地址直传 MinIO', put.status === 200)

// 3. 完成上传
const complete = await fetch(`${BASE}/api/storage/complate_url`, {
  method: 'POST', headers: auth, body: JSON.stringify({ fileId: pre.fileId, isSuccess: true }),
}).then((r) => r.json())
check('完成上传返回 objectKey/accessUrl', complete?.objectKey === pre.objectKey && Boolean(complete?.accessUrl))

// 4. 公开静态访问（匿名）
const stat = await fetch(complete.accessUrl)
const body = Buffer.from(await stat.arrayBuffer())
check('匿名 GET accessUrl 字节一致', stat.status === 200 && body.equals(png), `status=${stat.status} bytes=${body.length}`)

// 5. 秒传
const pre2 = await fetch(`${BASE}/api/storage/public/pre_upload_image`, {
  method: 'POST', headers: auth,
  body: JSON.stringify({ fileName: `audit${ts}.png`, contentType: 'image/png', fileSize: png.length, sha256 }),
}).then((r) => r.json())
check('重复预上传 isExist=true 复用 fileId', pre2?.isExist === true && pre2?.fileId === pre.fileId)

// 6. 校验失败
const bad = await fetch(`${BASE}/api/storage/public/pre_upload_image`, {
  method: 'POST', headers: auth,
  body: JSON.stringify({ fileName: 'x.png', contentType: 'image/png', fileSize: 0, sha256: '' }),
})
check('fileSize=0 校验 400', bad.status === 400)

// 7. 私有前缀不可经 /static 访问
const priv = await fetch(`${publicStoreUrl.replace(/\/static$/, '')}/static/temp/nonexist`)
check('私有/不存在对象 /static 404', priv.status === 404)

console.log(`\n结果: ${pass} 通过, ${fail} 失败`)
process.exit(fail > 0 ? 1 : 0)
