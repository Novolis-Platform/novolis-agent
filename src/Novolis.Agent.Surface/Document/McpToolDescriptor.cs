using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>MCP tool descriptor: name, human summary, and JSON Schema input shape.</summary>
public sealed record McpToolDescriptor(string Name, string Description, Dictionary<string, object?> InputSchema);
