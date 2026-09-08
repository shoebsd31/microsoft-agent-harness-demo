# How to run

## Prerequisites

- .NET 10 SDK (`dotnet --list-sdks` shows `10.0.x`). The repo's `global.json` rolls forward to any 10.0 feature band.
- Windows, macOS or Linux. No Docker.
- Optional for the shell tool on Windows: Git for Windows (Git Bash is preferred when present so `ls`, `cat`, `wc`, `grep` work; otherwise PowerShell is used and `dir`, `type`, `findstr` work).
- Optional for live mode: an Azure AI Foundry / Azure OpenAI endpoint, an API key and a chat model deployment (Responses API capable, e.g. a GPT-4.1/5-class deployment).
- Optional for the SQL Server backend: SQL Server 2019+ (Express is fine) with the AdventureWorks2019 sample database restored. Without it the app uses the JSON seed files.

## 1. Build and test

```bash
dotnet build
dotnet test --solution ProcurementCopilot.slnx
```

Or run the whole verification pipeline (also scans for vulnerable packages and checks the 150-line rule):

```bash
./scripts/verify.sh
```

```powershell
pwsh ./scripts/verify.ps1
```

## 2. Offline demo (no credentials)

```bash
dotnet run --project src/ProcurementCopilot.Console -- --fake
```

Fake mode replays a canned end-to-end run through the *real* harness (real tools, providers, approvals, session store,
telemetry); only the model is scripted.

On a real terminal the console opens the **full-screen TUI** (transcript on the left, prompt below it, live *Todos / Background
tasks / Context / Traces* panels on the right, shortcuts in the status bar). Add `--classic` for the line-oriented UI (also used
automatically when input or output is redirected, e.g. when piping prompts in). Everything below works the same in both.
Follow the banner:

1. `Evaluate RFP-2026-017 and recommend a vendor.` → plan turn: reads the RFP and bids, creates 8 todos, writes the plan to file memory, asks to switch mode.
2. `/mode execute`
3. `go` → scores all five bids, checks compliance (VND-0004 blocked), delegates to both background agents, then asks approval for the clarification email, the memo write and the award recommendation. Answer `y` (once), `a` (always this session) or `n` (deny).
4. Explore: `/todos`, `/tasks`, `/context`, `/approvals`, `/traces`, `/session list`, `/whois`, `/help`, `/exit`.
5. Open a **second terminal** and watch the same session live: `dotnet run --project src/ProcurementCopilot.Console -- --attach <session id from the banner>`
   (see section 4b).

Outputs land under `src/ProcurementCopilot.Console/bin/<config>/net10.0/workspace/output/` (memo, `award-recommendation.json`,
`outbox/`, `audit/approvals.jsonl`, `audit/actions.jsonl`). Logs and trace files are under `.../bin/<config>/net10.0/logs/`.

If Foundry is not configured, running without `--fake` also falls back to fake mode with a warning.

## 3. Live mode (Azure AI Foundry / Azure OpenAI)

Never put secrets in `appsettings.json`. Use user-secrets in development:

```bash
cd src/ProcurementCopilot.Console
dotnet user-secrets set "Foundry:Endpoint" "https://<resource>.openai.azure.com"
dotnet user-secrets set "Foundry:ApiKey" "<your-api-key>"
dotnet user-secrets set "Foundry:DeploymentName" "<chat-deployment>"
dotnet user-secrets set "Foundry:JudgeDeploymentName" "<optional-judge-deployment>"
```

or environment variables (`Foundry__Endpoint`, `Foundry__ApiKey`, `Foundry__DeploymentName`). `Foundry:UseV1Path` (default `true`)
appends `/openai/v1` to the endpoint when it is missing, which is what the Responses API on Azure expects.

```bash
dotnet run --project src/ProcurementCopilot.Console
```

