using MoAI.Database.Schema;
using Xunit;

namespace MoAI.Database.Tests;

public class SchemaDdlTests
{
    [Fact]
    public void BuildAddColumnDefinition_AddsFallbackDefaultForRequiredColumnWithoutDefault()
    {
        var column = Column("name", "character varying(50) NOT NULL");

        Assert.Equal("character varying(50) NOT NULL DEFAULT ''", SchemaDdl.BuildAddColumnDefinition(column));
    }

    [Theory]
    [InlineData("integer NOT NULL", "DEFAULT 0")]
    [InlineData("bigint NOT NULL", "DEFAULT 0")]
    [InlineData("boolean NOT NULL", "DEFAULT false")]
    [InlineData("uuid NOT NULL", "DEFAULT '00000000-0000-0000-0000-000000000000'")]
    [InlineData("jsonb NOT NULL", "DEFAULT '{}'::jsonb")]
    [InlineData("timestamp with time zone NOT NULL", "DEFAULT now()")]
    public void BuildAddColumnDefinition_DerivesDefaultByType(string definition, string expected)
    {
        var column = Column("col", definition);

        Assert.Contains(expected, SchemaDdl.BuildAddColumnDefinition(column), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAddColumnDefinition_KeepsExistingDefault()
    {
        var column = Column("level", "integer NOT NULL DEFAULT 0");

        Assert.Equal("integer NOT NULL DEFAULT 0", SchemaDdl.BuildAddColumnDefinition(column));
    }

    [Fact]
    public void BuildAddColumnDefinition_DropsNotNullWhenNoFallbackAvailable()
    {
        var column = Column("embedding", "vector(1536) NOT NULL");

        Assert.DoesNotContain("NOT NULL", SchemaDdl.BuildAddColumnDefinition(column), StringComparison.Ordinal);
    }

    [Fact]
    public void MakeIndexStatementIdempotent_HandlesUniqueAndPlainIndexes()
    {
        Assert.Equal(
            "CREATE INDEX IF NOT EXISTS idx_a ON t (a);",
            SchemaDdl.MakeIndexStatementIdempotent("CREATE INDEX idx_a ON t (a);"));
        Assert.Equal(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_b ON t (b);",
            SchemaDdl.MakeIndexStatementIdempotent("CREATE UNIQUE INDEX idx_b ON t (b);"));
    }

    [Fact]
    public void BuildIdempotentScript_CoversTablesColumnsIndexesAndSkipsSeedRows()
    {
        const string script = """
            CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
            CREATE SEQUENCE external_id_seq START WITH 1 INCREMENT BY 1 NO CYCLE;
            CREATE TABLE access_app (
                id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                name character varying(50) NOT NULL,
                CONSTRAINT idx_access_app_primary PRIMARY KEY (id)
            );
            COMMENT ON TABLE access_app IS '应用接入';
            CREATE INDEX idx_access_app_name ON access_app (name);
            INSERT INTO access_app (id, name) VALUES (gen_random_uuid(), 'a');
            """;

        var tables = SchemaScriptParser.Parse(script);
        var patch = SchemaDdl.BuildIdempotentScript(script, tables);

        Assert.Contains("CREATE SEQUENCE IF NOT EXISTS external_id_seq", patch, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS access_app", patch, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE access_app ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';", patch, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_access_app_name ON access_app (name);", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMENT ON", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE EXTENSION", patch, StringComparison.Ordinal);
    }

    private static ColumnDefinition Column(string name, string definition)
    {
        var typeName = definition;
        foreach (var modifier in new[] { " NOT NULL", " DEFAULT", " GENERATED", " COLLATE", " CHECK", " REFERENCES", " PRIMARY KEY", " UNIQUE" })
        {
            var index = typeName.IndexOf(modifier, StringComparison.Ordinal);
            if (index >= 0)
            {
                typeName = typeName[..index];
            }
        }

        return new ColumnDefinition
        {
            Name = name,
            RawName = name,
            Definition = definition,
            TypeName = typeName.Trim(),
            HasDefault = definition.Contains(" DEFAULT ", StringComparison.Ordinal),
            IsRequired = definition.Contains("NOT NULL", StringComparison.Ordinal),
            IsIdentity = definition.Contains("GENERATED", StringComparison.Ordinal),
            MaxLength = null,
        };
    }
}
