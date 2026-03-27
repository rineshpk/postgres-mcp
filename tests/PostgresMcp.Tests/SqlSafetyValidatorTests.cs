using PostgresMcp.Server.Validators;

namespace PostgresMcp.Tests;

public class SqlSafetyValidatorTests
{
    // ── Allowed statements ───────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT * FROM users")]
    [InlineData("SELECT id, name FROM orders WHERE id = 1")]
    [InlineData("SELECT COUNT(*) FROM pg_stat_user_tables")]
    public void Validate_AllowsSelectStatements(string sql)
        => SqlSafetyValidator.Validate(sql); // must not throw

    [Theory]
    [InlineData("EXPLAIN SELECT * FROM users")]
    [InlineData("EXPLAIN ANALYZE SELECT id FROM orders")]
    public void Validate_AllowsExplainStatements(string sql)
        => SqlSafetyValidator.Validate(sql);

    [Theory]
    [InlineData("select * from users")]
    [InlineData("Select Id From Products")]
    public void Validate_IsCaseInsensitiveForAllowedStatements(string sql)
        => SqlSafetyValidator.Validate(sql);

    // ── Statements that don't start with SELECT/EXPLAIN ──────────────────────
    // These fail the first guard ("Only SELECT/EXPLAIN allowed") before the
    // forbidden-keyword check is reached.

    [Theory]
    [InlineData("INSERT INTO users VALUES (1)")]
    [InlineData("UPDATE users SET name = 'x' WHERE id = 1")]
    [InlineData("DELETE FROM users WHERE id = 1")]
    [InlineData("DROP TABLE users")]
    [InlineData("DROP DATABASE production")]
    [InlineData("ALTER TABLE users ADD COLUMN x INT")]
    [InlineData("TRUNCATE users")]
    [InlineData("CREATE TABLE foo (id INT)")]
    [InlineData("CREATE INDEX idx_name ON users(name)")]
    [InlineData("CALL my_proc()")]
    [InlineData("EXECUTE my_prepared")]
    [InlineData("DO $$ BEGIN END $$")]
    public void Validate_RejectsStatementsThatDontStartWithSelectOrExplain(string sql)
    {
        var ex = Assert.Throws<Exception>(() => SqlSafetyValidator.Validate(sql));
        Assert.Contains("Only SELECT/EXPLAIN", ex.Message);
    }

    // ── Forbidden keywords embedded inside a SELECT ──────────────────────────
    // These pass the first guard (start with SELECT) but are caught by the
    // forbidden-keyword check.

    [Theory]
    [InlineData("SELECT * FROM t UNION ALL INSERT INTO t VALUES (1)")]
    [InlineData("SELECT * FROM t; UPDATE t SET x = 1")] // also hits semicolon check first — still throws
    public void Validate_RejectsForbiddenKeywordEmbeddedInSelect(string sql)
        => Assert.Throws<Exception>(() => SqlSafetyValidator.Validate(sql));

    [Fact]
    public void Validate_RejectsSelectThatContainsInsert()
    {
        var ex = Assert.Throws<Exception>(() =>
            SqlSafetyValidator.Validate("SELECT * FROM t UNION ALL INSERT INTO foo VALUES (1)"));
        Assert.Contains("Forbidden", ex.Message);
    }

    [Fact]
    public void Validate_RejectsSelectThatContainsDrop()
    {
        var ex = Assert.Throws<Exception>(() =>
            SqlSafetyValidator.Validate("SELECT * FROM t WHERE id IN (SELECT id FROM DROP_TABLE_ALIAS)"));
        // "DROP" appears in the column alias name above → caught as forbidden
        Assert.Contains("Forbidden", ex.Message);
    }

    // ── Multi-statement prevention ───────────────────────────────────────────

    [Fact]
    public void Validate_RejectsSemicolon()
    {
        var ex = Assert.Throws<Exception>(() => SqlSafetyValidator.Validate("SELECT 1; SELECT 2"));
        Assert.Contains("Multiple statements", ex.Message);
    }

    [Fact]
    public void Validate_RejectsSemicolonAtEnd()
    {
        var ex = Assert.Throws<Exception>(() => SqlSafetyValidator.Validate("SELECT 1;"));
        Assert.Contains("Multiple statements", ex.Message);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_RejectsEmptyString()
        => Assert.Throws<Exception>(() => SqlSafetyValidator.Validate(""));

    [Fact]
    public void Validate_ForbiddenKeywordIsCaseInsensitive()
    {
        // lowercase forbidden keyword is upcased before checking — still caught
        var ex = Assert.Throws<Exception>(() =>
            SqlSafetyValidator.Validate("SELECT * FROM t UNION ALL insert into foo VALUES (1)"));
        Assert.Contains("Forbidden", ex.Message);
    }
}
