using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// ── Configuration ────────────────────────────────────────────────────────────

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");

var serverProjectPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/PostgresMcp.Server"));

if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("Usage: ClientExample <connection-string> [server-project-path]");
    Console.Error.WriteLine("       Or set the POSTGRES_CONNECTION environment variable.");
    return 1;
}

// ── Start MCP server and connect ─────────────────────────────────────────────

Console.WriteLine("Connecting to PostgresMCP server...");
Console.WriteLine($"  Server project: {serverProjectPath}");
Console.WriteLine();

using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning).AddConsole());

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Command = "dotnet",
    Arguments = ["run", "--project", serverProjectPath, "--no-launch-profile"],
    EnvironmentVariables = new Dictionary<string, string?>
    {
        ["POSTGRES_CONNECTION"] = connectionString
    },
    Name = "PostgresMCP"
});

await using var client = await McpClient.CreateAsync(
    transport,
    new McpClientOptions { ClientInfo = new Implementation { Name = "ClientExample", Version = "1.0.0" } },
    loggerFactory);

// ── List available tools ─────────────────────────────────────────────────────

Console.WriteLine("=== Available Tools ===");
var tools = await client.ListToolsAsync();
foreach (var tool in tools)
    Console.WriteLine($"  {tool.Name,-28} {tool.Description}");
Console.WriteLine();

// ── Helper: call tool and print result ───────────────────────────────────────

async Task CallAndPrint(string toolName, Dictionary<string, object?>? args = null)
{
    Console.WriteLine($"=== {toolName} ===");
    try
    {
        var result = await client.CallToolAsync(toolName, args);
        foreach (var content in result.Content)
        {
            if (content is TextContentBlock text)
            {
                // Pretty-print if JSON, otherwise raw text
                try
                {
                    var doc = JsonDocument.Parse(text.Text);
                    Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch
                {
                    Console.WriteLine(text.Text);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
    }
    Console.WriteLine();
}

// ── Run demo calls ───────────────────────────────────────────────────────────

await CallAndPrint("get_database_stats");
await CallAndPrint("list_tables");
await CallAndPrint("get_index_stats");
await CallAndPrint("get_table_vacuum_stats");
await CallAndPrint("get_locks");
await CallAndPrint("get_long_running_queries");

// Demonstrate execute query
await CallAndPrint("execute", new Dictionary<string, object?>
{
    ["query"] = "SELECT current_database() AS database, current_user AS user, version() AS version",
    ["limit"] = 1
});

// Demonstrate index suggestion
await CallAndPrint("suggest_indexes", new Dictionary<string, object?>
{
    ["query"] = "SELECT * FROM orders WHERE user_id = 1 AND status = 'active'"
});

return 0;
