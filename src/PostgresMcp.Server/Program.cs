using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostgresMcp.Server.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? throw new Exception("POSTGRES_CONNECTION not set");

// register services
builder.Services.AddSingleton<QueryService>(_ =>
    new QueryService(connectionString));

// MCP server
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

await app.RunAsync();
