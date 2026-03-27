using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class GetDatabaseStatsTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Returns database-level health stats: size, connection count, cache hit ratio, deadlocks, and temp file usage.")]
    public Task<QueryResult> GetDatabaseStats()
        => _service.GetDatabaseStatsAsync();
}
