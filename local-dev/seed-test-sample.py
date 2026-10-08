# -*- coding: utf-8 -*-
"""
生成 test_sample 示例电商数据库（AI 数据分析演示用）。

星型模型：customers / products / orders / order_items
时间范围：2025-01-01 ~ 2026-09-30（21 个月）

数据中预埋的"故事"（供 AI 分析报告发现）：
1. 大促尖峰：2025-11（双11）订单量约 2.2 倍，2025-06 / 2026-06（618）约 1.6~1.7 倍，12 月小高峰
2. 品类兴衰：智能家居份额逐月上升（~16% -> ~45%），办公用品持续衰退（~24% -> ~3%）
3. 季节性：户外运动 6-8 月上扬；服饰鞋包 11/12/1 月冬季上扬
4. 渠道迁移：app 渠道份额 40% -> 65%，web 从 40% 降到 20%
5. 退款率：服饰鞋包 ~12%，其他品类 ~5-6%
6. 客群：高会员等级客户下单更频繁（top 客户集中度可观）
7. 周末订单量略低于工作日
"""
import random
from datetime import date, datetime, timedelta
from decimal import Decimal

import psycopg2
from psycopg2.extras import execute_values

random.seed(42)

HOST, PORT, USER, PW = "192.168.50.199", 5432, "postgres", "19971120"
DB = "test_sample"
TODAY = date(2026, 10, 8)
START, END = date(2025, 1, 1), date(2026, 9, 30)

# ---------------------------------------------------------------- 维表数据

SURNAMES = "王李张刘陈杨黄赵吴周徐孙马朱胡郭何高林罗郑梁谢宋唐许韩冯邓曹彭曾肖田董潘袁蔡蒋余于杜叶程苏魏吕丁任沈姚卢姜崔钟谭陆汪范金石廖贾夏韦付方白邹孟熊秦邱江尹薛闫段雷侯龙史陶黎贺顾毛郝龚邵万钱严"
GIVEN = ["伟", "芳", "娜", "敏", "静", "丽", "强", "磊", "军", "洋", "勇", "艳", "杰", "娟", "涛", "明", "超", "霞", "平", "刚", "辉", "鹏", "华", "梅", "琳", "宇", "欣", "怡", "晨", "昊", "雨", "泽", "子", "萱", "浩", "然", "一诺", "思远", "梦琪", "致远"]
REGIONS = ["华东", "华南", "华北", "西南", "东北", "西北"]
REGION_W = [0.35, 0.25, 0.20, 0.10, 0.06, 0.04]
LEVELS = ["普通会员", "银卡会员", "金卡会员", "铂金会员"]
LEVEL_W = [0.55, 0.25, 0.15, 0.05]
LEVEL_ORDER_W = {"普通会员": 1.0, "银卡会员": 2.0, "金卡会员": 4.0, "铂金会员": 8.0}

