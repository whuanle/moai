// 本批功能 E2E：应用分类绑定 + 应用市场搜索/分类（AP-CLS）、技能头像（SK-AV）、技能压缩包上传解压（SK-ZIP）、
// 知识库头像（WK-AV）、静态插件保存后列表不重复（PG-DUP）
// 用法：node local-dev/feature-batch-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5210）
// 前置：后端运行中，MinIO/Redis/RabbitMQ 可用，admin 账号可登录（admin/abcd123456）。
import crypto from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5210'

let passed = 0
let failed = 0
const check = (name, cond, extra = '') => {
  if (cond) { passed++; console.log(`  ✓ ${name}`) }
  else { failed++; console.error(`  ✗ ${name} ${extra}`) }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
async function api(method, path, { token, body, raw } = {}) {
  const headers = {}
  if (body !== undefined && !raw) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: raw ?? (body === undefined ? undefined : JSON.stringify(body)) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex')

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

// 1x1 透明 PNG
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64')

/** 走公开图片直传管线（pre_upload_image → PUT → complate_url），返回 objectKey */
async function uploadImage(token, name = 'a.png') {
  const hash = sha256(PNG)
  const pre = await api('POST', '/api/storage/public/pre_upload_image', { token, body: { fileName: name, contentType: 'image/png', fileSize: PNG.length, shA256: hash } })
  if (pre.status !== 200) throw new Error(`图片预上传失败: ${pre.status} ${pre.text.slice(0, 120)}`)
  if (!pre.json.isExist && pre.json.uploadUrl) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: PNG })
    if (put.status !== 200) throw new Error(`图片直传失败: ${put.status}`)
  }
  await api('POST', '/api/storage/complate_url', { token, body: { fileId: String(pre.json.fileId), isSuccess: true } })
  return pre.json.objectKey
}

/** 构造技能压缩包：entries 为 [zipPath, content] */
function buildZip(entries) {
  const dir = mkdtempSync(join(tmpdir(), 'skpkg-'))
  try {
    for (const [p, content] of entries) {
      const full = join(dir, p)
      mkdirSync(dirname(full), { recursive: true })
      writeFileSync(full, content)
    }
    const zipPath = join(dir, 'pack.zip')
    execFileSync('zip', ['-q', '-r', zipPath, ...new Set(entries.map(([p]) => p.split('/')[0]))], { cwd: dir })
    return { buf: readFileSyncSafe(zipPath), zipPath }
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}
import { readFileSync as readFileSyncSafe, mkdirSync } from 'node:fs'
import { crc32 as zlibCrc32 } from 'node:zlib'

/** 手工构造 STORE 型 zip（entry 名可含 ../ 等任意字面路径，系统 zip CLI 会拒绝这类路径） */
function buildRawZip(entries) {
  const enc = new TextEncoder()
  const chunks = []
  const central = []
  let offset = 0
  const dosTime = 0
  const dosDate = 0x21 // 1980-01-01
  for (const [name, content] of entries) {
    const nameBuf = enc.encode(name)
    const data = Buffer.from(content, 'utf8')
    const crc = zlibCrc32(data) >>> 0
    const local = Buffer.alloc(30)
    local.writeUInt32LE(0x04034b50, 0)
    local.writeUInt16LE(20, 4)
    local.writeUInt16LE(0, 6)
    local.writeUInt16LE(0, 8)
    local.writeUInt16LE(dosTime, 10)
    local.writeUInt16LE(dosDate, 12)
    local.writeUInt32LE(crc, 14)
    local.writeUInt32LE(data.length, 18)
    local.writeUInt32LE(data.length, 22)
    local.writeUInt16LE(nameBuf.length, 26)
    local.writeUInt16LE(0, 28)
    chunks.push(local, nameBuf, data)

    const cd = Buffer.alloc(46)
    cd.writeUInt32LE(0x02014b50, 0)
    cd.writeUInt16LE(20, 4)
    cd.writeUInt16LE(20, 6)
    cd.writeUInt16LE(0, 8)
    cd.writeUInt16LE(0, 10)
    cd.writeUInt16LE(dosTime, 12)
    cd.writeUInt16LE(dosDate, 14)
    cd.writeUInt32LE(crc, 16)
    cd.writeUInt32LE(data.length, 20)
    cd.writeUInt32LE(data.length, 24)
    cd.writeUInt16LE(nameBuf.length, 28)
    cd.writeUInt16LE(0, 30)
    cd.writeUInt16LE(0, 32)
    cd.writeUInt16LE(0, 34)
    cd.writeUInt16LE(0, 36)
    cd.writeUInt32LE(0, 38)
    cd.writeUInt32LE(offset, 42)
    central.push(cd, nameBuf)

    offset += local.length + nameBuf.length + data.length
  }
  const centralBuf = Buffer.concat(central)
  const eocd = Buffer.alloc(22)
  eocd.writeUInt32LE(0x06054b50, 0)
  eocd.writeUInt16LE(entries.length, 8)
  eocd.writeUInt16LE(entries.length, 10)
  eocd.writeUInt32LE(centralBuf.length, 12)
  eocd.writeUInt32LE(offset, 16)
  return Buffer.concat([...chunks, centralBuf, eocd])
}

/** 技能文件三段上传（preupload → PUT → complete），返回 fileId */
async function uploadSkillFile(token, name, buf, contentType) {
  const hash = sha256(buf)
  const pre = await api('POST', '/api/skill/file/preupload', { token, body: { fileName: name, contentType, fileSize: buf.length, shA256: hash } })
  if (pre.status !== 200) throw new Error(`技能文件预上传失败: ${pre.status} ${pre.text.slice(0, 120)}`)
  if (!pre.json.isExist && pre.json.uploadUrl) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: buf })
    if (put.status !== 200) throw new Error(`技能文件直传失败: ${put.status}`)
  }
  const done = await api('POST', '/api/skill/file/complete', { token, body: { fileId: String(pre.json.fileId), isSuccess: true } })
  if (done.status !== 200) throw new Error(`技能文件完成失败: ${done.status}`)
  return Number(pre.json.fileId)
}

