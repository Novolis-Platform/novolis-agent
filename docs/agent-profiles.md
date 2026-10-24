# Agent profile authoring

The protocol in `Novolis.Agent.Core` and `Novolis.Agent.Surface` is host-neutral. A profile supplies capabilities, mutations, artifacts, and events. Filesystem, build, test, source-control, UI, and game rules stay in the profile.

## Declare capabilities

Publish one stable action id per operation. Set `Capability`, `SafetyClass` (`read`, `mutate`, or `irreversible`), `ApprovalRequired`, `Idempotency`, `EffectPreview`, and a JSON schema. Compact param hints remain for older clients; the schema is what the host validates.

Read-only observation may run without an approval token. A mutation, process termination, source-control change, or publish sets `ApprovalRequired` and returns `approval-required` until `approvalToken` is present. The development profile issues that token as the proposal correlation id. Game play can keep `ApprovalRequired` false when the product UI is the gate.

## Revisions and receipts

`AgentSnapshot.StateRevision` increases only when an accepted command changes host state. `AgentCommandGate` rejects a mismatched `expectedRevision` with `stale-snapshot` and does not apply the command. Repeat the same `correlationId` to replay the stored receipt. Every terminal result carries an `AgentReceipt` with lifecycle (`preview`, `applied`, `rejected`, or `failed`), reason, recovery text, effects, and artifact references.

## Artifacts and events

Put files, logs, and diffs in `AgentArtifactRef`. Put compiler, test, and domain problems in `AgentDiagnostic` on an `AgentObservation`. Raise the existing `Changed` and `ActionResult` events with the same correlation id. Do not invent a second event protocol.

## Watch and replay

Callers open `agent.watch` on the live connection, filter by event name, correlation, resource, and severity, and resume with `afterEventSequence`. Delivery is at-most-once. A full outbound queue closes that watch. Disconnect removes only that connection's watches. `AgentScenario` plus `AgentTrace` in `Novolis.Agent.Testing` replay a script and report the first diverging action. Live model calls stay outside CI.

## Reference profiles

`novolis-agent-dev` (`Novolis.Tools.AgentDev`) is the development profile: inspect, diagnostics, build, test, diff, propose, apply, and evidence. Apply is the only approval-gated write. The Captain bridge is a second profile over the same contract: capability `captain.play`, stable action ids, revisioned receipts, and semantic UI ids `calypso.cash`, `calypso.runway`, `calypso.life`, `calypso.receipt`, and `calypso.error`. UI framework types do not belong in `novolis-agent`.
