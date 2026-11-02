using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace MoAI.Database;

/// <summary>
/// DatabasPostgresModule.
/// </summary>
public class DatabasPostgresModule : IModule
{
    private readonly ILogger<DatabasPostgresModule> _logger;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabasPostgresModule"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="systemOptions"></param>
    public DatabasPostgresModule(ILogger<DatabasPostgresModule> logger, SystemOptions systemOptions)
    {
        _logger = logger;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

        var connectionString = new NpgsqlDataSourceBuilder(_systemOptions.Database);

        Action<DbContextOptionsBuilder> contextOptionsBuilder = o =>
        {
            o.UseNpgsql(_systemOptions.Database)
                .ConfigureWarnings(
                    b => b.Ignore([
                        CoreEventId.ServiceProviderCreated,
                        CoreEventId.ContextInitialized,
                        CoreEventId.ContextDisposed,
                        CoreEventId.LazyLoadOnDisposedContextWarning,
                        CoreEventId.QueryCompilationStarting,
                        CoreEventId.StateChanged,
                        CoreEventId.SaveChangesCanceled,
                        CoreEventId.SaveChangesCompleted,
                        CoreEventId.SensitiveDataLoggingEnabledWarning,
                        CoreEventId.QueryExecutionPlanned,
                        CoreEventId.StartedTracking,
                        RelationalEventId.ConnectionOpening,
                        RelationalEventId.ConnectionCreating,
                        RelationalEventId.ConnectionCreated,
                        RelationalEventId.ConnectionClosing,
                        RelationalEventId.ConnectionClosed,
                        RelationalEventId.DataReaderClosing,
                        RelationalEventId.DataReaderDisposing,
                        RelationalEventId.CommandCanceled,
                        RelationalEventId.CommandCreated,
                        RelationalEventId.CommandCreating,
                        RelationalEventId.CommandInitialized,
                        RelationalEventId.BoolWithDefaultWarning,
                        RelationalEventId.ModelValidationKeyDefaultValueWarning
                    ]))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors();
        };

        context.Services.AddDbContext<DatabaseContext, PostgresDatabaseContext>(contextOptionsBuilder);

        try
        {
            DbContextOptionsBuilder<PostgresDatabaseContext> options = new();
            contextOptionsBuilder.Invoke(options);

            using var ioc = context.Services.BuildServiceProvider();
            using PostgresDatabaseContext? dbContext = new PostgresDatabaseContext(options.Options, ioc);

            // 如果数据库不存在，则会创建数据库及其所有表。
            //dbContext.Database.Migrate();
            dbContext.Database.EnsureCreated();

            // EnsureCreated 不会为已存在的表补列：oauth_connection.agent_id（企业微信登录）为后加列，
            // 用幂等 DDL 给存量库补齐，否则旧部署升级后 OAuth 查询会因缺列报 42703.
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS agent_id character varying(100) NULL");

            // app.use_count（应用累计使用次数）为后加列，幂等补齐
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE app ADD COLUMN IF NOT EXISTS use_count integer NOT NULL DEFAULT 0");

            // user_oauth_connection.union_id（飞书租户级唯一id，渠道用户识别跨应用匹配）为后加列，幂等补齐
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS union_id character varying(100) NULL");
            dbContext.Database.ExecuteSqlRaw(
                "CREATE INDEX IF NOT EXISTS idx_user_oauth_provider_id_union_id ON user_oauth_connection (provider_id, union_id) WHERE union_id IS NOT NULL");

            // user.totp_secret / user.is_totp_enabled（两步验证）为后加列，幂等补齐（user 是保留字需引号）
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE \"user\" ADD COLUMN IF NOT EXISTS totp_secret character varying(200) NULL");
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE \"user\" ADD COLUMN IF NOT EXISTS is_totp_enabled boolean NOT NULL DEFAULT false");

