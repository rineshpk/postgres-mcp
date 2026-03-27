using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;
[McpServerToolType]
public class LongRunningQueriesTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Identifies long-running queries in the database.")]
    public async Task<object> GetLongRunningQueries()
        => await _service.GetLongRunningQueriesAsync();
}
