using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcp.Server.Models;
using PostgresMcp.Server.Services;

namespace PostgresMcp.Server.Tools;

[McpServerToolType]
public class GetTableSchemaTool(QueryService service)
{
    private readonly QueryService _service = service;

    [McpServerTool]
    [Description("Returns the column definitions for a table: data types, nullability, defaults, primary key, and unique constraints.")]
    public Task<QueryResult> GetTableSchema(
        [Description("Name of the table to inspect (without schema prefix).")] string tableName)
        => _service.GetTableSchemaAsync(tableName);
}