async function main() {
  console.log(`BASE = ${BASE}`)
  const server = await api('GET', '/api/common/serverinfo')
  RSA_KEY = server.json?.rsaPublic
  if (!RSA_KEY) throw new Error('获取 rsaPublic 失败')

  // ============ 管理员：建分类（app 类型，带 emoji） ============
  const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const admin = adminLogin.json?.accessToken
  if (!admin) throw new Error('admin 登录失败（admin/abcd123456）')

  const cls1 = await api('POST', '/api/classify', { token: admin, body: { type: 'app', name: `效率-${TS}`, emoji: '🚀', description: '效率工具' } })
  check('AP-CLS-01 管理员创建应用分类 200', cls1.status === 200 && cls1.json?.value > 0, `${cls1.status} ${cls1.text.slice(0, 120)}`)
  const CLS1 = cls1.json?.value
  const cls2 = await api('POST', '/api/classify', { token: admin, body: { type: 'app', name: `办公-${TS}`, emoji: '📄' } })
  const CLS2 = cls2.json?.value
  check('AP-CLS-02 第二个应用分类 200', cls2.status === 200 && CLS2 > 0)

  const owner = await mkuser('own')

  // 建团队
  const team = await api('POST', '/api/team', { token: owner.token, body: { name: `e2e团队-${TS}` } })
  check('AP-CLS-03 创建团队 200', team.status === 200, `${team.status} ${team.text.slice(0, 120)}`)
  const TID = team.json?.value

  // ============ 应用绑定分类 ============
  const app1 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: `市场应用A${TS}`, appType: 'agent', description: '效率工具类应用', classifyId: CLS1 } })
  check('AP-CLS-04 创建应用绑定分类 200', app1.status === 200 && !!app1.json?.value, `${app1.status} ${app1.text.slice(0, 140)}`)
  const APP1 = app1.json?.value

  const d1 = await api('GET', `/api/app/${APP1}`, { token: owner.token })
  check('AP-CLS-05 详情返回 classifyId', d1.status === 200 && d1.json?.classifyId === CLS1, JSON.stringify(d1.json?.classifyId))

  const list1 = await api('GET', `/api/app/list?teamId=${TID}`, { token: owner.token })
  check('AP-CLS-06 团队列表返回 classifyId', list1.status === 200 && list1.json?.items?.some((x) => x.appId === APP1 && x.classifyId === CLS1))

  const upd1 = await api('PUT', `/api/app/${APP1}`, { token: owner.token, body: { name: `市场应用A${TS}`, description: '改到办公分类', isExternal: false, isAuth: false, classifyId: CLS2 } })
  check('AP-CLS-07 更新应用换绑分类 200', upd1.status === 200, `${upd1.status} ${upd1.text.slice(0, 120)}`)
  const d2 = await api('GET', `/api/app/${APP1}`, { token: owner.token })
  check('AP-CLS-08 详情反映新分类', d2.json?.classifyId === CLS2)

  const badCls = await api('PUT', `/api/app/${APP1}`, { token: owner.token, body: { name: `市场应用A${TS}`, isExternal: false, isAuth: false, classifyId: 99999999 } })
  check('AP-CLS-09 非法分类 404', badCls.status === 404, `${badCls.status}`)

  // 公开+发布，验证市场列表
  const apply = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: APP1 } })
  check('AP-CLS-10 申请上架 200', apply.status === 200, `${apply.status} ${apply.text.slice(0, 120)}`)
  const review = await api('POST', '/api/publication/review', { token: admin, body: { publicationId: apply.json?.value, isApprove: true, reviewComment: 'ok' } })
  check('AP-CLS-11 审批通过 200', review.status === 200, `${review.status} ${review.text.slice(0, 120)}`)
  const pub = await api('POST', `/api/app/${APP1}/publish`, { token: owner.token })
  check('AP-CLS-12 发布应用 200', pub.status === 200, `${pub.status} ${pub.text.slice(0, 120)}`)

  const pubAll = await api('GET', '/api/app/public/list', { token: owner.token })
  check('AP-CLS-13 市场全量包含应用并带 classifyId', pubAll.status === 200 && pubAll.json?.items?.some((x) => x.appId === APP1 && x.classifyId === CLS2))

  const pubKw = await api('GET', `/api/app/public/list?Keywords=${encodeURIComponent('市场应用A' + TS)}`, { token: owner.token })
  check('AP-CLS-14 市场关键字搜索命中', pubKw.status === 200 && pubKw.json?.items?.some((x) => x.appId === APP1))

  const pubKwMiss = await api('GET', '/api/app/public/list?Keywords=绝不存在的应用名xyz', { token: owner.token })
  check('AP-CLS-15 市场关键字未命中为空', pubKwMiss.status === 200 && (pubKwMiss.json?.items?.length ?? 0) === 0)

  const pubCls = await api('GET', `/api/app/public/list?ClassifyId=${CLS2}`, { token: owner.token })
  check('AP-CLS-16 市场按分类过滤命中', pubCls.status === 200 && pubCls.json?.items?.some((x) => x.appId === APP1))
  const pubClsMiss = await api('GET', `/api/app/public/list?ClassifyId=${CLS1}`, { token: owner.token })
  check('AP-CLS-17 市场按旧分类过滤不含应用', pubClsMiss.status === 200 && !pubClsMiss.json?.items?.some((x) => x.appId === APP1))

  // ============ 知识库头像 ============
  const wiki = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: `头像库${TS}`, description: '测头像' } })
  check('WK-AV-01 创建知识库 200', wiki.status === 200 && Number(wiki.json?.value) > 0, `${wiki.status} ${wiki.text.slice(0, 120)}`)
  const WID = Number(wiki.json?.value)

  const wikiAvatarKey = await uploadImage(owner.token, 'wiki-avatar.png')
  const setAvatar = await api('POST', `/api/wiki/${WID}/avatar`, { token: owner.token, body: { objectKey: wikiAvatarKey } })
  check('WK-AV-02 设置知识库头像 200', setAvatar.status === 200, `${setAvatar.status} ${setAvatar.text.slice(0, 120)}`)
  const wlist = await api('GET', `/api/wiki/list?teamId=${TID}`, { token: owner.token })
  const witem = wlist.json?.items?.find((x) => Number(x.wikiId) === WID)
  check('WK-AV-03 列表返回 avatarPath', witem?.avatarPath === wikiAvatarKey, JSON.stringify(witem?.avatarPath))

  // ============ 技能压缩包上传 + 自动解压 ============
  const SKILL_MD = `---\nname: 压缩包技能${TS}\ndescription: 由 zip 解压创建的技能\n---\n\n# 使用说明\n\n1. 调用 run.py 生成文档\n2. 结果保存到输出目录\n`
  const { buf: zipBuf } = buildZip([
    ['mypack/SKILL.md', SKILL_MD],
    ['mypack/scripts/run.py', 'print("hello skill")\n'],
    ['mypack/docs/usage.md', '# usage\n'],
  ])
  const zipFileId = await uploadSkillFile(owner.token, 'skill-pack.zip', zipBuf, 'application/zip')

  const ext = await api('POST', '/api/skill/file/extract', { token: owner.token, body: { fileId: zipFileId } })
  check('SK-ZIP-01 解压技能包 200', ext.status === 200, `${ext.status} ${ext.text.slice(0, 160)}`)
  check('SK-ZIP-02 解析 SKILL.md 名称', ext.json?.name === `压缩包技能${TS}`, JSON.stringify(ext.json?.name))
  check('SK-ZIP-03 解析 SKILL.md 描述', ext.json?.description === '由 zip 解压创建的技能')
  check('SK-ZIP-04 正文作为使用说明', String(ext.json?.instructions ?? '').includes('使用说明'))
  const extFiles = ext.json?.files ?? []
  check('SK-ZIP-05 展开为 3 个文件', extFiles.length === 3, JSON.stringify(extFiles.map((f) => f.path)))
  check('SK-ZIP-06 剥离顶层目录', extFiles.some((f) => f.path === 'SKILL.md') && extFiles.some((f) => f.path === 'scripts/run.py'))

  // 用解压结果 + 头像创建技能
  const avatarKey1 = await uploadImage(owner.token, 'skill-avatar1.png')
  const create = await api('POST', '/api/skill', { token: owner.token, body: { teamId: 0, key: `zip_${TS.toLowerCase()}`, name: ext.json?.name, description: ext.json?.description, instructions: ext.json?.instructions, files: extFiles, classifyId: 0, avatar: avatarKey1 } })
  check('SK-AV-01 创建技能携带头像 200', create.status === 200 && !!create.json?.value, `${create.status} ${create.text.slice(0, 160)}`)
  const SKILL = create.json?.value

  const my = await api('GET', '/api/skill/my_list', { token: owner.token })
  const myItem = my.json?.items?.find((x) => x.id === SKILL)
  check('SK-AV-02 我的技能列表带 avatarPath', myItem?.avatarPath === avatarKey1, JSON.stringify(myItem?.avatarPath))

  // 更换头像端点
  const avatarKey2 = await uploadImage(owner.token, 'skill-avatar2.png')
  const setSkAvatar = await api('POST', `/api/skill/${SKILL}/avatar`, { token: owner.token, body: { objectKey: avatarKey2 } })
  check('SK-AV-03 设置技能头像端点 200', setSkAvatar.status === 200, `${setSkAvatar.status} ${setSkAvatar.text.slice(0, 120)}`)
  const sdet = await api('GET', `/api/skill/${SKILL}`, { token: owner.token })
  check('SK-AV-04 详情反映新头像', sdet.json?.avatarPath === avatarKey2, JSON.stringify(sdet.json?.avatarPath))
  check('SK-ZIP-07 技能包文件入库', (sdet.json?.files ?? []).length === 3)

  const badAvatar = await api('POST', `/api/skill/${SKILL}/avatar`, { token: owner.token, body: { objectKey: 'skill/nonexistent.png' } })
  check('SK-AV-05 伪造 objectKey 404', badAvatar.status === 404, `${badAvatar.status}`)

  // 负例：路径穿越
  const evilBuf = buildRawZip([['evil/../../x.py', '#!/bin/sh\n']])
  const evilId = await uploadSkillFile(owner.token, 'evil.zip', evilBuf, 'application/zip')
  const evilExt = await api('POST', '/api/skill/file/extract', { token: owner.token, body: { fileId: evilId } })
  check('SK-ZIP-08 路径穿越被拒 400', evilExt.status === 400, `${evilExt.status} ${evilExt.text.slice(0, 120)}`)

  // 负例：非 zip 文件解压
  const pyId = await uploadSkillFile(owner.token, 'plain.py', Buffer.from('print(1)\n'), 'text/x-python')
  const noZip = await api('POST', '/api/skill/file/extract', { token: owner.token, body: { fileId: pyId } })
  check('SK-ZIP-09 非 zip 文件解压 400', noZip.status === 400, `${noZip.status}`)
  const noFile = await api('POST', '/api/skill/file/extract', { token: owner.token, body: { fileId: 99999999 } })
  check('SK-ZIP-10 不存在文件 404', noFile.status === 404, `${noFile.status}`)

  // ============ 静态插件：保存后列表无重复 ============
  const staticBefore = await api('GET', '/api/ai/plugin/manage/list?kind=static', { token: admin })
  check('PG-DUP-01 静态插件列表 200', staticBefore.status === 200)
  const beforeItems = staticBefore.json?.items ?? []
  const memoryOnly = beforeItems.find((x) => x.id === '00000000-0000-0000-0000-000000000000' && x.pluginKey)
  const dupBefore = beforeItems.filter((x) => x.pluginKey && new Set(beforeItems.filter((y) => y.pluginKey === x.pluginKey)).size > 1)
  check('PG-DUP-02 初始列表无重复 pluginKey', dupBefore.length === 0, JSON.stringify(dupBefore.map((x) => x.pluginKey)))

  if (memoryOnly) {
    const save = await api('POST', '/api/ai/plugin/static/save', { token: admin, body: { pluginKey: memoryOnly.pluginKey, title: memoryOnly.title ?? memoryOnly.pluginName, description: 'e2e 修改描述', classifyId: 0 } })
    check('PG-DUP-03 保存内存静态插件 200', save.status === 200, `${save.status} ${save.text.slice(0, 120)}`)
    const staticAfter = await api('GET', '/api/ai/plugin/manage/list?kind=static', { token: admin })
    const afterItems = staticAfter.json?.items ?? []
    const dupAfter = afterItems.filter((x) => afterItems.filter((y) => y.pluginKey === x.pluginKey).length > 1)
    check('PG-DUP-04 保存后列表仍无重复', dupAfter.length === 0, JSON.stringify(dupAfter.map((x) => x.pluginKey)))
    check('PG-DUP-05 保存后插件仍在列表', afterItems.some((x) => x.pluginKey === memoryOnly.pluginKey))
  } else {
    // 库里已有全部静态插件记录时，取任一 DB 行做保存回写验证
    const anyStatic = beforeItems[0]
    const save = await api('POST', '/api/ai/plugin/static/save', { token: admin, body: { pluginKey: anyStatic.pluginKey ?? anyStatic.pluginName, title: anyStatic.title ?? anyStatic.pluginName, description: 'e2e 回写', classifyId: 0 } })
    check('PG-DUP-03b 回写静态插件 200', save.status === 200, `${save.status} ${save.text.slice(0, 120)}`)
    const staticAfter = await api('GET', '/api/ai/plugin/manage/list?kind=static', { token: admin })
    const afterItems = staticAfter.json?.items ?? []
    const key = anyStatic.pluginKey ?? anyStatic.pluginName
    check('PG-DUP-04b 回写后无重复', afterItems.filter((x) => (x.pluginKey ?? x.pluginName) === key).length === 1)
  }

  console.log(`\n结果: ${passed} 通过, ${failed} 失败`)
  process.exit(failed > 0 ? 1 : 0)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
