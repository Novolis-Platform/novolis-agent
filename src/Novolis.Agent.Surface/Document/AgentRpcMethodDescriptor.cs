using System.Text.Json;
using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

/// <summary>JSON-RPC 2.0 method descriptor exposed by an agent surface.</summary>
public sealed record AgentRpcMethodDescriptor(string Method, string Summary, Dictionary<string, object?>? ParamsSchema);
