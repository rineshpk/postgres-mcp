using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;
[McpServerToolType]
public class ExecuteQueryTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Executes a SQL query and returns the results.")] 
    public Task<QueryResult> Execute(string query, int limit = 100)
        => _service.ExecuteAsync(query, limit);
}