# (品类, [(商品名, 吊牌价)])
PRODUCTS = [
    ("数码配件", [
        ("无线鼠标 M2", 79), ("无线键盘 K380", 199), ("机械键盘 87 键", 349),
        ("移动电源 20000mAh", 149), ("真无线蓝牙耳机", 249), ("USB-C 八合一扩展坞", 299),
        ("多合一读卡器", 39), ("铝合金笔记本支架", 129), ("1080P 高清摄像头", 199),
        ("电容麦克风", 399), ("移动硬盘盒", 159), ("100W 快充数据线", 29),
    ]),
    ("智能家居", [
        ("智能音箱 Pro", 399), ("智能门锁 S2", 1599), ("扫地机器人 X5", 2299),
        ("智能灯泡四只装", 129), ("智能插座", 59), ("智能窗帘电机", 699),
        ("室内云台摄像机", 249), ("智能温湿度计", 79), ("智能体脂秤", 169),
        ("智能无雾加湿器", 349), ("电动晾衣架", 1299), ("智能家居网关", 199),
    ]),
    ("办公用品", [
        ("中性笔盒装 10 支", 19.9), ("A5 笔记本", 15.9), ("便签纸三合一", 9.9),
        ("风琴文件夹", 25), ("订书机", 22), ("白板笔三支装", 14.5),
        ("封箱胶带 6 卷", 8.8), ("不锈钢剪刀", 17), ("桌面计算器", 35),
        ("三层桌面收纳盒", 29), ("A4 复印纸一箱", 45), ("便携标签打印机", 199),
    ]),
    ("家居日用", [
        ("衣物收纳箱 66L", 39), ("316 不锈钢保温杯", 89), ("无火香薰机", 69),
        ("纯棉毛巾两条装", 39), ("防滑衣架十个装", 25), ("感应垃圾桶", 59),
        ("厨房置物架", 79), ("纯棉浴巾", 49), ("玻璃密封罐三件套", 42),
        ("家庭清洁工具套装", 59),
    ]),
    ("户外运动", [
        ("双人双层帐篷", 399), ("碳素登山杖", 89), ("Tritan 运动水壶", 39),
        ("TPE 瑜伽垫", 99), ("便携折叠椅", 129), ("防晒渔夫帽", 49),
        ("骑行手套", 39), ("户外露营挂灯", 59), ("25L 运动背包", 149),
        ("速干 T 恤", 79),
    ]),
    ("服饰鞋包", [
        ("纯棉圆领 T 恤", 69), ("直筒牛仔裤", 179), ("连帽卫衣", 129),
        ("通勤连衣裙", 199), ("透气缓震跑鞋", 299), ("经典帆布鞋", 159),
        ("双肩电脑包", 219), ("头层牛皮皮带", 129), ("棒球帽", 45),
        ("羊毛围巾", 159),
    ]),
]
CATEGORIES = [c for c, _ in PRODUCTS]
REFUND_RATE = {"服饰鞋包": 0.12, "家居日用": 0.06}  # 其余默认 0.05


def cat_weight(cat, y, m):
    """品类被选购权重（随时间演变，预埋品类兴衰与季节性）。mi=0 是 2025-01。"""
    mi = (y - 2025) * 12 + (m - 1)
    if cat == "智能家居":
        return 0.9 + 0.09 * mi          # 增长线
    if cat == "办公用品":
        return max(0.2, 1.3 - 0.055 * mi)  # 衰退线
    if cat == "户外运动":
        return 0.6 + (0.7 if m in (6, 7, 8) else 0)  # 夏季上扬
    if cat == "服饰鞋包":
        return 0.7 + (0.2 if m in (11, 12, 1) else 0)  # 冬季上扬
    return 1.0 if cat == "数码配件" else 0.8


def month_index(d):
    return (d.year - 2025) * 12 + (d.month - 1)


# ---------------------------------------------------------------- 建库

admin = psycopg2.connect(host=HOST, port=PORT, user=USER, password=PW, dbname="postgres")
admin.autocommit = True
with admin.cursor() as c:
    c.execute("SELECT 1 FROM pg_database WHERE datname = %s", (DB,))
    if not c.fetchone():
        c.execute(f'CREATE DATABASE "{DB}"')
        print(f"[ok] 数据库 {DB} 已创建")
    else:
        print(f"[ok] 数据库 {DB} 已存在，检查是否为空")
admin.close()

conn = psycopg2.connect(host=HOST, port=PORT, user=USER, password=PW, dbname=DB)
cur = conn.cursor()
cur.execute("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'")
if cur.fetchone()[0]:
    raise SystemExit(f"[abort] {DB} 已含业务表，为避免误删请先人工处理")

cur.execute("DROP TABLE IF EXISTS order_items, orders, customers, products CASCADE")

