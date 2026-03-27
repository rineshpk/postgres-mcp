using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class GetTableVacuumStatsTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Returns vacuum and analyze stats for all user tables: live/dead row counts, dead row percentage, and last vacuum/analyze timestamps. Tables with high dead_row_pct need VACUUM.")]
    public Task<QueryResult> GetTableVacuumStats()
        => _service.GetTableVacuumStatsAsync();
}
