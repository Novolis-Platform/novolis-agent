using Novolis.Agent.Core;

namespace Novolis.Agent.Surface;

public delegate ValueTask AgentWatchCallback(AgentWatchEvent value, CancellationToken cancellationToken);
