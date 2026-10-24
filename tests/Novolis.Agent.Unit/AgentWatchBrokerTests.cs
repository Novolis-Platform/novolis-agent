using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Agent.Testing;

namespace Novolis.Agent.Unit;

public sealed class AgentWatchBrokerTests
{
    [Test]
    public async Task Watch_filters_replays_and_closes_on_overflow_or_disconnect()
    {
        var host = new FakeAgentHost();
        host.RaiseChanged("before");
        using var broker = new AgentEventBroker(host, replayLimit: 4);
        var sink = new RecordingSink();
        var opened = broker.Open(new AgentWatchRequest
        {
            CallbackId = "cb",
            EventNames = [AgentMethodNames.Changed],
            CorrelationId = "corr",
        }, sink);
        await Assert.That(opened.Ok).IsTrue();
        await Assert.That(sink.Events.Count).IsEqualTo(0);

        host.RaiseDecision("ignored");
        host.RaiseChanged("other");
        Changed(host, "kept", "corr");
        await Assert.That(sink.Events.Count).IsEqualTo(1);
        await Assert.That(sink.Events[0].IsReplay).IsFalse();
        await Assert.That(sink.Events[0].CorrelationId).IsEqualTo("corr");
        await Assert.That(sink.Events[0].CallbackId).IsEqualTo("cb");

        var replaySink = new RecordingSink();
        var replay = broker.Open(new AgentWatchRequest { EventNames = [AgentMethodNames.Changed], AfterEventSequence = 0 }, replaySink);
        await Assert.That(replay.Ok).IsTrue();
        await Assert.That(replaySink.Events.Count).IsGreaterThan(0);
        await Assert.That(replaySink.Events[0].IsReplay).IsTrue();
        await Assert.That(replaySink.Events[0].EventSequence).IsLessThan(replaySink.Events[^1].EventSequence + 1);

        var full = new RecordingSink { FailAfter = 1 };
        var limited = broker.Open(new AgentWatchRequest(), full);
        await Assert.That(limited.Ok).IsTrue();
        host.RaiseChanged("one");
        host.RaiseChanged("two");
        await Assert.That(full.Events.Count).IsEqualTo(1);
        host.RaiseChanged("three");
        await Assert.That(full.Events.Count).IsEqualTo(1);

        broker.CloseConnection(sink, "disconnect");
        var before = sink.Events.Count;
        Changed(host, "after-disconnect", "corr");
        await Assert.That(sink.Events.Count).IsEqualTo(before);
        await Assert.That(sink.Events[^1].EventName).IsEqualTo(AgentMethodNames.WatchClosed);
    }

    [Test]
    public async Task Callback_watch_can_be_cancelled()
    {
        var host = new FakeAgentHost();
        using var broker = new AgentEventBroker(host);
        var count = 0;
        var seen = new TaskCompletionSource<AgentWatchEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = broker.Watch(
            new AgentWatchRequest { CallbackId = "local" },
            (evt, _) =>
            {
                count++;
                seen.TrySetResult(evt);
                return ValueTask.CompletedTask;
            });
        host.RaiseChanged("live");
        var first = await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.That(first.EventName).IsEqualTo(AgentMethodNames.Changed);
        await subscription.DisposeAsync();
        await Task.Delay(100);
        var afterDispose = count;
        host.RaiseChanged("after");
        await Task.Delay(100);
        await Assert.That(count).IsEqualTo(afterDispose);
    }

    [Test]
    public async Task Poll_resumes_after_a_sequence_and_unknown_watch_fails()
    {
        var host = new FakeAgentHost();
        using var broker = new AgentEventBroker(host);
        Changed(host, "a", "c");
        Changed(host, "b", "c");
        var opened = broker.OpenPoll(new AgentWatchRequest { AfterEventSequence = 1, CorrelationId = "c" });
        var page = await broker.PollAsync(opened.WatchId, 8, TimeSpan.FromMilliseconds(50));
        await Assert.That(page.Ok).IsTrue();
        await Assert.That(page.Events.Length).IsEqualTo(1);
        await Assert.That(page.Events[0].EventSequence).IsEqualTo(2);
        broker.Close(opened.WatchId, null);
        var missing = await broker.PollAsync("missing", 1, TimeSpan.FromMilliseconds(20));
        await Assert.That(missing.Ok).IsFalse();
    }

    private static void Changed(FakeAgentHost host, string reason, string correlation) =>
        host.RaiseChanged(new AgentChangedEvent { Reason = reason, CorrelationId = correlation, Severity = AgentSeverity.Info });

    private sealed class RecordingSink : IAgentEventSink
    {
        public List<AgentWatchEvent> Events { get; } = [];

        public int FailAfter { get; init; } = int.MaxValue;

        public string ConnectionId => "recording";

        public bool TryEnqueue(AgentWatchEvent value)
        {
            if (Events.Count >= FailAfter)
            {
                return false;
            }

            Events.Add(value);
            return true;
        }
    }
}