cur.execute("""
CREATE TABLE customers (
    customer_id   serial PRIMARY KEY,
    customer_name varchar(50) NOT NULL,
    gender        char(1) NOT NULL,
    region        varchar(20) NOT NULL,
    member_level  varchar(10) NOT NULL,
    registered_at date NOT NULL
);
CREATE TABLE products (
    product_id   serial PRIMARY KEY,
    product_name varchar(100) NOT NULL,
    category     varchar(20) NOT NULL,
    unit_price   numeric(10,2) NOT NULL,
    unit_cost    numeric(10,2) NOT NULL
);
CREATE TABLE orders (
    order_id     serial PRIMARY KEY,
    order_no     varchar(20) NOT NULL UNIQUE,
    customer_id  int NOT NULL REFERENCES customers(customer_id),
    order_date   timestamp NOT NULL,
    status       varchar(10) NOT NULL,
    channel      varchar(10) NOT NULL,
    total_amount numeric(12,2) NOT NULL
);
CREATE TABLE order_items (
    item_id    serial PRIMARY KEY,
    order_id   int NOT NULL REFERENCES orders(order_id),
    product_id int NOT NULL REFERENCES products(product_id),
    quantity   int NOT NULL,
    unit_price numeric(10,2) NOT NULL
);
CREATE INDEX idx_orders_order_date  ON orders (order_date);
CREATE INDEX idx_orders_customer_id ON orders (customer_id);
CREATE INDEX idx_items_product_id   ON order_items (product_id);
CREATE INDEX idx_items_order_id     ON order_items (order_id);

COMMENT ON DATABASE test_sample             IS '示例电商数据库（AI 数据分析演示用）';
COMMENT ON TABLE customers                  IS '客户维表';
COMMENT ON COLUMN customers.region          IS '所在大区：华东/华南/华北/西南/东北/西北';
COMMENT ON COLUMN customers.member_level    IS '会员等级：普通会员/银卡会员/金卡会员/铂金会员';
COMMENT ON TABLE products                   IS '商品维表';
COMMENT ON COLUMN products.unit_price       IS '吊牌价（当前）';
COMMENT ON COLUMN products.unit_cost       IS '单位成本';
COMMENT ON TABLE orders                     IS '订单事实表';
COMMENT ON COLUMN orders.status             IS '订单状态：pending待付款/shipped已发货/completed已完成/cancelled已取消/refunded已退款';
COMMENT ON COLUMN orders.channel            IS '下单渠道：app/web/miniapp（小程序）';
COMMENT ON COLUMN orders.total_amount       IS '订单金额（=明细 quantity*unit_price 之和）';
COMMENT ON TABLE order_items                IS '订单明细（unit_price 为下单时快照价）';
""")

# ---------------------------------------------------------------- 维表数据

customers = []
for _ in range(500):
    name = random.choice(SURNAMES) + "".join(random.sample(GIVEN, random.choice([1, 1, 2])))
    reg = date(2023, 1, 1) + timedelta(days=random.randint(0, 1000))
    customers.append((
        name,
        random.choice("MF"),
        random.choices(REGIONS, REGION_W)[0],
        random.choices(LEVELS, LEVEL_W)[0],
        reg,
    ))
execute_values(cur, "INSERT INTO customers (customer_name,gender,region,member_level,registered_at) VALUES %s", customers)

product_rows = []          # (category, name, price, cost)
for cat, items in PRODUCTS:
    for name, price in items:
        cost = round(price * random.uniform(0.50, 0.72), 2)
        product_rows.append((cat, name, Decimal(str(price)), Decimal(str(cost))))
execute_values(cur, "INSERT INTO products (category,product_name,unit_price,unit_cost) VALUES %s", product_rows)

cur.execute("SELECT product_id, category, product_name, unit_price FROM products ORDER BY product_id")
prods = cur.fetchall()                     # (id, cat, name, price)
by_cat = {c: [p for p in prods if p[1] == c] for c in CATEGORIES}
cust_w = [LEVEL_ORDER_W[c[3]] for c in customers]

# ---------------------------------------------------------------- 订单生成

