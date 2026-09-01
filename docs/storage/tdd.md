# 文件存储（Storage，STO）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @STO-S1 | [local-dev/audit-storage.mjs](../../local-dev/audit-storage.mjs)（"预上传返回 fileId/objectKey/uploadUrl"） | PASS（2026-09-02） |
| @STO-S2 | 同上（"重复预上传 isExist=true 复用 fileId"） | PASS（2026-09-02） |
| @STO-S3 | 同上（"fileSize=0 校验 400"） | PASS（2026-09-02） |
| @STO-S6 | 同上（"PUT 预签名地址直传 MinIO"） | PASS（2026-09-02） |
| @STO-S7 | 同上（"完成上传返回 objectKey/accessUrl"） | PASS（2026-09-02） |
| @STO-S15 | 同上（"匿名 GET accessUrl 字节一致"） | PASS（2026-09-02） |
| @STO-S16 | 同上（"私有/不存在对象 /static 404"） | PASS（2026-09-02） |
| @STO-S4、@STO-S5、@STO-S8 ~ @STO-S14、@STO-S17、@STO-S18 | @manual HTTP 走查（[SOP 第 4 节](./sop.md)；401/损坏/抢占/过期等分支 2026-09-01 第二轮深度 API 互证） | PASS（2026-09-01） |
| @STO-S19 ~ @STO-S21 | @manual 代码走查（IStorageService 状态机与 S3Client 实现，见 SDD 关键决策） | PASS（2026-09-01） |
| @STO-S22 ~ @STO-S24 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01/02） |

## 回归命令

```bash
node local-dev/audit-storage.mjs   # 需后端 :5210 + MinIO 运行；7 断言全链路
```

## 覆盖率说明

- 自动化覆盖主链路（预上传→直传→完成→匿名访问→秒传→校验/边界 404）；409 分支（损坏/抢占/过期）与领域服务为手动/走查。
- 已知未覆盖：大小校验绕过（同长度不同内容，见 SDD 已知问题）；`/static` 405 未自动化。
