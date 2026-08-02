using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace Novolis.Agent.Unit;

public sealed class AgentSurfaceDefinitionEnvTests
{
    readonly List<(string key, string? value)> _saved = [];

    [Before(Test)]
    public void SaveEnv()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        foreach (var key in new[] { def.EnableEnv, def.HttpEnableEnv, def.IpcEnableEnv, def.TcpEnableEnv, def.HttpPortEnv })
        {
            _saved.Add((key, Environment.GetEnvironmentVariable(key)));
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [After(Test)]
    public void RestoreEnv()
    {
        foreach (var (key, value) in _saved)
            Environment.SetEnvironmentVariable(key, value);
        _saved.Clear();
    }

    [Test]
    public async Task BuildHello_and_actions_and_schema()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();
        var hello = def.BuildHello(appId: "demo", appTitle: "Demo");
        await Assert.That(hello.SurfaceId).IsEqualTo("unit");
        await Assert.That(hello.AppId).IsEqualTo("demo");
        await Assert.That(hello.Capabilities).Contains(AgentMethodNames.Hello);

        var actions = def.BuildActions(a =>
        {
            a.Enabled = false;
            return a;
        });
        await Assert.That(actions.Actions[0].Enabled).IsFalse();

        var schema = def.BuildCommandJsonSchema();
        await Assert.That(schema["title"]?.ToString()).IsEqualTo("unit.command");
        var props = schema["properties"] as Dictionary<string, object?>;
        await Assert.That(props).IsNotNull();
        await Assert.That(props!.ContainsKey("actionId")).IsTrue();
    }

    [Test]
    public async Task Environment_gating_and_port_resolution()
    {
        var def = AgentSurfaceDefinition.From<IUnitSurface>();

        Environment.SetEnvironmentVariable(def.EnableEnv, "1");
        await Assert.That(def.IsEnabledByEnvironment()).IsTrue();
        await Assert.That(def.IsHttpEnabledByEnvironment()).IsTrue();
        await Assert.That(def.IsIpcEnabledByEnvironment()).IsTrue();

        Environment.SetEnvironmentVariable(def.HttpEnableEnv, "0");
        await Assert.That(def.IsHttpEnabledByEnvironment()).IsFalse();

        Environment.SetEnvironmentVariable(def.HttpEnableEnv, "1");
        await Assert.That(def.IsHttpEnabledByEnvironment()).IsTrue();

        Environment.SetEnvironmentVariable(def.HttpPortEnv, "19999");
        await Assert.That(def.ResolveHttpPort()).IsEqualTo(19999);

        Environment.SetEnvironmentVariable(def.TcpEnableEnv, "yes");
        await Assert.That(def.IsTcpEnabledByEnvironment()).IsTrue();
    }
}