            // org_role / org_user_role（组织）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/org.sql 等价，免手工执行）
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS org_role (
                    id serial NOT NULL,
                    name varchar(50) NOT NULL,
                    parent_id int NULL,
                    path varchar(255) NOT NULL DEFAULT ''::text,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_org_role_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_org_role_path_uindex ON org_role (path) WHERE is_deleted = 0;
                CREATE INDEX IF NOT EXISTS idx_org_role_parent_id ON org_role (parent_id);
                CREATE TABLE IF NOT EXISTS org_user_role (
                    id serial NOT NULL,
                    user_id bigint NOT NULL,
                    role_id int NOT NULL,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_org_user_role_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_org_user_role_user_role_live_uindex ON org_user_role (user_id, role_id) WHERE is_deleted = 0;
                CREATE INDEX IF NOT EXISTS idx_org_user_role_role_id ON org_user_role (role_id);
                CREATE TABLE IF NOT EXISTS org_grant (
                    id serial NOT NULL,
                    resource_type int NOT NULL,
                    resource_id varchar(64) NOT NULL,
                    org_role_id int NOT NULL,
                    scope int NOT NULL DEFAULT 0,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_org_grant_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_org_grant_resource_role_live_uindex ON org_grant (resource_type, resource_id, org_role_id) WHERE is_deleted = 0;
                CREATE INDEX IF NOT EXISTS idx_org_grant_role_id ON org_grant (org_role_id);
                """);

            // org_role.sort_order（同级展示顺序，拖拽重排）2026-11-02 后加列：存量库幂等补列，
            // 并按 id 次序回填一次（业务写入恒 >=1，sort_order=0 即未回填的旧行，回填不会覆盖管理员后续调整）
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE org_role ADD COLUMN IF NOT EXISTS sort_order int NOT NULL DEFAULT 0");
            dbContext.Database.ExecuteSqlRaw(
                """
                UPDATE org_role o SET sort_order = d.rn
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY parent_id ORDER BY id) rn
                    FROM org_role WHERE is_deleted = 0
                ) d
                WHERE o.id = d.id AND o.sort_order = 0;
                """);

            // wiki_document_image（知识库文档内嵌图片附件）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/wiki_document_image.sql 等价，免手工执行）
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS wiki_document_image (
                    id serial NOT NULL,
                    wiki_id int NOT NULL,
                    document_id int NOT NULL,
                    file_id bigint NOT NULL,
                    object_key varchar(255) NOT NULL,
                    file_name varchar(1024) NOT NULL,
                    file_type varchar(10) NOT NULL,
                    page_number int NULL,
                    image_index int NOT NULL DEFAULT 0,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_wiki_document_image_primary PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_wiki_document_image_document_id_index ON wiki_document_image (wiki_id, document_id) WHERE is_deleted = 0;
                """);

            // resource_usage_daily / resource_usage_total（资源使用统计）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/resource_usage.sql 等价，免手工执行）；
            // usage_hour（统计粒度按小时桶）为后加列，旧三列唯一索引替换为四列
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS resource_usage_daily (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    usage_date date NOT NULL,
                    usage_hour integer NOT NULL DEFAULT -1,
                    name varchar(100) NOT NULL,
                    resource_id varchar(128) NOT NULL,
                    count bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    CONSTRAINT idx_resource_usage_daily_primary PRIMARY KEY (id)
                );
                ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS usage_hour integer NOT NULL DEFAULT -1;
                DROP INDEX IF EXISTS idx_resource_usage_daily_date_name_resource_uindex;
                CREATE UNIQUE INDEX IF NOT EXISTS idx_resource_usage_daily_date_hour_name_resource_uindex ON resource_usage_daily (usage_date, usage_hour, name, resource_id);
                CREATE INDEX IF NOT EXISTS idx_resource_usage_daily_name_date_index ON resource_usage_daily (name, usage_date);
                CREATE TABLE IF NOT EXISTS resource_usage_total (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    name varchar(100) NOT NULL,
                    resource_id varchar(128) NOT NULL,
                    total bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    CONSTRAINT idx_resource_usage_total_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_resource_usage_total_name_resource_uindex ON resource_usage_total (name, resource_id);
                """);

            // ai_channel.team_id（团队自有模型渠道）为后加列，幂等补齐；
            // 额度体系（ai_model_limit/ai_model_quota）已整体下线，存量表直接清除（DDL 与 asserts/ai_channel_team.sql 等价）
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0");
            dbContext.Database.ExecuteSqlRaw(
                "CREATE INDEX IF NOT EXISTS idx_ai_channel_team_id ON ai_channel (team_id) WHERE is_deleted = 0");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ai_model_quota");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ai_model_limit");

            // message（站内信）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/message.sql 等价，免手工执行）
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS message (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    recipient_user_id bigint NOT NULL,
                    title varchar(256) NOT NULL,
                    content varchar(4000) NOT NULL,
                    message_type int NOT NULL DEFAULT 1,
                    source varchar(50) NOT NULL DEFAULT ''::character varying,
                    sender_user_id bigint NULL,
                    params varchar(1024) NULL,
                    is_read boolean NOT NULL DEFAULT false,
                    read_time timestamptz NULL,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_message_primary PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_message_recipient_read ON message (recipient_user_id, is_read) WHERE is_deleted = 0;
                CREATE INDEX IF NOT EXISTS idx_message_recipient_time ON message (recipient_user_id, create_time DESC) WHERE is_deleted = 0;
                """);

