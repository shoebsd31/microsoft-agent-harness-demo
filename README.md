# Procurement Copilot — Microsoft Agent Framework Harness demo (.NET 10)

A complete, tested .NET 10 solution that exercises **every row of the Agent Framework Harness capability matrix**
in one realistic business scenario: a procurement analyst at *Contoso Industrial Systems* evaluating an open RFP
(`RFP-2026-017`, 40 CNC vertical machining centres, EUR) with five vendor bids — one sanctioned vendor, one missing
ISO 14001, one bid in USD and one ambiguous delivery clause.

It runs fully **offline** (`--fake`, scripted model) or **live** against Azure AI Foundry / Azure OpenAI with an API key.

> Quick start: `dotnet run --project src/ProcurementCopilot.Console -- --fake` and type
> `Evaluate RFP-2026-017 and recommend a vendor.` — see [how-to-run.md](how-to-run.md).

## What it demonstrates

| Harness capability | Where it lives | Watch it in the console |
|---|---|---|
| Function invocation (9 tools, 15-iteration limit) | `Agent/Tools/*`, `HarnessAgentFactory` | `▸ score_bid(RFP-2026-017, BID-003) → 89.60` |
| Per-service-call history persistence | `Sessions/CheckpointingChatHistoryProvider` | `/session list`, `/session resume <id>` |
| Compaction (custom strategy) | `Compaction/ProcurementCompactionStrategy` | `/context` |
| Todo tracking | built-in `TodoProvider` | todo panel after each turn, `/todos` |
| Agent modes (plan / execute) | built-in `AgentModeProvider` + `Middleware/ModeGuardMiddleware` | mode badge, `/mode execute` |
| File memory and confined file access | `Files/WorkspaceFileStore` + `WorkspacePathPolicy` | memo written to `workspace/output/` |
| Tool approval (once / always / deny, audited) | `Approval/ApprovalRuleBuilder`, `Ui/ApprovalPrompt` | 🔐 approval panels, `/approvals` |
| OpenTelemetry (redacted) | `Infrastructure/Telemetry/*` | `/traces`, `logs/traces-*.jsonl` |
| Hosted web search | `WebSearch/WebSearchSupport` | market-research background agent (live mode) |
| Agent Skills (3 file-based skills) | `skills/`, `Skills/SkillsSourceFactory` | `load_skill rfp-scoring` in the tool stream |
| Background agents (2 children, bounded) | `Background/*` | `/tasks` |
| Shell execution (allowlist, sandboxed) | `Shell/*`, `ShellCommandPolicy` | `▸ shell(wc -l …)` after approval |
| Looping (predicate + optional AI judge) | `Looping/AllBidsScoredEvaluator` | `↻ loop:` feedback lines |

The full capability → class → test map is in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); the threat model is in
[docs/SECURITY.md](docs/SECURITY.md); the click-through, including a prompt-by-prompt live run against Azure AI Foundry,
is in [docs/DEMO_SCRIPT.md](docs/DEMO_SCRIPT.md).

## Architecture

```mermaid
flowchart LR
    subgraph Console["ProcurementCopilot.Console (Spectre.Console UX)"]
        UX["InteractiveConsole<br/>commands, streaming, approvals"]
    end
    subgraph Agent["ProcurementCopilot.Agent (harness composition)"]
        F["HarnessAgentFactory"]
        T["9 procurement tools<br/>+ confined shell"]
        MG["ModeGuardMiddleware"]
        CH["Checkpointing history"]
        CS["ProcurementCompactionStrategy"]
        LE["AllBidsScoredEvaluator"]
        BG["market-research / risk-analyst"]
        FS["WorkspaceFileStore"]
    end
    subgraph App["ProcurementCopilot.Application"]
        P["Policies: workspace path, shell allowlist,<br/>approval, secret redaction, untrusted envelope"]
        S["Use cases: scoring, compliance,<br/>clarification, award"]
    end
    subgraph Dom["ProcurementCopilot.Domain (no NuGet deps)"]
        D["Entities, value objects,<br/>BidScoringService, ComplianceChecker"]
    end
    subgraph Infra["ProcurementCopilot.Infrastructure"]
        R["JSON/CSV repositories"]
        SS["File session store, outbox, audit log"]
        FC["FoundryChatClientFactory"]
        OT["OpenTelemetry + Serilog with redaction"]
    end
    UX --> F
    F -->|AsHarnessAgent| H(("HarnessAgent"))
    H --> T & MG & CH & CS & LE & BG & FS
    T --> S --> D
    T --> P
    S --> R & SS
    UX --> FC
    FC -->|Responses API| Foundry[("Azure AI Foundry")]
```

Dependency flow: `Console → Agent → Application → Domain` and `Console → Infrastructure → Application → Domain`.

## Repository layout

```
src/ProcurementCopilot.Domain          pure domain: Result<T>, value objects, scoring, compliance
src/ProcurementCopilot.Application     options, security policies, tool contracts, use cases
src/ProcurementCopilot.Infrastructure  seed repositories, session store, outbox, audit, Foundry client, telemetry, Serilog
src/ProcurementCopilot.Agent           tools, providers, evaluators, background agents, HarnessAgentFactory
src/ProcurementCopilot.Testing         ScriptedChatClient + in-memory fakes (shared by tests and --fake mode)
src/ProcurementCopilot.Console         terminal UX (entry point)
tests/                                 Domain, Application, Agent (fake chat client) and Integration (live, auto-skipped)
data/                                  seeded RFPs, vendors, bids, sanctions, FX rates
workspace/rfps                         read-only RFP documents and bid letters
workspace/output                       agent-writable outputs (memo, outbox, audit) — gitignored
skills/                                rfp-scoring, compliance-check, award-memo
docs/                                  requirements, ARCHITECTURE, SECURITY, DEMO_SCRIPT, DEVIATIONS, OPEN_QUESTIONS, API_NOTES
scripts/                               verify.ps1 / verify.sh (restore, build, test+coverage, vuln scan, 150-line check, self-check)
                                       reset-demo.ps1 / reset-demo.sh (delete bin/logs and bin/workspace; --sessions / --all optional)
```

## Build and verify

```bash
./scripts/verify.sh        # or: pwsh ./scripts/verify.ps1
```

The scripts run `dotnet restore`, `dotnet build -warnaserror`, `dotnet test` with code coverage,
`dotnet list package --vulnerable`, the 150-line-per-file check and `--self-check` (host start-up without a model call).
Everything passes on a clean machine with the .NET 10 SDK and **no** Azure credentials; the live integration test is skipped.

## Security highlights

Least-privilege tools, approval-gated side effects, a plan-mode guard, one workspace confinement policy shared by file
access, shell and outbox, a shell allowlist evaluated on the parsed command line, untrusted-data envelopes around all
vendor text, secret redaction in logs and traces, bounded loops/iterations/timeouts and an append-only audit trail.
Details and the threat → control → test table: [docs/SECURITY.md](docs/SECURITY.md).

## Packages

Microsoft.Agents.AI 1.20.0 (+ Harness, OpenAI), Microsoft.Agents.AI.Tools.Shell 1.20.0-preview, Microsoft.Extensions.AI 10.9,
OpenTelemetry 1.18, Serilog, Spectre.Console, xunit v3, Shouldly, NSubstitute. All free/OSS; versions in
`Directory.Packages.props`. Places where the implementation differs from `docs/requirements.md` are listed in
[docs/DEVIATIONS.md](docs/DEVIATIONS.md).
