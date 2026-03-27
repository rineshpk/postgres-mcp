using Dapper;
using Npgsql;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Validators;

namespace PostgresMcp.Server.Services;

public class QueryService
{
    private readonly string _connectionString;

    public QueryService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<QueryResult> ExecuteAsync(string query, int limit)
    {
        SqlSafetyValidator.Validate(query);

        limit = Math.Min(limit, 100);
        var sql = $"{query} LIMIT {limit}";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();

        var rows = (await conn.QueryAsync(sql))
            .Select(r => (IDictionary<string, object>)r)
            .Select(d => d.ToDictionary(k => k.Key, v => v.Value))
            .ToList();

        sw.Stop();

        return new QueryResult(rows, rows.Count, sw.Elapsed.TotalMilliseconds);
    }

    public async Task<ExplainResult> ExplainAsync(string query)
    {
        SqlSafetyValidator.Validate(query);

        var sql = $"EXPLAIN ANALYZE {query}";

        await using var conn = new NpgsqlConnection(_connectionString);
        var result = await conn.QueryAsync<string>(sql);

        return new ExplainResult(result.ToList());
    }

    public async Task<object> GetLongRunningQueriesAsync()
    {
        var sql = @"
        SELECT pid, now() - query_start AS duration, query
        FROM pg_stat_activity
        WHERE state = 'active'
        ORDER BY duration DESC
        LIMIT 10";

        return await ExecuteAsync(sql, 10);
    }

    public object SuggestIndexes(string query)
    {
        var suggestions = new List<string>();

        var whereMatch = Regex.Match(query, @"WHERE\s+(.*)", RegexOptions.IgnoreCase);
        if (whereMatch.Success)
        {
            foreach (var col in ExtractColumns(whereMatch.Groups[1].Value))
            {
                suggestions.Add($"CREATE INDEX idx_{col} ON table_name({col});");
            }
        }

        return new { suggestions = suggestions.Distinct() };
    }

    public async Task<QueryResult> ListTablesAsync()
    {
        var sql = @"
            SELECT
                schemaname AS schema,
                tablename AS table,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS total_size,
                pg_size_pretty(pg_relation_size(schemaname||'.'||tablename)) AS table_size,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename) - pg_relation_size(schemaname||'.'||tablename)) AS index_size,
                n_live_tup AS estimated_rows
            FROM pg_stat_user_tables
            ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC";

        return await ExecuteInternalAsync(sql);
    }

    public async Task<QueryResult> GetTableSchemaAsync(string tableName)
    {
        var sql = @"
            SELECT
                c.column_name,
                c.data_type,
                c.character_maximum_length AS max_length,
                c.is_nullable,
                c.column_default,
                CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_primary_key,
                CASE WHEN uq.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_unique
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name AND tc.table_name = ku.table_name
                WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_name = @tableName
            ) pk ON c.column_name = pk.column_name
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name AND tc.table_name = ku.table_name
                WHERE tc.constraint_type = 'UNIQUE' AND tc.table_name = @tableName
            ) uq ON c.column_name = uq.column_name
            WHERE c.table_name = @tableName
            ORDER BY c.ordinal_position";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();
        var rows = (await conn.QueryAsync(sql, new { tableName }))
            .Select(r => (IDictionary<string, object>)r)
            .Select(d => d.ToDictionary(k => k.Key, v => v.Value))
            .ToList();
        sw.Stop();

        return new QueryResult(rows, rows.Count, sw.Elapsed.TotalMilliseconds);
    }

    public async Task<QueryResult> GetDatabaseStatsAsync()
    {
        var sql = @"
            SELECT
                datname AS database_name,
                pg_size_pretty(pg_database_size(datname)) AS size,
                numbackends AS active_connections,
                (SELECT setting::int FROM pg_settings WHERE name = 'max_connections') AS max_connections,
                xact_commit AS transactions_committed,
                xact_rollback AS transactions_rolled_back,
                CASE WHEN (blks_hit + blks_read) > 0
                     THEN ROUND(100.0 * blks_hit / (blks_hit + blks_read), 2)
                     ELSE 0 END AS cache_hit_ratio_pct,
                deadlocks,
                temp_files,
                pg_size_pretty(temp_bytes) AS temp_bytes_used,
                stats_reset
            FROM pg_stat_database
            WHERE datname = current_database()";

        return await ExecuteInternalAsync(sql);
    }

    public async Task<QueryResult> GetIndexStatsAsync()
    {
        var sql = @"
            SELECT
                schemaname AS schema,
                tablename AS table,
                indexname AS index,
                idx_scan AS times_used,
                idx_tup_read AS tuples_read,
                idx_tup_fetch AS tuples_fetched,
                pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
                CASE WHEN idx_scan = 0 THEN 'UNUSED' ELSE 'ACTIVE' END AS status
            FROM pg_stat_user_indexes
            ORDER BY idx_scan ASC, pg_relation_size(indexrelid) DESC
            LIMIT 50";

        return await ExecuteInternalAsync(sql);
    }

    public async Task<QueryResult> GetLocksAsync()
    {
        var sql = @"
            SELECT
                blocked.pid AS blocked_pid,
                SUBSTRING(blocked.query, 1, 100) AS blocked_query,
                blocking.pid AS blocking_pid,
                SUBSTRING(blocking.query, 1, 100) AS blocking_query,
                blocked.wait_event_type,
                blocked.wait_event,
                now() - blocked.query_start AS blocked_duration
            FROM pg_stat_activity blocked
            JOIN pg_locks blocked_locks ON blocked.pid = blocked_locks.pid AND NOT blocked_locks.granted
            JOIN pg_locks blocking_locks
                ON blocking_locks.granted
                AND blocked_locks.locktype = blocking_locks.locktype
                AND blocked_locks.relation IS NOT DISTINCT FROM blocking_locks.relation
                AND blocked_locks.page IS NOT DISTINCT FROM blocking_locks.page
                AND blocked_locks.tuple IS NOT DISTINCT FROM blocking_locks.tuple
            JOIN pg_stat_activity blocking ON blocking.pid = blocking_locks.pid
            WHERE blocked.state = 'active'
            LIMIT 20";

        return await ExecuteInternalAsync(sql);
    }

    public async Task<QueryResult> GetTableVacuumStatsAsync()
    {
        var sql = @"
            SELECT
                schemaname AS schema,
                tablename AS table,
                n_live_tup AS live_rows,
                n_dead_tup AS dead_rows,
                CASE WHEN n_live_tup > 0
                     THEN ROUND(100.0 * n_dead_tup / n_live_tup, 2)
                     ELSE 0 END AS dead_row_pct,
                last_vacuum,
                last_autovacuum,
                last_analyze,
                last_autoanalyze,
                vacuum_count,
                autovacuum_count
            FROM pg_stat_user_tables
            ORDER BY n_dead_tup DESC
            LIMIT 30";

        return await ExecuteInternalAsync(sql);
    }

    private async Task<QueryResult> ExecuteInternalAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();
        var rows = (await conn.QueryAsync(sql))
            .Select(r => (IDictionary<string, object>)r)
            .Select(d => d.ToDictionary(k => k.Key, v => v.Value))
            .ToList();
        sw.Stop();

        return new QueryResult(rows, rows.Count, sw.Elapsed.TotalMilliseconds);
    }

    private static List<string> ExtractColumns(string input)
    {
        var matches = Regex.Matches(input, @"(\w+)\s*=");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }
}