orders, items = [], []
d = START
while d <= END:
    y, m, wd = d.year, d.month, d.weekday()
    promo = {(2025, 6): 1.6, (2025, 11): 2.2, (2025, 12): 1.25, (2026, 6): 1.7}.get((y, m), 1.0)
    weekend = 0.8 if wd >= 5 else 1.0
    n_orders = max(3, round(13 * promo * weekend * random.uniform(0.75, 1.25)))

    weights = [cat_weight(c, y, m) for c in CATEGORIES]
    mi = month_index(d)
    app_share = 0.40 + 0.25 * mi / 20       # 40% -> 65%
    web_share = 0.40 - 0.20 * mi / 20       # 40% -> 20%

    for seq in range(1, n_orders + 1):
        cust_idx = random.choices(range(500), cust_w)[0]
        hour = max(8, min(22, int(random.gauss(14, 4))))
        odt = datetime(y, m, d.day, hour, random.choice([0, 10, 15, 30, 45]))
        r = random.random()
        channel = "app" if r < app_share else ("web" if r < app_share + web_share else "miniapp")

        n_items = random.choices([1, 2, 3, 4], weights=[0.35, 0.40, 0.20, 0.05])[0]
        picks = random.choices(CATEGORIES, weights, k=n_items)
        chosen, amount = [], Decimal("0")
        for cat in picks:
            p = random.choice(by_cat[cat])
            qty = random.choices([1, 2, 3, 5], weights=[0.5, 0.3, 0.15, 0.05])[0] if float(p[3]) < 50 else \
                random.choices([1, 2, 3], weights=[0.7, 0.25, 0.05])[0]
            chosen.append((p[0], qty, p[3]))
            amount += qty * p[3]
        main_cat = max(set(picks), key=picks.count)

        if d > TODAY - timedelta(days=7):           # 近一周：在途
            status = "pending" if random.random() < 0.4 else "shipped"
        elif d > TODAY - timedelta(days=30):        # 近一月：大部分完成
            rr = random.random()
            status = "shipped" if rr < 0.3 else ("cancelled" if rr < 0.33 else "completed")
        else:
            rr = random.random()
            if rr < 0.06:
                status = "cancelled"
            elif rr < 0.06 + REFUND_RATE.get(main_cat, 0.05):
                status = "refunded"
            else:
                status = "completed"

        orders.append((f"SO{d:%Y%m%d}{seq:04d}", cust_idx + 1, odt, status, channel, amount))
        oid = len(orders)
        items.extend((oid, pid, qty, price) for pid, qty, price in chosen)
    d += timedelta(days=1)

execute_values(cur, "INSERT INTO orders (order_no,customer_id,order_date,status,channel,total_amount) VALUES %s", orders,
               "(%s,%s,%s,%s,%s,%s)")
execute_values(cur, "INSERT INTO order_items (order_id,product_id,quantity,unit_price) VALUES %s", items)
conn.commit()

# ---------------------------------------------------------------- 验证

print(f"\n[数据量] customers=500 products={len(prods)} orders={len(orders)} order_items={len(items)}")

cur.execute("""
    SELECT to_char(order_date,'YYYY-MM') m, count(*), sum(total_amount)::bigint
    FROM orders GROUP BY 1 ORDER BY 1""")
print("\n[月度趋势]（订单数 / GMV）")
for m, n, gmv in cur.fetchall():
    print(f"  {m}  {n:>4}  {gmv:>10,}")

cur.execute("""
    WITH t AS (SELECT o.order_id, date_trunc('month', o.order_date) mo, p.category
               FROM orders o JOIN order_items i ON i.order_id=o.order_id JOIN products p ON p.product_id=i.product_id)
    SELECT CASE WHEN mo < '2025-04-01' THEN '2025Q1' ELSE '2026Q3' END period, category,
           round(100.0*count(*)/sum(count(*)) OVER (PARTITION BY CASE WHEN mo < '2025-04-01' THEN '2025Q1' ELSE '2026Q3' END), 1) pct
    FROM t WHERE mo < '2025-04-01' OR mo >= '2026-07-01'
    GROUP BY 1, 2 ORDER BY 1, 3 DESC""")
print("\n[品类结构变迁] 2025Q1 vs 2026Q3（按明细行数份额）")
for period, cat, pct in cur.fetchall():
    print(f"  {period}  {cat}  {pct}%")

cur.execute("""
    SELECT p.category, round(100.0*count(*) FILTER (WHERE o.status='refunded')/count(*),1)
    FROM orders o JOIN order_items i ON i.order_id=o.order_id JOIN products p ON p.product_id=i.product_id
    GROUP BY 1 ORDER BY 2 DESC""")
print("\n[各品类退款率]")
for cat, rate in cur.fetchall():
    print(f"  {cat}  {rate}%")

cur.execute("""
    WITH q AS (SELECT to_char(date_trunc('quarter', order_date),'YYYY-Q') q, channel FROM orders)
    SELECT q, channel, round(100.0*count(*)/sum(count(*)) OVER (PARTITION BY q),1)
    FROM q GROUP BY 1,2 ORDER BY 1,2""")
print("\n[渠道份额按季度]")
for q, ch, pct in cur.fetchall():
    print(f"  {q}  {ch:<8} {pct}%")

cur.close()
conn.close()
print("\n[done] test_sample 灌数完成")