Then follow the prompt catalogue in [docs/DEMO_SCRIPT.md](docs/DEMO_SCRIPT.md#3-prompt-catalogue-every-harness-capability-on-sql-server--foundry).
Start-up validates configuration and names any missing key (never its value). Check without calling the model:

```bash
dotnet run --project src/ProcurementCopilot.Console -- --self-check
```

## 3b. SQL Server backend (AdventureWorks2019) and the admin app

The default `Data:Provider` is `Auto`: when `SqlServer:ConnectionString` (default `Server=localhost\SQLEXPRESS;Database=AdventureWorks2019;Integrated Security=True;…`)
answers and the `copilot` schema exists, the copilot runs on AdventureWorks; otherwise it falls back to the JSON files with a warning.

```bash
./scripts/apply-migrations.sh            # pwsh ./scripts/apply-migrations.ps1 on Windows; adds Procurement + copilot schemas and the demo seed
./scripts/apply-migrations.sh --status   # applied / pending scripts
dotnet run --project src/ProcurementCopilot.Console -- --self-check   # expect: Data backend | SqlServer, 4 applied, up to date
dotnet run --project src/ProcurementCopilot.Admin    # Blazor admin at http://localhost:5010
```

With the SQL backend the scenario switches to Adventure Works Cycles (RFP-2026-017 = 2,000 HL Mountain Tires, USD, five real
vendors `VND-1498`, `VND-1652`, `VND-1526`, `VND-1678`, `VND-1632`), the approval-gated `query_readonly` tool appears, and
`record_award_recommendation` creates a pending purchase order in AdventureWorks. The admin app edits RFPs, bids, vendor
profiles (country, years, notes, certifications) and the restricted-parties list, shows recorded awards with their purchase
orders, and can apply pending migrations. Details: [docs/DATABASE.md](docs/DATABASE.md). Force a backend with
`"Data": { "Provider": "Json" }` or `"SqlServer"`. `--fake` always uses JSON.

## 4. Console commands

| Command | Purpose |
|---|---|
| `/help` | list commands |
| `/todos` | show the todo list |
| `/mode [plan\|execute]` | show or switch mode; side-effecting tools only work in execute |
| `/session new \| list \| resume <id> \| attach <id>` | manage persisted sessions (`%LOCALAPPDATA%/ProcurementCopilot/sessions` by default); `resume` drives, `attach` observes; `list` shows which instance drives each session |
| `/context` | token usage, context budget and whether compaction fired |
| `/approvals [clear]` | list or revoke standing ("always") approvals |
| `/tasks` | background agent tasks and status |
| `/traces` | most recent OpenTelemetry spans |
| `/data` | active data backend (JSON or SQL Server) and migration status |
| `/whois` | which instance drives this session, its heartbeat and last published activity |
| `/takeover` | observer only: drive the session from here once the current driver has exited or detached |
| `/detach` | driver only: save, release the session lock, keep watching as an observer |
| `/exit` | save the session and quit |

Command-line flags: `--fake` (offline), `--session <guid>` (resume and drive), `--attach <guid>` (observe), `--tui` / `--classic`
(force a front-end), `--self-check` (validate configuration, no model call).

**TUI keys**: `Enter` send, `Esc` cancel the current run, `F1` help, `F2` toggle plan/execute, `F3` todos, `F4` traces,
`F5` take over / detach, `F6` session list, `F7` who drives, `Ctrl+Q` quit, `Up`/`Down` prompt history, `Tab` moves the
focus into the panels (scroll with the arrow keys / PageUp / PageDown). Approvals open a dialog with *Once / Always this
session / Deny*. Slash-command output is written into the transcript.

**Classic keys**: `Ctrl+C` cancels the current run; a second `Ctrl+C` (or `Ctrl+C` while idle) saves the session and exits.

### 4b. Second instance: observe a running session, take it over, hand it back

Every driving instance writes `<id>.status.json` (activity, current tool, recent tool calls, token usage, background tasks,
compaction count, recent spans, outstanding loop items) next to the session file, and holds `<id>.lock` with a 10-second
heartbeat. A second instance can attach read-only while the first one is still working:

```bash
dotnet run --project src/ProcurementCopilot.Console -- --attach <guid>
```

The observer shows the driver's tool calls and activity changes as they happen, re-reads the session file whenever the
driver checkpoints it (todos, history, evaluation state), and answers `/todos`, `/tasks`, `/context`, `/traces`, `/whois`
from the published status. Prompts typed in the observer are refused with the name of the driving instance. When the driver
exits, presses `Ctrl+Q`, or runs `/detach`, the lock is released and the observer can `/takeover` (or press `F5`) and
continue the same conversation; the previous driver becomes an observer and can take it back the same way. Starting
`--session <guid>` while another live instance drives that session attaches as an observer automatically. A lock whose
process is gone or whose heartbeat is older than 45 seconds is treated as stale.

## 5. Configuration reference (`appsettings.json`)

| Section | Keys | Notes |
|---|---|---|
| `Foundry` | `Endpoint`, `ApiKey`, `DeploymentName`, `JudgeDeploymentName`, `UseV1Path`, `StoreResponses` | secrets via user-secrets / env only |
| `Agent` | `MaxContextWindowTokens`, `MaxOutputTokens`, `MaxFunctionInvocationIterations`, `MaxLoopIterations`, `EnableWebSearch`, `EnableLocalWebFetch`, `EnableJudge`, `BackgroundAgents:{MaxParallel,TimeoutSeconds}` | all bounds are explicit |
| `Workspace` | `Root`, `ReadOnlyPaths`, `WritablePaths`, `MaxFileBytes`, `AllowedExtensions` | one confinement policy for files, shell and outbox |
| `Security:ApprovalPolicy` | `RequireApprovalFor` | single source of truth for approval-gated tools (includes `query_readonly`) |
| `Data` | `Provider` (`Auto`, `Json`, `SqlServer`) | which backend to use |
| `SqlServer` | `ConnectionString`, `CommandTimeoutSeconds`, `QueryRowLimit`, `ReadOnlyUser`, `PurchaseOrderEmployeeId`, `ShipMethodId`, `MigrationsDirectory` | AdventureWorks connection, `query_readonly` limits, purchase-order defaults |
| `Security:Shell` | `AllowedCommands`, `TimeoutSeconds`, `MaxOutputBytes` | allowlist evaluated before any shell runs |
| `Sessions` | `Directory` | empty = `%LOCALAPPDATA%/ProcurementCopilot/sessions` |
| `OpenTelemetry` | `OtlpEndpoint`, `EnableSensitiveData`, `ConsoleExporter` | OTLP when set, else file exporter + `/traces` |
| `Serilog` | `MinimumLevel` | logs go to `logs/` only, redacted |

## 6. Tests

| Project | What |
|---|---|
| `ProcurementCopilot.Domain.Tests` | pinned seeded scores, value objects, currency, compliance |
| `ProcurementCopilot.Application.Tests` | path policy, shell policy (49 table-driven cases), SQL query policy (35 cases), approval precedence, mode guard, redaction, use cases, session store, outbox/audit |
| `ProcurementCopilot.Agent.Tests` | harness options from config, per-call persistence, compaction, loop evaluator, background agents, skills, approvals, mode guard, shell tool, `query_readonly` tool, file store |
| `ProcurementCopilot.Integration.Tests` | one live Foundry smoke run (skipped unless `Foundry__*` are set) and four SQL Server tests over the migrated AdventureWorks database (skipped when it is unreachable; the award test cleans up the purchase order it creates) |

Run one project: `dotnet test --project tests/ProcurementCopilot.Agent.Tests`. Coverage: add `--coverage --coverage-settings <absolute path to coverage.runsettings> --coverage-output-format cobertura` (the settings path must be absolute because each test executable resolves it from its own directory; the file excludes generated sources and the test/fake assemblies).

## 7. Where files and data are stored

Nothing the harness produces is written into the repository. `<bin>` below means
`src/ProcurementCopilot.Console/bin/<Debug|Release>/net10.0/`, the executable's directory.

| What | Location | Committed? |
|---|---|---|
| Seed data (RFPs, vendors, bids, sanctions, FX) | JSON backend: `<bin>/data/` (copied at build from `data/`). SQL backend: AdventureWorks2019 plus the `Procurement` schema created by `database/migrations` | `data/`, `database/` yes |
| Skills | `<bin>/skills/` (copied at build from `skills/`; never loaded from the working directory) | `skills/` yes |
| Workspace inputs read by the agent | `<bin>/workspace/rfps/` (copied at build from `workspace/rfps/`) | `workspace/rfps/` yes |
| Agent outputs: award memo, `award-recommendation.json`, `outbox/*.md`, `audit/approvals.jsonl`, `audit/actions.jsonl` (SQL backend: the award additionally creates a pending purchase order in AdventureWorks; remove it with `scripts/reset-demo.ps1 -Database`) | `<bin>/workspace/output/` | no (`bin/` is ignored; `workspace/output/**` in the repo is ignored too, only `.gitkeep` is kept) |
| Session file memory (`file_memory_*` tools) | `<bin>/workspace/agent-file-memory/<timestamp>_<guid>/` | no |
| Persisted sessions (`/session`, `--session <id>`, `--attach <id>`) | `%LOCALAPPDATA%\ProcurementCopilot\sessions\<guid>.json` + `<guid>.meta.json`, plus `<guid>.status.json` (live status of the driving instance) and `<guid>.lock` (who drives it; deleted on exit) (Linux/macOS: `~/.local/share/ProcurementCopilot/sessions`); override with `Sessions:Directory` | no (outside the repo) |
| Logs and traces | `<bin>/logs/procurement-copilot-<date>.log`, `<bin>/logs/traces-<date>.jsonl` (redacted) | no |
| Foundry credentials | `%APPDATA%\Microsoft\UserSecrets\procurement-copilot-9c1f0c2e\secrets.json` (user-secrets) or environment variables | no (outside the repo) |
| Test artefacts | `TestResults/` (coverage), `%TEMP%\pc-tests`, `%TEMP%\pc-agent-tests`, `%TEMP%\pc-integration` (scratch workspaces) | no |

To start from a clean slate run the reset script, which deletes `<bin>/logs` and `<bin>/workspace` for every build
configuration (the next build recreates the read-only inputs):

```bash
./scripts/reset-demo.sh              # bin folders only
./scripts/reset-demo.sh --sessions   # also the persisted sessions
./scripts/reset-demo.sh --all        # sessions plus TestResults
./scripts/reset-demo.sh --dry-run    # show what would be deleted
```

```powershell
pwsh ./scripts/reset-demo.ps1                 # bin folders only
pwsh ./scripts/reset-demo.ps1 -Sessions       # also the persisted sessions
pwsh ./scripts/reset-demo.ps1 -All            # sessions plus TestResults
pwsh ./scripts/reset-demo.ps1 -WhatIf         # show what would be deleted
```

## Troubleshooting

- **"Start-up failed: … Foundry:Endpoint must be an absolute http(s) URL"** — fix the endpoint; leave it empty to use fake mode.
- **Hosted web search warning** — the selected chat client does not expose the Responses API; the demo continues without web search.
- **Shell tool says `CommandNotAllowed`** — only the configured allowlist runs; see `Security:Shell:AllowedCommands`.
- **Approval prompt appears for `file_access_write`** — by design: writes to the workspace are approval-gated; reads are auto-approved.
- **Nothing in `workspace/output` at the repo root** — outputs are written next to the executable (`bin/.../workspace/output`) because the workspace is copied to the build output.
