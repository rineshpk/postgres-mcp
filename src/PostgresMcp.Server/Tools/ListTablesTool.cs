using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class ListTablesTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Lists all user tables in the database with their size and estimated row counts.")]
    public Task<QueryResult> ListTables()
        => _service.ListTablesAsync();
}
