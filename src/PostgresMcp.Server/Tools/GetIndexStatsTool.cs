using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class GetIndexStatsTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Returns index usage statistics for all user tables. Highlights unused indexes (idx_scan = 0) sorted by size so you can identify candidates for removal.")]
    public Task<QueryResult> GetIndexStats()
        => _service.GetIndexStatsAsync();
}
