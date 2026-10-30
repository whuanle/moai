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

            // org_role / org_user_role（角色组织）为后加表：EnsureCreated 只在建库时建表，
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

            // ai_channel.team_id（团队自有模型渠道）为后加列，幂等补齐；
            // 额度体系（ai_model_limit/ai_model_quota）已整体下线，存量表直接清除（DDL 与 asserts/ai_channel_team.sql 等价）
            dbContext.Database.ExecuteSqlRaw(
                "ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0");
            dbContext.Database.ExecuteSqlRaw(
                "CREATE INDEX IF NOT EXISTS idx_ai_channel_team_id ON ai_channel (team_id) WHERE is_deleted = 0");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ai_model_quota");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS ai_model_limit");

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