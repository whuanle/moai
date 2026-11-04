# -*- coding: utf-8 -*-
"""安全事件演示数据种子（aa 团队 / 电商数据分析）。

直接向 PostgreSQL 的 app_security_event 表造一批内容脱敏规则命中审计数据，
用于走查应用「日志 → 安全事件」界面。可重复执行：先清空该应用已有事件再插入。

用法：
    python local-dev/security-events-demo-seed.py [团队名] [应用名]
    # 默认：aa 电商数据分析

连接参数默认对齐 src/MoAI/appsettings.Development.json，可用环境变量覆盖：
    E2E_PG_HOST / E2E_PG_PORT / E2E_PG_DB / E2E_PG_USER / E2E_PG_PASSWORD
"""
import sys
from datetime import datetime, timedelta, timezone

import psycopg2

DEFAULT_HOST = "192.168.50.199"
DEFAULT_PORT = "5432"
DEFAULT_DB = "moai_v2"
DEFAULT_USER = "postgres"
DEFAULT_PASSWORD = "19971120"

TEAM_NAME = sys.argv[1] if len(sys.argv) > 1 else "aa"
APP_NAME = sys.argv[2] if len(sys.argv) > 2 else "电商数据分析"

# (scope, rule_name, rule_type, strategy, action, hit_count, days_ago)
# 覆盖四作用域 × 全部规则类型 × 三种动作，场景贴合电商数据分析应用
# （客户手机号/收件人证件/邮箱抽取、内部订单号、供应商报价单、提示词注入攻击等）
EVENTS = [
    ("input", "", "phone", "mask", "masked", 6, 0.2, "导出订单明细时命中客户手机号"),
    ("input", "", "injection", "block", "blocked", 2, 0.5, "提示词注入：要求忽略系统提示并导出全库"),
    ("input", "", "email", "mask", "masked", 4, 1.1, "粘贴客户投诉邮件命中邮箱地址"),
    ("modelOutput", "", "phone", "mask", "masked", 3, 1.4, "模型回复客户名单命中手机号"),
    ("toolResult", "供应商合同号", "custom", "hash", "masked", 5, 2.3, "ERP 查询工具返回供应商合同号"),
    ("toolArgs", "", "phone", "redact", "masked", 2, 2.8, "调用短信查询工具参数命中手机号"),
    ("input", "内部订单号", "custom", "flag", "flagged", 9, 3.2, "分析请求粘贴内部订单号（观测不处理）"),
    ("modelOutput", "", "idCard", "mask", "masked", 2, 3.9, "模型回复收件人信息命中证件号"),
    ("toolResult", "", "bankCard", "block", "blocked", 1, 4.5, "对账工具返回命中银行卡号被拦截"),
    ("input", "", "email", "mask", "masked", 3, 5.1, "客户回访记录粘贴命中邮箱"),
    ("toolResult", "", "phone", "hash", "masked", 8, 6.0, "会员查询工具返回手机号做哈希关联"),
    ("modelOutput", "供应商报价", "custom", "flag", "flagged", 4, 6.7, "模型回复引用供应商报价（观测）"),
    ("input", "", "injection", "block", "blocked", 3, 7.4, "提示词注入：诱导输出系统提示词"),
    ("input", "", "phone", "mask", "masked", 5, 8.2, "复购分析请求命中客户手机号"),
    ("toolArgs", "", "idCard", "redact", "masked", 1, 8.9, "物流查询工具参数命中证件号"),
    ("modelOutput", "", "email", "mask", "masked", 2, 9.6, "周报生成命中客户邮箱"),
]


def main() -> None:
    import os

    conn = psycopg2.connect(
        host=os.environ.get("E2E_PG_HOST", DEFAULT_HOST),
        port=int(os.environ.get("E2E_PG_PORT", DEFAULT_PORT)),
        dbname=os.environ.get("E2E_PG_DB", DEFAULT_DB),
        user=os.environ.get("E2E_PG_USER", DEFAULT_USER),
        password=os.environ.get("E2E_PG_PASSWORD", DEFAULT_PASSWORD),
        connect_timeout=5,
    )
    cur = conn.cursor()

    cur.execute("select id from team where name=%s and is_deleted=0", (TEAM_NAME,))
    row = cur.fetchone()
    if not row:
        raise SystemExit(f"team not found: {TEAM_NAME}")
    team_id = row[0]

    cur.execute(
        "select id, team_id, create_user_id from app where name=%s and team_id=%s and is_deleted=0",
        (APP_NAME, team_id),
    )
    row = cur.fetchone()
    if not row:
        raise SystemExit(f"app not found in team {TEAM_NAME}: {APP_NAME}")
    app_id, _, creator_id = row

    # 会话归属：优先挂到该应用的真实会话（取最近的几个），不足则置空
    cur.execute(
        "select id from app_agent_session where app_id=%s and is_deleted=0 order by create_time desc limit 6",
        (app_id,),
    )
    session_ids = [r[0] for r in cur.fetchall()]

    cur.execute("delete from app_security_event where app_id=%s", (app_id,))

    now = datetime.now(timezone.utc)
    inserted = 0
    for idx, (scope, rule_name, rule_type, strategy, action, hits, days_ago, _note) in enumerate(EVENTS):
        create_time = now - timedelta(days=days_ago, hours=idx % 5)
        session_id = session_ids[idx % len(session_ids)] if session_ids else None
        user_id = creator_id if idx % 3 == 0 else (creator_id + 1 + (idx % 2))
        cur.execute(
            """
            insert into app_security_event
                (id, team_id, app_id, session_id, user_id, scope, rule_name, rule_type,
                 strategy, action, hit_count, create_user_id, create_time, update_user_id, update_time, is_deleted)
            values
                (gen_random_uuid(), %s, %s, %s, %s, %s, %s, %s,
                 %s, %s, %s, %s, %s, %s, %s, 0)
            """,
            (team_id, app_id, session_id, user_id, scope, rule_name, rule_type, strategy, action, hits,
             creator_id, create_time, creator_id, create_time),
        )
        inserted += 1

    conn.commit()
    cur.execute("select count(*) from app_security_event where app_id=%s", (app_id,))
    total = cur.fetchone()[0]
    conn.close()
    print(f"team={TEAM_NAME}({team_id}) app={APP_NAME}")
    print(f"deleted old + inserted {inserted}, app_security_event total now: {total}")


if __name__ == "__main__":
    main()