            // notification_channel / notification_log（通知中心）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/notification.sql 等价，免手工执行）
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS notification_channel (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    name varchar(100) NOT NULL,
                    type int NOT NULL DEFAULT 1,
                    config varchar(4000) NOT NULL,
                    enabled boolean NOT NULL DEFAULT true,
                    description varchar(500) NOT NULL DEFAULT ''::character varying,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_notification_channel_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_channel_name_uindex ON notification_channel (name) WHERE is_deleted = 0;

                CREATE TABLE IF NOT EXISTS notification_log (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    channel_id bigint NOT NULL DEFAULT 0,
                    channel_name varchar(100) NOT NULL DEFAULT ''::character varying,
                    channel_type int NOT NULL DEFAULT 1,
                    level int NOT NULL DEFAULT 0,
                    title varchar(256) NOT NULL,
                    content varchar(4000) NOT NULL,
                    status int NOT NULL DEFAULT 0,
                    error varchar(1000) NOT NULL DEFAULT ''::character varying,
                    duration_ms bigint NOT NULL DEFAULT 0,
                    target_user_ids varchar(1000) NOT NULL DEFAULT ''::character varying,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_notification_log_primary PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_notification_log_time ON notification_log (create_time DESC) WHERE is_deleted = 0;
                CREATE INDEX IF NOT EXISTS idx_notification_log_channel ON notification_log (channel_id, create_time DESC) WHERE is_deleted = 0;
                ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS target_user_ids character varying(1000) NOT NULL DEFAULT ''::character varying;
                ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS recipients character varying(1000) NOT NULL DEFAULT ''::character varying;

                CREATE TABLE IF NOT EXISTS notification_template (
                    id bigint GENERATED BY DEFAULT AS IDENTITY,
                    code varchar(64) NOT NULL DEFAULT ''::character varying,
                    name varchar(100) NOT NULL,
                    title varchar(256) NOT NULL DEFAULT ''::character varying,
                    content varchar(10000) NOT NULL,
                    content_type int NOT NULL DEFAULT 0,
                    variables varchar(4000) NOT NULL DEFAULT ''::character varying,
                    aliyun_template_code varchar(128) NOT NULL DEFAULT ''::character varying,
                    tencent_template_id varchar(128) NOT NULL DEFAULT ''::character varying,
                    description varchar(500) NOT NULL DEFAULT ''::character varying,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    update_user_id bigint NOT NULL DEFAULT 0,
                    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_deleted bigint NOT NULL DEFAULT 0,
                    CONSTRAINT idx_notification_template_primary PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_template_name_uindex ON notification_template (name) WHERE is_deleted = 0;
                ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS code varchar(64) NOT NULL DEFAULT ''::character varying;
                CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_template_code_uindex ON notification_template (code) WHERE is_deleted = 0 AND code <> '';
                """);

            // login_log / login_risk_event / login_ip_ban（登录安全风控）为后加表：EnsureCreated 只在建库时建表，
            // 存量库用幂等 DDL 补齐（与 asserts/loginsafety.sql 等价，免手工执行）
            dbContext.Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS login_log (
                    id uuid NOT NULL DEFAULT uuid_generate_v4(),
                    user_id bigint NOT NULL DEFAULT 0,
                    user_name varchar(200) NOT NULL DEFAULT ''::character varying,
                    login_type int NOT NULL DEFAULT 0,
                    is_success boolean NOT NULL DEFAULT false,
                    fail_reason varchar(200) NOT NULL DEFAULT ''::character varying,
                    ip varchar(64) NOT NULL DEFAULT ''::character varying,
                    country varchar(64) NOT NULL DEFAULT ''::character varying,
                    province varchar(64) NOT NULL DEFAULT ''::character varying,
                    city varchar(64) NOT NULL DEFAULT ''::character varying,
                    isp varchar(128) NOT NULL DEFAULT ''::character varying,
                    user_agent varchar(500) NOT NULL DEFAULT ''::character varying,
                    risk_level int NOT NULL DEFAULT 0,
                    risk_reasons varchar(2000) NOT NULL DEFAULT ''::character varying,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    CONSTRAINT login_log_pkey PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_login_log_user_id ON login_log (user_id);
                CREATE INDEX IF NOT EXISTS idx_login_log_ip ON login_log (ip);
                CREATE INDEX IF NOT EXISTS idx_login_log_create_time ON login_log (create_time);
                CREATE TABLE IF NOT EXISTS login_risk_event (
                    id uuid NOT NULL DEFAULT uuid_generate_v4(),
                    user_id bigint NOT NULL DEFAULT 0,
                    user_name varchar(200) NOT NULL DEFAULT ''::character varying,
                    rule_code varchar(8) NOT NULL DEFAULT ''::character varying,
                    risk_level int NOT NULL DEFAULT 0,
                    summary varchar(1000) NOT NULL DEFAULT ''::character varying,
                    detail varchar(2000) NOT NULL DEFAULT ''::character varying,
                    is_read boolean NOT NULL DEFAULT false,
                    ip varchar(64) NOT NULL DEFAULT ''::character varying,
                    province varchar(64) NOT NULL DEFAULT ''::character varying,
                    city varchar(64) NOT NULL DEFAULT ''::character varying,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    CONSTRAINT login_risk_event_pkey PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_login_risk_event_user_id ON login_risk_event (user_id);
                CREATE INDEX IF NOT EXISTS idx_login_risk_event_is_read ON login_risk_event (is_read);
                CREATE INDEX IF NOT EXISTS idx_login_risk_event_create_time ON login_risk_event (create_time);
                CREATE TABLE IF NOT EXISTS login_ip_ban (
                    id uuid NOT NULL DEFAULT uuid_generate_v4(),
                    ip varchar(64) NOT NULL DEFAULT ''::character varying,
                    reason varchar(200) NOT NULL DEFAULT ''::character varying,
                    source_rule varchar(32) NOT NULL DEFAULT ''::character varying,
                    expire_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    is_lifted boolean NOT NULL DEFAULT false,
                    lift_user_id bigint NOT NULL DEFAULT 0,
                    lift_time timestamptz NULL,
                    create_user_id bigint NOT NULL DEFAULT 0,
                    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
                    CONSTRAINT login_ip_ban_pkey PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS idx_login_ip_ban_ip ON login_ip_ban (ip);
                """);

            // 种子数据(如 user/setting/classify)使用显式 Id 插入，不会推进 serial/identity 序列，
            // 会导致后续自增时主键冲突。此处将所有序列同步到对应表当前最大 Id，保证后续插入不冲突。
            dbContext.Database.ExecuteSqlRaw(
                """
                DO $$
                DECLARE
                    rec record;
                BEGIN
                    FOR rec IN
                        SELECT n.nspname AS schema_name,
                               c.relname AS table_name,
                               a.attname AS column_name
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
                        WHERE c.relkind IN ('r', 'p')
                          AND pg_get_serial_sequence(format('%I.%I', n.nspname, c.relname), a.attname) IS NOT NULL
                    LOOP
                        EXECUTE format(
                            'SELECT setval(%L, COALESCE((SELECT MAX(%I) FROM %s), 0) + 1, false)',
                            pg_get_serial_sequence(format('%I.%I', rec.schema_name, rec.table_name), rec.column_name),
                            rec.column_name,
                            format('%I.%I', rec.schema_name, rec.table_name));
                    END LOOP;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "The database initialization failed. Please check if the database connection string is correct.");
            throw;
        }
    }
}